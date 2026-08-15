/**
 * Converts an ArrayBuffer of bytes to a Unicode string.
 * @param signatureBytes - The ArrayBuffer containing byte data.
 * @returns A string representation in Unicode form.
 */
export const getUnicode = (signatureBytes: ArrayBuffer): string => {
    const bytes = new Uint8Array(signatureBytes);
    const bytesSplit = bytes.toString().split(",");
    const unicode = bytesSplit.map(element => String.fromCharCode(parseInt(element))).join("");
    return unicode;
}

/**
 * Computes a base64-encoded content hash from a given ReadableStream and algorithm.
 * @param body - The ReadableStream containing data to hash.
 * @param algorithm - The hashing algorithm to use.
 * @returns A base64 string of the hash if body exists; otherwise, null.
 */
export const computeContentHash = async (body: ReadableStream<Uint8Array> | null, algorithm: AlgorithmIdentifier) => {
    if (!body) {
        return null;
    }

    // Drained to completion. A ReadableStream makes no guarantee that one
    // read() returns the whole body, and this previously took exactly one:
    // anything arriving in more than one chunk was hashed from its first chunk
    // alone. The server hashes everything it received, so the two signing
    // content strings differed and the request was rejected as a signature
    // mismatch — size-dependent, and therefore invisible until a payload
    // happened to cross a chunk boundary.
    const reader = body.getReader();
    const chunks: Uint8Array[] = [];
    let length = 0;

    for (;;) {
        const { done, value } = await reader.read();
        if (done) {
            break;
        }

        if (value) {
            chunks.push(value);
            length += value.length;
        }
    }

    // No bytes means no content-hash segment in the signing content at all,
    // which is what the original returned for an empty body and what the
    // verifier expects for a request without one.
    if (length === 0) {
        return null;
    }

    const content = new Uint8Array(length);
    let offset = 0;
    for (const chunk of chunks) {
        content.set(chunk, offset);
        offset += chunk.length;
    }

    return btoa(getUnicode(await crypto.subtle.digest(algorithm, content)));
}

/**
 * Converts a string into a Uint8Array of byte values.
 * @param content - The string to convert.
 * @returns Uint8Array of byte values from the string.
 *
 * The buffer type is pinned to `ArrayBuffer` rather than left to the default. A bare
 * `Uint8Array` annotation means `Uint8Array<ArrayBufferLike>`, which includes
 * `SharedArrayBuffer` and so does not satisfy `BufferSource` — every `crypto.subtle`
 * call taking this value fails to typecheck. `Uint8Array.from` cannot return a shared
 * buffer, so narrowing here is a statement of fact, not an assertion.
 */
export const getByteArray = (content: string): Uint8Array<ArrayBuffer> =>
    Uint8Array.from(content, element => element.charCodeAt(0));

/**
 * Encodes a string as UTF-8 bytes.
 *
 * Distinct from {@link getByteArray}, which takes the low byte of each code unit. The
 * two agree on ASCII and diverge on everything else, and which one is correct depends
 * entirely on what the string represents:
 *
 * - The signing content is text, and .NET hashes it with `Encoding.UTF8.GetBytes`. It
 *   must go through here, or a signing string containing any non-ASCII character — a
 *   scheme header value carrying a name, most realistically — produces a different
 *   signature in each implementation, and neither can verify the other's requests.
 * - The private key is base64 that `atob` has already turned into one character per
 *   byte. Those characters run to U+00FF, so UTF-8 would re-encode half of them into
 *   two bytes and change the key. That path keeps using {@link getByteArray}.
 *
 * @param content - The string to encode.
 * @returns The UTF-8 bytes.
 */
export const getUtf8ByteArray = (content: string): Uint8Array<ArrayBuffer> =>
    new TextEncoder().encode(content);

/**
 * Converts a base64 private key string to a CryptoKey object for signing.
 * @param privateKey - Base64-encoded private key.
 * @param algorithm - The signing algorithm to use.
 * @returns A CryptoKey object for signing.
 */
export const getKeyBytes = async (privateKey: string, algorithm: Algorithm) =>
    crypto.subtle.importKey("raw",
        getByteArray(atob(privateKey)),
        algorithm,
        false,
        ["sign"]
    );

/**
 * Signs the provided content using a CryptoKey and returns the signature.
 * @param keyBytes - The CryptoKey used for signing.
 * @param signingContentBytes - The data to be signed.
 * @returns A Promise that resolves with the signature as an ArrayBuffer.
 */
export const getSignature = async (keyBytes: CryptoKey, signingContentBytes: BufferSource) =>
    crypto.subtle.sign("HMAC", keyBytes, signingContentBytes);

/**
 * Compares two strings in time that does not depend on where they first differ.
 *
 * `===` returns as soon as it finds a mismatched character, so how long it takes leaks
 * how many leading characters were right. Against a verifier an attacker can call
 * repeatedly, that turns forging a signature from guessing the whole value at once into
 * guessing it one character at a time.
 *
 * The length check up front does leak the length, which is unavoidable without hashing
 * both sides first and is not worth defending: the signature length is fixed by the
 * algorithm and already public.
 *
 * @param left - The first string.
 * @param right - The second string.
 * @returns Whether the two are equal.
 */
export const timingSafeEqual = (left: string, right: string): boolean => {
    if (left.length !== right.length) {
        return false;
    }

    // Accumulated rather than short-circuited: every character is compared whatever the
    // earlier ones did, so the loop runs the same number of times either way.
    let difference = 0;
    for (let index = 0; index < left.length; index++) {
        difference |= left.charCodeAt(index) ^ right.charCodeAt(index);
    }

    return difference === 0;
};