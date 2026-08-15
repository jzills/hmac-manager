import HmacHeaderParser from "./hmac-header-parser";
import HmacOptionsHeaderParser from "./hmac-options-header-parser";

/**
 * Creates the parser matching how the signer laid its headers out.
 *
 * The mirror of `HmacHeaderBuilderFactory` on the signing side, and it takes the same
 * flag: consolidated headers are a property of the deployment, and the two ends must
 * agree. A verifier configured one way against a signer configured the other rejects
 * every request, which is the intended outcome — the alternative, sniffing for an
 * `Hmac-Options` header and accepting either layout, would let a deployment drift out
 * of the configuration it believes it has and never find out.
 */
export default class HmacHeaderParserFactory {
    /**
     * Whether the four `Hmac-*` headers arrive packed into one `Hmac-Options` header.
     */
    private readonly isConsolidatedHeadersEnabled: boolean;

    constructor(isConsolidatedHeadersEnabled: boolean) {
        this.isConsolidatedHeadersEnabled = isConsolidatedHeadersEnabled;
    }

    create = (headers: Headers): HmacHeaderParser =>
        this.isConsolidatedHeadersEnabled ?
            new HmacOptionsHeaderParser(headers) :
            new HmacHeaderParser(headers);
}
