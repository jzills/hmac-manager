import SignatureBuilder from "../builders/signature-builder"
import SigningContentBuilder from "../builders/signing-content-builder"
import HashAlgorithm from "../hash-algorithm";
import HmacSignature from "./hmac-signature";
import { computeContentHash } from "../utilities/hmac-utilities";

/**
 * The canonical 8-4-4-4-12 hexadecimal GUID form, matched case-insensitively.
 *
 * Deliberately not shared with `HmacHeaderParser.NoncePattern`, which happens to have the
 * same shape but a different job: that one rejects a value outright, this one decides
 * whether a value is one we know how to render.
 */
const GuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * Renders a public key the way .NET puts it into the signing content.
 *
 * `KeyCredentials.PublicKey` is a `Guid` there, so `SigningContentBuilder` emits
 * `Guid.ToString()` — the canonical lowercase form, whatever case the key was configured
 * in. Here the key is a string and went into the signing content verbatim, so a policy
 * configured with an uppercase GUID — which is what SQL Server's `NEWID()`, PowerShell
 * and the Azure portal all hand you — signed a different string than .NET built. Every
 * request under that policy came back 401 as a signature mismatch, and nothing local
 * could show it: two copies of this client agree with each other perfectly.
 *
 * Only a canonical GUID is touched. A key of any other shape passes through unchanged
 * rather than being blanket-lowercased, because there is no .NET rendering for it to
 * agree with — `Guid.Parse` rejects it — and lowercasing it would silently change a
 * signature that two copies of this client are already agreeing on.
 */
const normalizePublicKey = (publicKey: string): string =>
    GuidPattern.test(publicKey) ? publicKey.toLowerCase() : publicKey;

/**
 * Provides functionality to create an HMAC signature for request authentication.
 */
export default class HmacSignatureProvider {
    /**
     * The public key used in the HMAC signing process, in the form it goes on the wire.
     * See {@link normalizePublicKey}.
     */
    private readonly publicKey: string;

    /** 
     * The private key used for signing the content in the HMAC process.
     */
    private readonly privateKey: string;

    /** 
     * A list of headers that are included in the HMAC signature.
     * Each string in the array represents a header that has been included.
     */
    private readonly signedHeaders: string[] = [];

    /** 
     * The hashing algorithm used to compute the content hash. 
     * Default is SHA-256.
     */
    private readonly contentHashAlgorithm: HashAlgorithm = HashAlgorithm.SHA256;

    /** 
     * The hashing algorithm used to compute the signature hash. 
     * Default is SHA-256.
     */
    private readonly signatureHashAlgorithm: HashAlgorithm = HashAlgorithm.SHA256;

    private readonly signingContentBuilder: SigningContentBuilder;

    /**
     * Initializes a new instance of HmacSignatureProvider.
     * @param publicKey - The public key used for signature generation. A canonical GUID
     * is normalized to lowercase to match .NET; see {@link normalizePublicKey}.
     * @param privateKey - The private key used for signature generation.
     * @param signedHeaders - An array of headers to be signed. Defaults to an empty array.
     * @param contentHashAlgorithm - The algorithm used for content hashing. Defaults to "sha-256".
     * @param signatureHashAlgorithm - The algorithm used for signature hashing. Defaults to "sha-256".
     */
    constructor(
        publicKey: string,
        privateKey: string,
        signedHeaders: string[] = [],
        contentHashAlgorithm: HashAlgorithm = HashAlgorithm.SHA256,
        signatureHashAlgorithm: HashAlgorithm = HashAlgorithm.SHA256,
        signingContentBuilder: SigningContentBuilder = new SigningContentBuilder()
    ) {
        this.publicKey = normalizePublicKey(publicKey);
        this.privateKey = privateKey;
        this.signedHeaders = signedHeaders;
        this.contentHashAlgorithm = contentHashAlgorithm;
        this.signatureHashAlgorithm = signatureHashAlgorithm;
        this.signingContentBuilder = signingContentBuilder;
    }

    /**
     * Computes the HMAC signature for a given request, using the specified date and nonce.
     * @param request - The request to be signed.
     * @param dateRequested - The timestamp of the request.
     * @param nonce - A unique identifier for the request.
     * @returns An object containing the signing content and generated signature.
     */
    compute = async (request: Request, dateRequested: Date, nonce: string): Promise<HmacSignature> => {
        // Clone the request since we need to read the body if it exists
        const { body } = request.clone();
        const contentHash = await computeContentHash(body, this.contentHashAlgorithm);
        const signingContentBuilder = this.signingContentBuilder.createBuilder()
            .withRequest(request)
            .withPublicKey(this.publicKey)
            .withDateRequested(dateRequested)
            .withContentHash(contentHash)
            .withSignedHeaders(this.signedHeaders)
            .withNonce(nonce);

        const signingContent = await signingContentBuilder.build();
        const signatureBuilder = new SignatureBuilder(this.privateKey,
            signingContent,
            this.signatureHashAlgorithm
        );

        return {
            signingContent,
            signature: await signatureBuilder.build()
        };
    }
}