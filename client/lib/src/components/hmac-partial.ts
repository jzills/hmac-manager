/**
 * The HMAC details carried on an incoming request, as parsed from its headers.
 *
 * Partial because it is what the *caller* asserted, not what was verified: the
 * signature here is the one that arrived, and nothing has yet recomputed it. The
 * verified counterpart is an {@link Hmac}, which additionally carries the signing
 * content the verifier built for itself.
 *
 * Mirrors `HmacPartial` on the .NET side, with the signature folded in — .NET returns
 * it through an `out` parameter, which has no natural TypeScript equivalent.
 */
type HmacPartial = {
    /** The policy the caller signed with, from the `Hmac-Policy` header. */
    policy: string;

    /** The scheme the caller signed with, from `Hmac-Scheme`, or null if they used none. */
    scheme: string | null;

    /** When the caller signed, from `Hmac-DateRequested`. */
    dateRequested: Date;

    /** The caller's nonce, from `Hmac-Nonce`. Always a UUID. */
    nonce: string;

    /** The signature from the `Authorization` header, with the `Hmac ` prefix stripped. */
    signature: string;
};

export default HmacPartial;
