import HmacManager from "./hmac-manager";
import HmacPolicy from "./components/hmac-policy";
import HmacPolicyCollection from "./components/hmac-policy-collection";
import HmacHeaderBuilderFactory from "./builders/hmac-header-builder-factory";

/**
 * A factory class for creating instances of HmacManager based on
 * specified policies and schemes.
 */
export default class HmacManagerFactory {
    /**
     * A collection of HMAC policies used for authentication.
     */
    private readonly policies: HmacPolicyCollection;

    /**
     * Factory responsible for creating instances of HMAC header builders.
     */
    private readonly headerBuilderFactory: HmacHeaderBuilderFactory;

    /**
     * Initializes the factory with a collection of HmacPolicy objects.
     * @param policies - Array of HmacPolicy objects to manage and retrieve.
     * @param isConsolidatedHeadersEnabled - `true` if header consolidation is enabled otherwise `false`.
     */
    constructor(policies: HmacPolicy[], isConsolidatedHeadersEnabled: boolean = false) {
        this.policies = new HmacPolicyCollection(policies);
        this.headerBuilderFactory = new HmacHeaderBuilderFactory(isConsolidatedHeadersEnabled);
    }

    /**
     * Creates and returns an HmacManager instance based on the specified policy and scheme.
     * @param policy - Name of the policy to match.
     * @param scheme - Optional name of the scheme to match.
     * @returns An HmacManager instance if a matching policy is found; otherwise, null.
     */
    create(policy: string, scheme: string | null = null): HmacManager | null {
        // Blank means "no scheme", not "a scheme whose name is blank". The .NET factory decides this
        // with IsNullOrWhiteSpace and this is the same rule, so `create(policy, cfg.scheme)` behaves
        // the same in both when cfg.scheme is absent — which for a value read from configuration or
        // a form is far more often "" than null.
        //
        // != rather than !==: it catches undefined as well as null. The default parameter already
        // maps an explicit undefined to null for TypeScript callers, but this is a published
        // JavaScript package and nothing stops a caller passing one.
        //
        // Only an all-blank name counts as absent; the name itself is not trimmed, so " S " stays a
        // miss here exactly as it does in .NET.
        const isSchemeRequested = scheme != null && scheme.trim().length > 0;

        const [matchingPolicy, matchingScheme] = this.policies.get(
            policy, isSchemeRequested ? scheme : null);

        if (!matchingPolicy) {
            return null;
        }

        // A scheme that was asked for and did not resolve is a mistake, not a
        // request to sign without one. Previously only the policy was checked,
        // so a misspelled scheme name produced a working manager whose scheme
        // was undefined: it signed with no Hmac-Scheme header and with none of
        // the scheme's header values in the signing content, reported success,
        // and every request was then rejected server-side as a signature
        // mismatch — the least informative symptom available for a typo. It
        // also silently dropped the property the scheme exists to provide,
        // that those headers are covered by the signature.
        //
        // Guarded on isSchemeRequested so passing no scheme keeps working; the
        // rejection is specifically "asked for one and it does not exist".
        if (isSchemeRequested && !matchingScheme) {
            return null;
        }

        return new HmacManager(matchingPolicy, matchingScheme,
            this.headerBuilderFactory.create());
    }
}