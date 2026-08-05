using Microsoft.Extensions.Logging;

namespace HmacManager.Operator.Diagnostics;

/// <summary>
/// The catalogue of log messages emitted by the HmacPolicy operator.
/// </summary>
/// <remarks>
///     <para>
///         The operator is the only thing standing between an <c>HmacPolicy</c> resource and the
///         ConfigMap/Secret the pods actually mount, and its reconciliation is set-based: one policy
///         changing rewrites the whole aggregate. When a policy does not take effect, the question is
///         always the same — was it seen, was it valid, and was the aggregate written? These messages
///         exist to answer exactly that, in that order.
///     </para>
///     <para>
///         Event ids: 2000–2099 reconciliation, 2100–2199 policy resolution, 2200–2299 startup.
///     </para>
/// </remarks>
internal static partial class OperatorLog
{
    // ---------------------------------------------------------------------------------------------
    // Reconciliation — 2000–2099
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Records the resource event that triggered a rebuild. Debug, because a busy namespace produces
    /// one of these per policy write.
    /// </summary>
    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Debug,
        Message = "Reconciling namespace {Namespace} after {Trigger} of HmacPolicy {Policy}.")]
    public static partial void ReconcileTriggered(ILogger logger, string @namespace, string trigger, string policy);

    /// <summary>
    /// Records the outcome of a rebuild — the one line that says whether the mounted configuration
    /// now matches the cluster's desired state.
    /// </summary>
    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Reconciled namespace {Namespace}: {ValidCount} of {TotalCount} HmacPolicies are valid and written to ConfigMap {ConfigMapName} and Secret {SecretName}.")]
    public static partial void ReconcileCompleted(
        ILogger logger, string @namespace, int validCount, int totalCount, string configMapName, string secretName);

    /// <summary>
    /// Records that a rebuild left no valid policies at all, which takes every pod mounting the
    /// aggregate from "authenticating" to "rejecting everything".
    /// </summary>
    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Warning,
        Message = "Reconciled namespace {Namespace} to an empty policy set. Every request verified against the aggregate configuration will now be rejected.")]
    public static partial void ReconciledToEmptyPolicySet(ILogger logger, string @namespace);

    // ---------------------------------------------------------------------------------------------
    // Policy resolution — 2100–2199
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Records that a policy failed validation and has been left out of the aggregate. The same
    /// message is written to the resource's status; it is logged too because a policy that never
    /// reconciles is usually noticed in the logs long before anyone reads its status.
    /// </summary>
    [LoggerMessage(
        EventId = 2100,
        Level = LogLevel.Warning,
        Message = "HmacPolicy {Policy} in namespace {Namespace} is invalid and has been excluded from the aggregate configuration: {Reason}")]
    public static partial void PolicyInvalid(ILogger logger, string policy, string @namespace, string? reason);

    /// <summary>
    /// Records that a policy names no private key Secret at all.
    /// </summary>
    [LoggerMessage(
        EventId = 2101,
        Level = LogLevel.Warning,
        Message = "HmacPolicy {Policy} in namespace {Namespace} has no usable privateKeySecretRef; both name and key are required.")]
    public static partial void PrivateKeyReferenceMissing(ILogger logger, string policy, string @namespace);

    /// <summary>
    /// Records that a policy's private key Secret exists but does not hold the referenced key —
    /// distinct from an absent reference, and almost always a typo in one of the two names.
    /// </summary>
    [LoggerMessage(
        EventId = 2102,
        Level = LogLevel.Warning,
        Message = "HmacPolicy {Policy} in namespace {Namespace} references key \"{Key}\" of Secret {SecretName}, which could not be read.")]
    public static partial void PrivateKeyNotResolved(
        ILogger logger, string policy, string @namespace, string secretName, string key);

    /// <summary>
    /// Records a status transition. Debug on its own, because the interesting transitions are
    /// already covered by <see cref="PolicyInvalid"/>.
    /// </summary>
    [LoggerMessage(
        EventId = 2103,
        Level = LogLevel.Debug,
        Message = "HmacPolicy {Policy} status set to {Phase} at generation {Generation}.")]
    public static partial void StatusUpdated(ILogger logger, string policy, string phase, long generation);

    // ---------------------------------------------------------------------------------------------
    // Startup — 2200–2299
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Records the settings the operator resolved at startup. Every "it is not picking up my policy"
    /// investigation starts by confirming the watched namespace and the aggregate resource names,
    /// which are otherwise only visible in the deployment's environment.
    /// </summary>
    [LoggerMessage(
        EventId = 2200,
        Level = LogLevel.Information,
        Message = "HmacPolicy operator starting. Watching namespace {Namespace}, writing ConfigMap {ConfigMapName} and Secret {SecretName}, rendering policies with the {NonceCacheType} nonce cache.")]
    public static partial void OperatorStarting(
        ILogger logger, string @namespace, string configMapName, string secretName, string nonceCacheType);
}
