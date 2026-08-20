import { HmacAuthenticationDefaults } from "../hmac-authentication-defaults";
import HmacPartial from "../components/hmac-partial";
import MissingHeaderError from "../exceptions/missing-header-error";
import BadHeaderFormatError from "../exceptions/bad-header-format-error";

/**
 * Reads the HMAC details a signed request carries in its headers.
 *
 * The inverse of `HmacHeaderBuilder`, and the counterpart to `HmacHeaderParser` on the
 * .NET side. Every rule below is that class's rule; where they disagree, a request one
 * implementation accepts would be rejected by the other, which is the failure mode this
 * whole package exists to avoid.
 *
 * Reads from a Fetch `Headers`, whose lookups are case-insensitive by specification, so
 * a proxy that lowercases header names — Envoy does — needs no special handling here.
 * The .NET parser has to build an `OrdinalIgnoreCase` dictionary to get the same thing.
 */
export default class HmacHeaderParser {
    /**
     * Matches the canonical 8-4-4-4-12 hexadecimal UUID form.
     *
     * Deliberately not a variant/version-aware pattern: .NET decides this with
     * `Guid.TryParse`, which cares about the shape and not about which RFC 4122 version
     * produced it. Anything stricter here would reject nonces .NET accepts.
     */
    private static readonly NoncePattern =
        /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

    /**
     * The headers to read from.
     */
    protected readonly headers: Headers;

    constructor(headers: Headers) {
        this.headers = headers;
    }

    /**
     * Creates a parser of the same kind over a different set of headers.
     *
     * Exists so a caller holding a parser can parse another request without knowing
     * which subclass it is holding — the same role `createBuilder` plays on the builders.
     */
    createParser = (headers: Headers): HmacHeaderParser => new HmacHeaderParser(headers);

    /**
     * Looks up a single header value, or null when it is absent.
     *
     * Every read below goes through here so that a subclass can change *where* the
     * values come from without restating any of the rules about what they must contain
     * — which is exactly what the consolidated-header parser needs, since its values
     * arrive packed inside one header rather than as headers of their own.
     */
    protected getHeader(name: string): string | null {
        return this.headers.get(name);
    }

    /**
     * Reads the signature out of the `Authorization` header.
     *
     * The value must be exactly `Hmac <signature>`. A header carrying some other
     * authentication scheme is a format error rather than a missing header: the caller
     * did send an `Authorization`, it just is not this one.
     */
    getAuthorization(): string {
        const authorization = this.getHeader(HmacAuthenticationDefaults.Headers.Authorization);
        if (authorization === null) {
            throw new MissingHeaderError(
                `The "${HmacAuthenticationDefaults.Headers.Authorization}" header is missing.`);
        }

        const parts = authorization.split(" ");
        if (parts.length !== 2 || parts[0] !== HmacAuthenticationDefaults.AuthenticationScheme) {
            throw new BadHeaderFormatError(
                `The "${HmacAuthenticationDefaults.Headers.Authorization}" header must be ` +
                `"${HmacAuthenticationDefaults.AuthenticationScheme} <signature>".`);
        }

        return parts[1];
    }

    /**
     * Reads the policy name out of the `Hmac-Policy` header.
     *
     * Blank counts as malformed rather than absent, matching .NET's
     * `IsNullOrWhiteSpace` check — a policy can never be found by an empty name, so
     * accepting one only defers the failure to a less obvious place.
     */
    getPolicy(): string {
        const policy = this.getHeader(HmacAuthenticationDefaults.Headers.Policy);
        if (policy === null) {
            throw new MissingHeaderError(
                `The "${HmacAuthenticationDefaults.Headers.Policy}" header is missing.`);
        }

        if (policy.trim().length === 0) {
            throw new BadHeaderFormatError(
                `The "${HmacAuthenticationDefaults.Headers.Policy}" header is blank.`);
        }

        return policy;
    }

    /**
     * Reads the scheme name out of the `Hmac-Scheme` header, if there is one.
     *
     * Optional: a request signed without a scheme carries no such header, and that is
     * not an error.
     *
     * Blank normalises to null, the same rule `HmacManagerFactory.create` applies —
     * blank means "no scheme", not "a scheme whose name is blank". Doing it here means
     * the parsed value can be compared against a resolved scheme name directly, without
     * every consumer having to remember that `""` and `null` mean the same thing.
     */
    getScheme(): string | null {
        const scheme = this.getHeader(HmacAuthenticationDefaults.Headers.Scheme);
        return scheme !== null && scheme.trim().length > 0 ? scheme : null;
    }

    /**
     * Reads the nonce out of the `Hmac-Nonce` header.
     *
     * Required to be a UUID. .NET parses this into a `Guid`, so a nonce of any other
     * shape could never verify there; rejecting it here keeps the two implementations
     * agreeing on the same requests, and gives the nonce store a bounded, predictable
     * key rather than an arbitrary caller-supplied string.
     */
    getNonce(): string {
        const nonce = this.getHeader(HmacAuthenticationDefaults.Headers.Nonce);
        if (nonce === null) {
            throw new MissingHeaderError(
                `The "${HmacAuthenticationDefaults.Headers.Nonce}" header is missing.`);
        }

        if (!HmacHeaderParser.NoncePattern.test(nonce)) {
            throw new BadHeaderFormatError(
                `The "${HmacAuthenticationDefaults.Headers.Nonce}" header must be a UUID.`);
        }

        return nonce;
    }

    /**
     * Reads the signing time out of the `Hmac-DateRequested` header.
     *
     * The wire format is milliseconds since the Unix epoch, which is what
     * `HmacHeaderBuilder.withDateRequested` writes and what .NET reads with
     * `DateTimeOffset.FromUnixTimeMilliseconds`.
     */
    getDateRequested(): Date {
        const dateRequested = this.getHeader(HmacAuthenticationDefaults.Headers.DateRequested);
        if (dateRequested === null) {
            throw new MissingHeaderError(
                `The "${HmacAuthenticationDefaults.Headers.DateRequested}" header is missing.`);
        }

        // Number() rather than parseInt(): parseInt("123abc") is 123, so it would accept
        // a value .NET's long.TryParse rejects. Trimmed first because Number("") is 0 —
        // an empty header would otherwise parse as the epoch instead of failing.
        const milliseconds = Number(dateRequested.trim());
        if (dateRequested.trim().length === 0 || !Number.isInteger(milliseconds)) {
            throw new BadHeaderFormatError(
                `The "${HmacAuthenticationDefaults.Headers.DateRequested}" header must be ` +
                "an integer number of milliseconds since the Unix epoch.");
        }

        return new Date(milliseconds);
    }

    /**
     * Reads every HMAC header the request carries.
     *
     * @returns The details the caller asserted.
     * @throws {MissingHeaderError} A required header is absent.
     * @throws {BadHeaderFormatError} A required header is present but unusable.
     */
    parse = (): HmacPartial => ({
        signature: this.getAuthorization(),
        policy: this.getPolicy(),
        scheme: this.getScheme(),
        nonce: this.getNonce(),
        dateRequested: this.getDateRequested()
    });
}
