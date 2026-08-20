import Hmac from "./hmac";
import HmacVerificationFailureReason from "./hmac-verification-failure-reason";
import HmacVerificationResult from "./hmac-verification-result";

/**
 * Creates {@link HmacVerificationResult} instances.
 *
 * The counterpart to `HmacResultFactory` on the signing side.
 */
export default class HmacVerificationResultFactory {
    /**
     * A request that verified.
     *
     * @param hmac - The HMAC the verifier recomputed and found to match.
     * @param headerValues - The scheme header values the signature covered.
     */
    success = (hmac: Hmac, headerValues: Record<string, string> = {}): HmacVerificationResult => ({
        hmac,
        isSuccess: true,
        dateGenerated: new Date(),
        headerValues
    });

    /**
     * A request that did not verify.
     *
     * @param reason - Which of the checks rejected it.
     * @param error - The underlying cause, when a check threw rather than simply failing.
     */
    failure = (
        reason: HmacVerificationFailureReason,
        error?: unknown
    ): HmacVerificationResult => ({
        hmac: null,
        isSuccess: false,
        dateGenerated: new Date(),
        reason,
        // Spread rather than always setting the key: most failures have no thrown cause,
        // and `error: undefined` reads as "there was an error field and it was empty" to
        // anything enumerating the object.
        ...(error === undefined ? {} : { error })
    });
}
