import HmacHeaderParserFactory from "../parsers/hmac-header-parser-factory";
import NonceStore from "../caching/nonce-store";

/**
 * What an `HmacManager` needs in order to verify, over and above what it needs to sign.
 *
 * Optional as a whole: a manager built without it can still sign, and will still verify
 * using the defaults below. Grouped into one object rather than added as three more
 * positional parameters so the published constructor keeps working unchanged.
 */
type HmacVerificationOptions = {
    /**
     * Chooses the parser for the header layout the signer used.
     *
     * Defaults to the individual `Hmac-*` headers.
     */
    headerParserFactory?: HmacHeaderParserFactory;

    /**
     * Where used nonces are recorded.
     *
     * Defaults to a fresh in-process store. **Share one instance across every manager
     * that verifies for the same policy** — `HmacManagerFactory` does this for you.
     * Handing each manager its own store means a replay is compared only against the
     * nonces that one manager happened to see, which for a per-request manager is none
     * of them.
     */
    nonceStore?: NonceStore;
};

export default HmacVerificationOptions;
