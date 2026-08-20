import HmacHeaderBuilder from "./builders/hmac-header-builder";
import HmacResultFactory from "./components/hmac-result-factory";
import HmacSignatureProvider from "./components/hmac-signature-provider";
import HmacResult from "./components/hmac-result";
import Hmac from "./components/hmac";
import HmacPolicy from "./components/hmac-policy";
import HmacScheme from "./components/hmac-scheme";
import HmacVerificationOptions from "./components/hmac-verification-options";
import HmacVerificationResult from "./components/hmac-verification-result";
import HmacVerificationResultFactory from "./components/hmac-verification-result-factory";
import SigningContentBuilder from "./builders/signing-content-builder";
import SigningContentBuilderAccessor from "./builders/signing-content-builder-accessor";
import HmacHeaderParserFactory from "./parsers/hmac-header-parser-factory";
import NonceStore, { isValidNonce } from "./caching/nonce-store";
import MemoryNonceStore from "./caching/memory-nonce-store";
import BadHeaderFormatError from "./exceptions/bad-header-format-error";
import MissingHeaderError from "./exceptions/missing-header-error";
import { timingSafeEqual } from "./utilities/hmac-utilities";

/**
 * The replay window used when a policy does not name one, in seconds.
 * Matches `Nonce.MaxAgeInSeconds` on the .NET side.
 */
const DefaultMaxAgeInSeconds = 30;

/**
 * HmacManager signs outgoing requests and verifies incoming ones.
 * Initializes the required policy, scheme, provider, and result factory to generate signed requests.
 */
export default class HmacManager {
    /** 
     * The policy configuration for HMAC, containing rules and constraints.
     */
    private readonly policy: HmacPolicy;

    /** 
     * The specific HMAC scheme in use; may be null if no scheme is defined.
     */
    private readonly scheme: HmacScheme | null;

    /** 
     * A builder used to construct headers required for HMAC authentication.
     */
    private readonly headerBuilder: HmacHeaderBuilder;

    /** 
     * Responsible for generating and verifying HMAC signatures.
     */
    private readonly signatureProvider: HmacSignatureProvider;

    /**
     * Produces results from HMAC operations, such as success or failure outcomes.
     */
    private readonly resultFactory: HmacResultFactory;

    /**
     * Produces results from verification, which fail in more ways than signing does.
     */
    private readonly verificationResultFactory: HmacVerificationResultFactory;

    /**
     * Creates the parser matching the header layout incoming requests use.
     */
    private readonly headerParserFactory: HmacHeaderParserFactory;

    /**
     * Records the nonces already spent, so a captured request cannot be replayed.
     */
    private readonly nonceStore: NonceStore;

    /**
     * How long an incoming signature stays valid, in seconds.
     */
    private readonly maxAgeInSeconds: number;

    /**
     * Constructs an HmacManager instance.
     * @param policy - The policy defining the public and private keys for HMAC signing.
     * @param scheme - The scheme specifying headers required for HMAC (optional).
     * @param headerBuilder - Builds the headers `sign` attaches.
     * @param verification - What `verify` needs beyond the above (optional). Prefer
     * `HmacManagerFactory`, which supplies a nonce store shared across every manager it
     * creates; a manager constructed directly gets a store of its own, which only
     * detects replays it has seen itself.
     */
    constructor(
        policy: HmacPolicy,
        scheme: HmacScheme | null = null,
        headerBuilder: HmacHeaderBuilder,
        verification: HmacVerificationOptions = {}
    ) {
        this.policy = policy;
        this.scheme = scheme;
        this.headerBuilder = headerBuilder;
        this.maxAgeInSeconds = policy.maxAgeInSeconds ?? DefaultMaxAgeInSeconds;
        this.headerParserFactory = verification.headerParserFactory ??
            new HmacHeaderParserFactory(false);
        this.nonceStore = verification.nonceStore ??
            new MemoryNonceStore(this.maxAgeInSeconds);

        const signingContentBuilder = this.policy.signingContentAccessor ? 
            new SigningContentBuilderAccessor(this.policy.signingContentAccessor) :
            new SigningContentBuilder();
        
        this.signatureProvider = new HmacSignatureProvider(
            this.policy.publicKey,
            this.policy.privateKey,
            this.scheme?.headers ?? [],
            this.policy.contentHashAlgorithm,
            this.policy.signatureHashAlgorithm,
            signingContentBuilder
        );

        this.resultFactory = new HmacResultFactory();
        this.verificationResultFactory = new HmacVerificationResultFactory();
    }

    /**
     * Signs an HTTP request with HMAC headers.
     * @param request - The HTTP request to sign.
     * @returns A Promise that resolves to an HmacResult indicating success or failure.
     */
    sign = async (request: Request): Promise<HmacResult> => {
        try {
            const {
                dateRequested,
                nonce,
                signingContent,
                signature
            } = await this.computeSignature(request);

            const hmac = {
                policy: this.policy.name,
                scheme: this.scheme?.name ?? null,
                dateRequested,
                nonce,
                signingContent,
                signature,
                signedHeaders: this.scheme?.headers ?? null
            };
            
            this.addHeaders(request.headers, hmac);

            return this.resultFactory.success(hmac);
        } catch (error: unknown) {
            // The cause travels with the result. sign() reports failure through
            // isSuccess rather than throwing, so discarding the error here left
            // a caller with no way to tell a missing scheme header from an
            // unusable key — both surfaced as a server-side 401 and nothing
            // local.
            return this.resultFactory.failure(error);
        }
    }

