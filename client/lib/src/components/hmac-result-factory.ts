import HmacResult from "./hmac-result";
import Hmac from "./hmac";

/**
 * Factory class for creating HMAC result instances.
 */
export default class HmacResultFactory {
    /**
     * Creates a successful HMAC result.
     * @param hmac - The HMAC details to include in the result.
     * @returns An HMAC result indicating success.
     */
    success = (hmac: Hmac) => this.create(true, hmac);

    /**
     * Creates a failed HMAC result.
     * @param error - What caused the failure, so the caller can find out why.
     * @returns An HMAC result indicating failure.
     */
    failure = (error?: unknown) => this.create(false, null, error);

    /**
     * Creates an HMAC result instance.
     * @param isSuccess - Indicates whether the result is successful.
     * @param hmac - The HMAC details, or null if the result is a failure.
     * @param error - The cause of a failure, omitted on success.
     * @returns An HMAC result object.
     */
    private create = (
        isSuccess: boolean,
        hmac: Hmac | null = null,
        error?: unknown
    ): HmacResult => ({
        hmac,
        isSuccess,
        dateGenerated: new Date(),
        // Spread rather than always setting the key: a successful result
        // should not carry `error: undefined`, which reads as "there was an
        // error field and it was empty" to anything enumerating the object.
        ...(error === undefined ? {} : { error })
    });
}