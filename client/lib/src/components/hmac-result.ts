import Hmac from "./hmac";

/**
 * Represents the result of an HMAC signing operation.
 */
type HmacResult = {
    /** 
     * The generated HMAC details, or null if the signing operation failed.
     */
    hmac: Hmac | null;

    /** 
     * Indicates whether the signing operation was successful.
     */
    isSuccess: boolean;

    /**
     * The date and time when the HMAC was generated.
     */
    dateGenerated: Date;

    /**
     * Why signing failed, present only when `isSuccess` is `false`.
     *
     * `sign` reports failure through `isSuccess` rather than throwing, so
     * without this the reason a request went unsigned — a header a scheme
     * requires but the request does not carry, a private key that is not valid
     * base64, `crypto.subtle` being unavailable outside a secure context — is
     * unavailable to the caller and the request fails server-side instead.
     *
     * Typed `unknown` because a `catch` binding is: anything can be thrown in
     * JavaScript, and narrowing it here would be a claim this cannot make.
     */
    error?: unknown;
};

export default HmacResult;