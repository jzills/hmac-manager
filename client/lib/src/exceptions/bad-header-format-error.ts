/**
 * Thrown when a header required to verify a request is present but malformed — an
 * `Authorization` value that is not `Hmac <signature>`, a nonce that is not a UUID, a
 * date that is not an integer.
 *
 * Mirrors `BadHeaderFormatException` on the .NET side. Kept distinct from
 * {@link MissingHeaderError} for the same reason .NET keeps them distinct: "you did
 * not send it" and "you sent something unusable" have different fixes.
 */
export default class BadHeaderFormatError extends Error {
    constructor(message: string = "An expected header was found but the format is incorrect.") {
        super(message);
        this.name = "BadHeaderFormatError";
    }
}
