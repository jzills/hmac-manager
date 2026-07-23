using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using HmacManager.Diagnostics;
using HmacManager.Policies;

namespace HmacManager.Mvc.Extensions.Internal;

/// <summary>
/// Watches an <see cref="IConfigurationSection"/> for changes and atomically republishes a
/// <see cref="ReloadableHmacPolicyCollection"/>, so edits to the underlying configuration
/// (e.g. a rotated key delivered via a reloadable mounted ConfigMap/Secret) take effect
/// without restarting the host process.
/// </summary>
internal sealed class HmacPolicyCollectionReloader : IHostedService
{
    private readonly IConfigurationSection ConfigurationSection;
    private readonly ReloadableHmacPolicyCollection Collection;

    // Written once by the thread that starts the host (see UseLogger) and read by the configuration
    // reload thread. Volatile for the same reason ReloadableHmacPolicyCollection's inner reference
    // is: a stale read here would only cost a log line, but the field crossing threads is the point
    // worth stating in the declaration rather than in a comment somewhere else.
    private volatile ILogger Logger = NullLogger.Instance;

    /// <summary>
    /// Creates an <see cref="HmacPolicyCollectionReloader"/> and immediately subscribes
    /// to configuration reload notifications for the lifetime of the process.
    /// </summary>
    /// <param name="configurationSection">The <see cref="IConfigurationSection"/> policies are bound from.</param>
    /// <param name="collection">The <see cref="ReloadableHmacPolicyCollection"/> to republish on each change.</param>
    public HmacPolicyCollectionReloader(IConfigurationSection configurationSection, ReloadableHmacPolicyCollection collection)
    {
        ConfigurationSection = configurationSection;
        Collection = collection;

        ChangeToken.OnChange(ConfigurationSection.GetReloadToken, Reload);
    }

    /// <summary>
    /// Attaches the host's logger, replacing the <see cref="NullLogger"/> this reloader starts with.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to record reload outcomes to.</param>
    /// <returns>This reloader, so it can be returned directly from a service factory.</returns>
    /// <remarks>
    /// The reloader is constructed during service registration so that a configuration change is
    /// never missed between <c>AddHmacManager</c> and the first request — which is before any
    /// <see cref="IServiceProvider"/> exists to resolve a logger from. Registering it as an
    /// <see cref="IHostedService"/> gives the container a chance to hand over the real logger before
    /// the host starts serving.
    /// </remarks>
    internal HmacPolicyCollectionReloader UseLogger(ILogger logger)
    {
        Logger = logger;
        return this;
    }

    /// <summary>
    /// Announces, once, that policies are being watched and which ones are live. Without this an
    /// operator has no way to tell a host that hot-reloads its policies from one that pinned them
    /// at startup.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var policies = Collection.GetAll();
        HmacLog.PolicyReloadWatchStarted(Logger, policies.Count, Describe(policies));

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Rebuilds the policy set from the current configuration and atomically publishes it,
    /// replacing the previously served set in a single reference swap.
    /// </summary>
    /// <remarks>
    /// Configuration that fails to build a valid policy set is ignored and the previous,
    /// still-valid set is left in place until the next change notification. This happens
    /// transiently when public policy fields and private keys arrive on independently-updated
    /// mounted volumes (ConfigMap vs. Secret): a rotation can be observed after config.json
    /// updates but before the matching private key file syncs, at which point binding throws
    /// a validation error. Building the new set fully before publishing means a failed reload
    /// never leaves a partially-updated collection, and swallowing the error here keeps it off
    /// the configuration reload thread — the log is the only trace it leaves, which is precisely
    /// why it is logged rather than silently dropped.
    /// </remarks>
    internal void Reload()
    {
        HmacPolicyCollection updated;
        try
        {
            updated = ConfigurationSection.GetPolicySection();
        }
        catch (Exception exception)
        {
            HmacLog.PolicyReloadFailed(Logger, Collection.GetAll().Count, exception);
            return;
        }

        Collection.Replace(updated);

        var policies = Collection.GetAll();
        HmacLog.PolicyReloadSucceeded(Logger, policies.Count, Describe(policies));
    }

    /// <summary>
    /// Renders the policy names for a log message. Names only — a policy carries its private key.
    /// </summary>
    private static string Describe(IReadOnlyCollection<HmacPolicy> policies) =>
        policies.Count > 0 ? string.Join(", ", policies.Select(policy => policy.Name)) : "(none)";
}