    /**
     * Verifies that an incoming request carries a signature this policy produced.
     *
     * Each way a request can fail is reported separately, in the same order and by the
     * same rules as `HmacManager.VerifyAsync` on the .NET side. A single "verification
     * failed" would be true but useless: an expired signature, a replayed nonce and a
     * genuine mismatch have entirely different causes and entirely different fixes.
     *
     * Never throws, matching `sign` — a caller-supplied request is untrusted input, and
     * every way it can be wrong is an outcome rather than an exception.
     *
     * @param request - The incoming request, unmodified. Its body is read through a
     * clone, so it remains available to the caller afterwards.
     * @returns Whether it verified, and if not, why.
     */
    verify = async (request: Request): Promise<HmacVerificationResult> => {
        let incoming;
        try {
            incoming = this.headerParserFactory.create(request.headers).parse();
        } catch (error: unknown) {
            return this.verificationResultFactory.failure(
                error instanceof BadHeaderFormatError ? "headers-malformed" : "headers-missing",
                error);
        }

        // A manager resolved by name from these same headers agrees by construction, so
        // this only ever catches a manager the caller picked themselves. It is worth
        // catching: the policy name is not part of the signing content, so two policies
        // sharing a key pair would otherwise verify each other's requests, and the
        // scheme name not matching means the signature covered a different set of
        // headers than this manager is about to check.
        if (incoming.policy !== this.policy.name || incoming.scheme !== (this.scheme?.name ?? null)) {
            return this.verificationResultFactory.failure("policy-not-found");
        }

        if (!this.hasValidDateRequested(incoming.dateRequested)) {
            return this.verificationResultFactory.failure("expired");
        }

        if (!await isValidNonce(this.nonceStore, incoming.nonce, incoming.dateRequested)) {
            return this.verificationResultFactory.failure("replayed");
        }

        let computed;
        try {
            // The caller's date and nonce, this policy's keys. Recomputing with the
            // values the request asserts is the whole mechanism: only something holding
            // the private key can make them produce this signature.
            computed = await this.signatureProvider.compute(request,
                incoming.dateRequested,
                incoming.nonce
            );
        } catch (error: unknown) {
            // A scheme header the signature covers but the request does not carry throws
            // out of the signing content builder. That is a rejected caller, not a fault.
            return this.verificationResultFactory.failure(
                error instanceof MissingHeaderError || error instanceof BadHeaderFormatError ?
                    "headers-missing" :
                    "verification-error",
                error);
        }

        if (!timingSafeEqual(computed.signature, incoming.signature)) {
            return this.verificationResultFactory.failure("signature-mismatch");
        }

        const hmac: Hmac = {
            policy: this.policy.name,
            scheme: this.scheme?.name ?? null,
            dateRequested: incoming.dateRequested,
            nonce: incoming.nonce,
            signingContent: computed.signingContent,
            signature: computed.signature,
            signedHeaders: this.scheme?.headers ?? null
        };

        return this.verificationResultFactory.success(hmac, this.getSchemeHeaderValues(request));
    }

    /**
     * Whether a signature made at this moment is still inside its validity window.
     *
     * Mirrors `HasValidDateRequested` on the .NET side, including its rejection of
     * future-dated requests outright — there is no skew allowance in either direction.
     * A verifier whose clock is behind its signers' will reject their requests, which is
     * a real operational hazard, but the two implementations agreeing about which
     * requests are valid matters more than either being lenient on its own.
     */
    private hasValidDateRequested = (dateRequested: Date): boolean => {
        const elapsed = Date.now() - dateRequested.getTime();
        return elapsed >= 0 && elapsed < this.maxAgeInSeconds * 1000;
    }

    /**
     * Collects the scheme header values the signature covered, by name.
     *
     * Only called once verification has succeeded, at which point every one of them is
     * known to be present — the signing content could not have been built otherwise.
     */
    private getSchemeHeaderValues = (request: Request): Record<string, string> =>
        Object.fromEntries((this.scheme?.headers ?? [])
            .map(name => [name, request.headers.get(name) ?? ""]));

    /**
     * Computes the signature components needed for HMAC authentication.
     * @param request - The HTTP request to compute the signature for.
     * @returns A Promise with dateRequested, nonce, signingContent, and signature.
     */
    private computeSignature = async (request: Request) => {
        const dateRequested = new Date();
        const nonce = crypto.randomUUID();
        const { signingContent, signature } = await this.signatureProvider.compute(request,
            dateRequested,
            nonce
        );

        return { dateRequested, nonce, signingContent, signature };
    }

    /**
     * Adds the required HMAC headers to an HTTP request.
     * @param headers - The headers of the HTTP request.
     * @param hmac - The HMAC object containing the signature data.
     */
    private addHeaders(headers: Headers, hmac: Hmac): void {
        const builder = this.headerBuilder.createBuilder()
            .withAuthorization(hmac.signature)
            .withPolicy(hmac.policy)
            .withScheme(hmac.scheme)
            .withNonce(hmac.nonce)
            .withDateRequested(hmac.dateRequested);
        
        const hmacHeaders = builder.build();
        for (const [name, value] of Object.entries(hmacHeaders)) {
            headers.append(name, value as string);
        }
    }
}