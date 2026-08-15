import HmacHeaderParser from "./hmac-header-parser";
import { HmacAuthenticationDefaults } from "../hmac-authentication-defaults";
import MissingHeaderError from "../exceptions/missing-header-error";
import BadHeaderFormatError from "../exceptions/bad-header-format-error";

/**
 * Reads HMAC details out of the single consolidated `Hmac-Options` header.
 *
 * The inverse of `HmacOptionsHeaderBuilder`, and the counterpart to
 * `HmacOptionsHeaderParser` on the .NET side. The builder packs the individual header
 * values into one base64-encoded `key=value&key=value` string; this unpacks it, and
 * every rule about what those values must contain is inherited unchanged from
 * {@link HmacHeaderParser}.
 *
 * The signature is read from inside the blob rather than from the `Authorization`
 * header the builder also emits. Both carry it, and .NET reads the blob — so a request
 * whose two copies disagree is resolved the same way by both implementations.
 */
export default class HmacOptionsHeaderParser extends HmacHeaderParser {
    /**
     * The values unpacked from the `Hmac-Options` header.
     */
    private readonly options: Map<string, string>;

    constructor(headers: Headers) {
        super(headers);
        this.options = HmacOptionsHeaderParser.decode(headers);
    }

    createParser = (headers: Headers): HmacHeaderParser => new HmacOptionsHeaderParser(headers);

    /**
     * Reads from the unpacked options rather than from the request's headers.
     *
     * Nothing falls back to a real header of the same name. In consolidated mode the
     * individual `Hmac-*` headers are not sent at all, so a value found under one of
     * those names came from somewhere other than the signer — and honouring it would
     * mean the verifier read a policy or nonce the `Hmac-Options` blob does not agree
     * with. .NET replaces the dictionary outright for the same reason.
     */
    protected override getHeader(name: string): string | null {
        return this.options.get(name) ?? null;
    }

    /**
     * Unpacks the `Hmac-Options` header into its constituent values.
     */
    private static decode(headers: Headers): Map<string, string> {
        const options = headers.get(HmacAuthenticationDefaults.Headers.Options);
        if (options === null) {
            throw new MissingHeaderError(
                `The "${HmacAuthenticationDefaults.Headers.Options}" header is missing.`);
        }

        if (options.trim().length === 0) {
            throw new BadHeaderFormatError(
                `The "${HmacAuthenticationDefaults.Headers.Options}" header is blank.`);
        }

        let decoded: string;
        try {
            decoded = atob(options);
        } catch {
            // atob throws InvalidCharacterError on anything that is not valid base64.
            // Rethrown as a header error so a caller sees the same failure shape for
            // "unusable header" however the header was unusable.
            throw new BadHeaderFormatError(
                `The "${HmacAuthenticationDefaults.Headers.Options}" header is not valid base64.`);
        }

        // Prefix-matched against the known names rather than split on "=", because the
        // values contain "=" themselves: a base64 signature is padded with it. Splitting
        // would truncate every signature that happened to need padding — a size-dependent
        // failure, and therefore one that hides until it does not.
        const names = [
            HmacAuthenticationDefaults.Headers.Authorization,
            HmacAuthenticationDefaults.Headers.Policy,
            HmacAuthenticationDefaults.Headers.Scheme,
            HmacAuthenticationDefaults.Headers.Nonce,
            HmacAuthenticationDefaults.Headers.DateRequested
        ];

        const values = new Map<string, string>();
        for (const pair of decoded.split("&")) {
            for (const name of names) {
                const prefix = `${name}=`;
                if (pair.startsWith(prefix)) {
                    values.set(name, pair.substring(prefix.length));
                    break;
                }
            }
        }

        return values;
    }
}
