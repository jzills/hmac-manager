/**
 * Why a request failed verification.
 *
 * A single "verification failed" would be true but useless: an expired signature, a
 * replayed nonce and a genuine signature mismatch have entirely different causes and
 * entirely different fixes. These map one-to-one onto the branches of `VerifyAsync` on
 * the .NET side, which exist for exactly that reason.
 *
 * A word on what is safe to tell the caller. The .NET library distinguishes these in
 * its *logs* and returns an undifferentiated failure over the wire, because a verifier
 * facing the open internet should not narrate why a forgery was rejected. This value is
 * for the process doing the verifying, not for the response body — a Node service
 * should log it and return a flat 401 or 403, the way `ExtAuthzHandler` does.
 */
type HmacVerificationFailureReason =
    /** A header needed to verify the request was not sent. */
    | "headers-missing"

    /** A header needed to verify the request was sent but is unusable. */
    | "headers-malformed"

    /** The request names a policy, or a scheme within one, that is not registered. */
    | "policy-not-found"

    /** The signature is outside its validity window — too old, or dated in the future. */
    | "expired"

    /** This nonce has already been used. The request is a replay. */
    | "replayed"

    /** The signature does not match the one the verifier computed. */
    | "signature-mismatch"

    /** Computing the signature to compare against threw. A server-side fault, not a rejected caller. */
    | "verification-error";

export default HmacVerificationFailureReason;
