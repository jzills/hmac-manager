import HmacManager from "./hmac-manager";
import HmacPolicy from "./components/hmac-policy";
import HmacPolicyCollection from "./components/hmac-policy-collection";
import HmacHeaderBuilderFactory from "./builders/hmac-header-builder-factory";
import HmacHeaderParserFactory from "./parsers/hmac-header-parser-factory";
import HmacVerificationResult from "./components/hmac-verification-result";
import HmacVerificationResultFactory from "./components/hmac-verification-result-factory";
import NonceStore from "./caching/nonce-store";
import MemoryNonceStore from "./caching/memory-nonce-store";
import BadHeaderFormatError from "./exceptions/bad-header-format-error";

/**
 * A factory class for creating instances of HmacManager based on
 * specified policies and schemes.
 */
export default class HmacManagerFactory {
    /**
     * A collection of HMAC policies used for authentication.
     */
    private readonly policies: HmacPolicyCollection;

    /**
     * Factory responsible for creating instances of HMAC header builders.
     */
    private readonly headerBuilderFactory: HmacHeaderBuilderFactory;

    /**
     * Factory responsible for creating instances of HMAC header parsers.
     */
    private readonly headerParserFactory: HmacHeaderParserFactory;

    /**
     * The nonce store handed to every manager this factory creates.
     *
     * Held here rather than per manager because `create` returns a new manager each
     * call: a store built alongside each one would only ever contain the nonces that
     * single manager had seen, which for the usual per-request `create` is none of them,
     * and replay detection would silently never fire.
     */
    private readonly nonceStore: NonceStore;

    /**
     * Produces results for requests rejected before a manager could be resolved.
     */
    private readonly verificationResultFactory = new HmacVerificationResultFactory();

    /**
     * Initializes the factory with a collection of HmacPolicy objects.
     * @param policies - Array of HmacPolicy objects to manage and retrieve.
     * @param isConsolidatedHeadersEnabled - `true` if header consolidation is enabled otherwise `false`.
     * @param nonceStore - Where used nonces are recorded when verifying. Defaults to an
     * in-process store, which is per-process: supply a shared one — Redis or similar —
     * when more than one replica verifies for the same policy, or a replay landing on a
     * different instance than the original goes undetected.
     */
    constructor(
        policies: HmacPolicy[],
        isConsolidatedHeadersEnabled: boolean = false,
        nonceStore?: NonceStore
    ) {
        this.policies = new HmacPolicyCollection(policies);
        this.headerBuilderFactory = new HmacHeaderBuilderFactory(isConsolidatedHeadersEnabled);
        this.headerParserFactory = new HmacHeaderParserFactory(isConsolidatedHeadersEnabled);
        this.nonceStore = nonceStore ?? new MemoryNonceStore(
            // The longest window any policy allows. A single shared store cannot hold a
            // per-policy TTL, and erring long is the safe direction: an entry kept longer
            // than its policy's window costs memory, while one dropped early stops
            // guarding a signature that is still valid.
            Math.max(30, ...policies.map(policy => policy.maxAgeInSeconds ?? 30)));
    }

    /**
     * Creates and returns an HmacManager instance based on the specified policy and scheme.
     * @param policy - Name of the policy to match.
     * @param scheme - Optional name of the scheme to match.
     * @returns An HmacManager instance if a matching policy is found; otherwise, null.
     */
    create(policy: string, scheme: string | null = null): HmacManager | null {
        // Blank means "no scheme", not "a scheme whose name is blank". The .NET factory decides this
        // with IsNullOrWhiteSpace and this is the same rule, so `create(policy, cfg.scheme)` behaves
        // the same in both when cfg.scheme is absent — which for a value read from configuration or
        // a form is far more often "" than null.
        //
        // != rather than !==: it catches undefined as well as null. The default parameter already
        // maps an explicit undefined to null for TypeScript callers, but this is a published
        // JavaScript package and nothing stops a caller passing one.
        //
        // Only an all-blank name counts as absent; the name itself is not trimmed, so " S " stays a
        // miss here exactly as it does in .NET.
        const isSchemeRequested = scheme != null && scheme.trim().length > 0;

        const [matchingPolicy, matchingScheme] = this.policies.get(
            policy, isSchemeRequested ? scheme : null);

        if (!matchingPolicy) {
            return null;
        }

        // A scheme that was asked for and did not resolve is a mistake, not a
        // request to sign without one. Previously only the policy was checked,
        // so a misspelled scheme name produced a working manager whose scheme
        // was undefined: it signed with no Hmac-Scheme header and with none of
        // the scheme's header values in the signing content, reported success,
        // and every request was then rejected server-side as a signature
        // mismatch — the least informative symptom available for a typo. It
        // also silently dropped the property the scheme exists to provide,
        // that those headers are covered by the signature.
        //
        // Guarded on isSchemeRequested so passing no scheme keeps working; the
        // rejection is specifically "asked for one and it does not exist".
        if (isSchemeRequested && !matchingScheme) {
            return null;
        }

        return new HmacManager(matchingPolicy, matchingScheme,
            this.headerBuilderFactory.create(), {
                headerParserFactory: this.headerParserFactory,
                nonceStore: this.nonceStore
            });
    }

    /**
     * Verifies an incoming request against whichever policy it says it was signed with.
     *
     * This is the entry point a server wants. A verifier does not know which policy a
     * caller used until it reads the request, so resolving the manager is part of
     * verifying rather than something to do beforehand — the same job
     * `HmacAuthenticationContextProvider` does on the .NET side.
     *
     * A request naming a policy or scheme that is not registered fails with
     * `policy-not-found` rather than throwing. .NET throws here and its handler flattens
     * the exception into a rejection; there is no reason to make a caller do that
     * flattening themselves when the answer is the same either way.
     *
     * @param request - The incoming request, unmodified.
     * @returns Whether it verified, and if not, why.
     */
    verify = async (request: Request): Promise<HmacVerificationResult> => {
        let policy: string;
        let scheme: string | null;

        try {
            const parser = this.headerParserFactory.create(request.headers);
            policy = parser.getPolicy();
            scheme = parser.getScheme();
        } catch (error: unknown) {
            // Only enough of the request is parsed here to find the manager; everything
            // else is the manager's own business, and parsing it twice would mean two
            // places that could disagree about what the request says.
            return this.verificationResultFactory.failure(
                error instanceof BadHeaderFormatError ? "headers-malformed" : "headers-missing",
                error);
        }

        const manager = this.create(policy, scheme);
        if (manager === null) {
            return this.verificationResultFactory.failure("policy-not-found");
        }

        return manager.verify(request);
    }
}