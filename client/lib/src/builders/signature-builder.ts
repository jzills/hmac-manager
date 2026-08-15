import Algorithm from "../components/algorithm";
import HashAlgorithm from "../hash-algorithm";
import { HmacAuthenticationDefaults } from "../hmac-authentication-defaults";
import { getKeyBytes, getSignature, getUnicode, getUtf8ByteArray } from "../utilities/hmac-utilities";

/**
 * Class responsible for building a signature using HMAC.
 */
export default class SignatureBuilder {
    /**
     * Represents the private key used for HMAC signature generation.
     */
    private readonly privateKey: string;

    /**
     * The content to be signed during the HMAC signature process.
     */
    private readonly signingContent: string;

    /**
     * The hashing algorithm used for signing, such as SHA1, SHA256, or SHA512.
     */
    private readonly algorithm: Algorithm;

    /**
     * Initializes a new instance of the SignatureBuilder.
     * @param privateKey The private key used for signing.
     * @param signingContent The content to sign.
     * @param signatureHashAlgorithm The hash algorithm to use (default is "sha-256").
     */
    constructor(
        privateKey: string,
        signingContent: string,
        signatureHashAlgorithm: string = HashAlgorithm.SHA256
    ) {
        this.privateKey = privateKey;
        this.signingContent = signingContent;
        this.algorithm = { name: HmacAuthenticationDefaults.AuthenticationScheme, hash: signatureHashAlgorithm };
    }

    /**
     * Builds the HMAC signature asynchronously.
     * @returns A promise that resolves to the base64-encoded signature.
     */
    build = async () => {
        const signatureBytes = await getSignature(
            await getKeyBytes(this.privateKey, this.algorithm),
            // UTF-8, matching Encoding.UTF8.GetBytes on the .NET side. The two agree on
            // ASCII — which every part of the signing content is, except the values a
            // scheme contributes — and disagree on anything above it.
            getUtf8ByteArray(this.signingContent)
        );

        const unicode = getUnicode(signatureBytes);
        return btoa(unicode);
    }
}