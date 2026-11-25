using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace HmacManager.Diagnostics;

/// <summary>
/// The name of the <see cref="Meter"/> HmacManager records its metrics on.
/// </summary>
/// <remarks>
/// Pass it to an OpenTelemetry <c>MeterProviderBuilder.AddMeter</c>, or match it in a
/// <see cref="MeterListener"/>, to collect them. Nothing is recorded anywhere until something
/// listens: an instrument with no listener costs one enabled check per measurement.
/// </remarks>
public static class HmacManagerDiagnostics
{
    /// <summary>
    /// The name of HmacManager's <see cref="Meter"/>: <c>HmacManager</c>.
    /// </summary>
    public const string MeterName = "HmacManager";
}

/// <summary>
/// The complete catalogue of metrics HmacManager records.
/// </summary>
/// <remarks>
///     <para>
///         Kept in one place for the same reason as <see cref="HmacLog"/>: the instrument names and
///         their tags are a contract with every dashboard and alert built on them, and the rule
///         that matters most is only checkable when it is all in view — <b>no tag carries a value a
///         caller chose</b>. A policy or scheme name is a tag only once it has resolved to one this
///         process has configured, so its cardinality is bounded by configuration. A name a caller
///         sent that did not resolve is never a tag: an edge deployment must not be drivable to
///         unbounded series by request headers, as it must not be drivable to unbounded
///         <c>Warning</c> volume. Nor is anything secret, or per request (a nonce, a signature).
///     </para>
///     <para>
///         Names follow OpenTelemetry's conventions; a Prometheus exporter renders
///         <c>hmac_manager.verifications</c> as <c>hmac_manager_verifications_total</c> and the
///         duration histogram as <c>hmac_manager_verification_duration_seconds</c>. They are
///         published in <c>site/content/docs/reference/metrics.md</c>; change that page with them.
///     </para>
/// </remarks>
internal static class HmacMetrics
{
    private static readonly Meter Meter = new(
        HmacManagerDiagnostics.MeterName,
        typeof(HmacMetrics).Assembly.GetName().Version?.ToString());

    /// <summary>The tag names. Part of the contract, like the instrument names.</summary>
    internal static class Tags
    {
        public const string Policy = "hmac_manager.policy";
        public const string Scheme = "hmac_manager.scheme";
        public const string Outcome = "hmac_manager.outcome";
    }

    /// <summary>
    /// Every value <see cref="Tags.Outcome"/> takes on a verification. A closed set: dashboards
    /// enumerate it, so add to it deliberately and document the addition.
    /// </summary>
    internal static class VerificationOutcome
    {
        /// <summary>Verified, and the nonce claimed.</summary>
        public const string Verified = "verified";

        /// <summary>
        /// The HMAC headers are absent or did not parse. Tagged with the policy only when counted
        /// by that policy's own manager; refused before one was resolved, it has no policy tag.
        /// </summary>
        public const string InvalidHeaders = "invalid_headers";

        /// <summary>The request named a policy this process does not have. No policy tag.</summary>
        public const string UnknownPolicy = "unknown_policy";

        /// <summary>The policy exists but does not declare the scheme the request named.</summary>
        public const string UnknownScheme = "unknown_scheme";

        /// <summary><c>HmacEvents.OnValidateKeys</c> refused the policy's keys.</summary>
        public const string KeysRejected = "keys_rejected";

        /// <summary>A header the request's scheme signs is absent from the request.</summary>
        public const string HeadersMissing = "headers_missing";

        /// <summary>Outside the policy's <c>MaxAgeInSeconds</c> window.</summary>
        public const string Expired = "expired";

        /// <summary>The recomputed signature differs. A forgery, a wrong key, or a client bug.</summary>
        public const string SignatureMismatch = "signature_mismatch";

        /// <summary>The nonce was already claimed.</summary>
        public const string Replayed = "replayed";

        /// <summary>Verification threw — a nonce cache fault, typically. The request was refused.</summary>
        public const string Error = "error";
    }

    /// <summary>Every value <see cref="Tags.Outcome"/> takes on a signing.</summary>
    internal static class SigningOutcome
    {
        public const string Signed = "signed";
        public const string Failed = "failed";
    }

    private static readonly Counter<long> Verifications = Meter.CreateCounter<long>(
        "hmac_manager.verifications",
        unit: "{request}",
        description: "Requests HmacManager verified or refused, by outcome.");

    private static readonly Histogram<double> VerificationDuration = Meter.CreateHistogram<double>(
        "hmac_manager.verification.duration",
        unit: "s",
        description: "Time HmacManager took to verify a request, nonce claim included.");

    private static readonly Counter<long> Signings = Meter.CreateCounter<long>(
        "hmac_manager.signings",
        unit: "{request}",
        description: "Requests HmacManager signed, by outcome.");

    /// <summary>
    /// Records a verification that reached a policy's <see cref="Components.HmacManager"/>, with how
    /// long it took. <paramref name="policy"/> and <paramref name="scheme"/> are the manager's own,
    /// so they are configured names, never a caller's.
    /// </summary>
    public static void VerificationCompleted(string policy, string? scheme, string outcome, TimeSpan duration)
    {
        var tags = PolicyTags(policy, scheme, outcome);
        Verifications.Add(1, tags);
        VerificationDuration.Record(duration.TotalSeconds, tags);
    }

    /// <summary>
    /// Records a request refused before any policy's manager was reached. Takes only a policy that
    /// has resolved — <paramref name="policy"/> is <see langword="null"/> whenever the request's
    /// policy name is unknown, because that name is the caller's.
    /// </summary>
    public static void VerificationRefused(string outcome, string? policy = null)
    {
        if (policy is null)
        {
            Verifications.Add(1, new KeyValuePair<string, object?>(Tags.Outcome, outcome));
        }
        else
        {
            Verifications.Add(1, PolicyTags(policy, scheme: null, outcome));
        }
    }

    /// <summary>Records a signing attempt by a policy's manager.</summary>
    public static void SigningCompleted(string policy, string? scheme, string outcome) =>
        Signings.Add(1, PolicyTags(policy, scheme, outcome));

    private static TagList PolicyTags(string policy, string? scheme, string outcome)
    {
        var tags = new TagList
        {
            { Tags.Policy, policy },
            { Tags.Outcome, outcome }
        };

        if (scheme is not null)
        {
            tags.Add(Tags.Scheme, scheme);
        }

        return tags;
    }
}
