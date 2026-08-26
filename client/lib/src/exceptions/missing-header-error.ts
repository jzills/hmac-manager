/**
 * Thrown when a header required to verify a request is absent.
 *
 * Mirrors `MissingHeaderException` on the .NET side. It is thrown by the header
 * parsers and caught by `verify`, which reports it as a
 * `"headers-missing"` result rather than letting it escape — `verify` never throws,
 * matching `sign`.
 */
export default class MissingHeaderError extends Error {
    constructor(message: string = "One or more required headers are missing.") {
        super(message);

        // Without this the name is inherited from Error, so `error.name` reads
        // "Error" and the two header errors are indistinguishable to anything
        // logging or matching on it.
        this.name = "MissingHeaderError";
    }
}
