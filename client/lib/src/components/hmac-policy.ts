import SigningContentAccessor from "../builders/signing-content-accessor";
import HashAlgorithm from "../hash-algorithm";
import HmacScheme from "./hmac-scheme";

/**
 * Represents an HMAC policy configuration.
 */
type HmacPolicy = {
    /** The name of the HMAC policy. */
    name: string;

    /** The public key used for HMAC signing. */
    publicKey: string;

    /** The private key used for HMAC signing. */
    privateKey: string;

    /** The algorithm used to compute the content hash. */
    contentHashAlgorithm: HashAlgorithm;

    /** The algorithm used to compute the signature hash. */
    signatureHashAlgorithm: HashAlgorithm;

    /** The schemes associated with this HMAC policy. */
    schemes: HmacScheme[];

    /**
     * How long a signature made under this policy stays valid, in seconds.
     *
     * Used when verifying: a request whose `Hmac-DateRequested` is older than this, or
     * dated in the future at all, is rejected. Signing ignores it.
     *
     * Defaults to 30, matching `Nonce.MaxAgeInSeconds` on the .NET side — the two ends
     * do not have to agree, but the shorter of them is what decides, so a verifier
     * configured tighter than its signers rejects requests that were never late.
     */
    maxAgeInSeconds?: number;

    signingContentAccessor?: SigningContentAccessor;
};

export default HmacPolicy;