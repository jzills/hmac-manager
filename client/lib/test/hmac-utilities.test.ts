import { assert, test } from "vitest";
import { computeContentHash, timingSafeEqual } from "../src/utilities/hmac-utilities";
import HashAlgorithm from "../src/hash-algorithm";

// computeContentHash used to take a single read() off the body stream, so a
// body arriving in more than one chunk was hashed from its first chunk alone.
// Nothing in the suite sent a body at all, which is why it went unnoticed.

const encoder = new TextEncoder();

/** A stream that yields `parts` as separate chunks, in order. */
const streamOf = (...parts: string[]): ReadableStream<Uint8Array> =>
    new ReadableStream({
        start(controller) {
            for (const part of parts) {
                controller.enqueue(encoder.encode(part));
            }
            controller.close();
        }
    });

/** The expected digest, computed straight from the concatenated bytes. */
const digestOf = async (content: string) => {
    const value = await crypto.subtle.digest(HashAlgorithm.SHA256, encoder.encode(content));
    return btoa(String.fromCharCode(...new Uint8Array(value)));
};

test("a multi-chunk body is hashed in full", async () => {
    const hash = await computeContentHash(
        streamOf("part-one:", "part-two:", "part-three"),
        HashAlgorithm.SHA256
    );

    assert.equal(hash, await digestOf("part-one:part-two:part-three"));
});

test("chunking does not change the hash", async () => {
    // The same bytes, delivered one chunk versus three. A hash that depends on
    // how the runtime happened to chunk the body is the bug.
    const whole = await computeContentHash(
        streamOf("part-one:part-two:part-three"),
        HashAlgorithm.SHA256
    );
    const split = await computeContentHash(
        streamOf("part-one:", "part-two:", "part-three"),
        HashAlgorithm.SHA256
    );

    assert.equal(split, whole);
});

test("a single-chunk body still hashes correctly", async () => {
    const hash = await computeContentHash(
        streamOf('{"sku":"ABC-1","quantity":2}'),
        HashAlgorithm.SHA256
    );

    assert.equal(hash, await digestOf('{"sku":"ABC-1","quantity":2}'));
});

test("a large body crossing chunk boundaries is hashed in full", async () => {
    const parts = Array.from({ length: 64 }, (_, i) => "x".repeat(1024) + i);
    const hash = await computeContentHash(streamOf(...parts), HashAlgorithm.SHA256);

    assert.equal(hash, await digestOf(parts.join("")));
});

test("no body means no content hash", async () => {
    assert.isNull(await computeContentHash(null, HashAlgorithm.SHA256));
});

test("an empty body means no content hash", async () => {
    // Distinct from null: a stream that closes without ever yielding bytes.
    // The verifier omits the content-hash segment for a request with no body,
    // so this must not become the hash of nothing.
    assert.isNull(await computeContentHash(streamOf(), HashAlgorithm.SHA256));
});

// timingSafeEqual replaces === on the verification path, so it has to agree with === on
// every input while taking the same time whatever the answer. Timing itself is not
// asserted — a wall-clock measurement in a JIT-compiled runtime is noise — so what is
// checked here is the correctness half, and the implementation is short enough to read
// for the other half.

test("timingSafeEqual accepts identical strings", () => {
    assert.isTrue(timingSafeEqual("hUY6fD+lVmqPZ8bTQ==", "hUY6fD+lVmqPZ8bTQ=="));
});

test("timingSafeEqual rejects a difference in the last character", () => {
    assert.isFalse(timingSafeEqual("hUY6fD+lVmqPZ8bTQ==", "hUY6fD+lVmqPZ8bTQ=A"));
});

test("timingSafeEqual rejects a difference in the first character", () => {
    // The case === would answer fastest, and the one an attacker measures for.
    assert.isFalse(timingSafeEqual("hUY6fD+lVmqPZ8bTQ==", "AUY6fD+lVmqPZ8bTQ=="));
});

test("timingSafeEqual rejects strings of different lengths", () => {
    assert.isFalse(timingSafeEqual("abc", "abcd"));
    assert.isFalse(timingSafeEqual("abcd", "abc"));
});

test("timingSafeEqual accepts two empty strings", () => {
    assert.isTrue(timingSafeEqual("", ""));
});

test("timingSafeEqual rejects an empty string against a non-empty one", () => {
    assert.isFalse(timingSafeEqual("", "a"));
});
