import Hmac from "./hmac";
import HmacVerificationFailureReason from "./hmac-verification-failure-reason";

/**
 * The outcome of verifying an incoming request.
 *
 * Deliberately a separate type from `HmacResult` rather than a widening of it. The two
 * carry different things: a signing result's `hmac` is what was *produced*, while a
 * verification result's is what the verifier *recomputed* and found to match. And a
 * signing failure has one shape — something threw — where a verification failure has
 * seven, only one of which is a fault rather than a rejected caller.
 */
type HmacVerificationResult = {
    /** Whether the request verified. */
    isSuccess: boolean;

    /**
     * The recomputed HMAC on success, otherwise null.
     *
     * Its `signature` is the verifier's own, not the caller's — on success they are
     * equal by definition, and on failure the caller's is not something to hand onward
     * as though it were verified.
     */
    hmac: Hmac | null;

    /** When the result was produced. */
    dateGenerated: Date;

    /** Why verification failed, present only when `isSuccess` is `false`. */
    reason?: HmacVerificationFailureReason;

    /**
     * The underlying cause, when there was a thrown one — a malformed header, an
     * unusable key, `crypto.subtle` being unavailable outside a secure context.
     *
     * Typed `unknown` because a `catch` binding is: anything can be thrown in
     * JavaScript, and narrowing it here would be a claim this cannot make.
     */
    error?: unknown;

    /**
     * The scheme header values the signature covered, by header name, present only on
     * success.
     *
     * These are the request's own claims about itself that the caller committed to when
     * they signed — a tenant id, a user id — and because they are inside the signature
     * they cannot be altered in transit. The .NET handler turns exactly these into
     * `Claim`s; a Node service can do the same with whatever its framework calls a
     * principal.
     *
     * Empty when the request was signed without a scheme.
     */
    headerValues?: Record<string, string>;
};

export default HmacVerificationResult;
