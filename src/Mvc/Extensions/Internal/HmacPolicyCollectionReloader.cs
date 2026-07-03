using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using HmacManager.Policies;

namespace HmacManager.Mvc.Extensions.Internal;

/// <summary>
/// Watches an <see cref="IConfigurationSection"/> for changes and atomically republishes a
/// <see cref="ReloadableHmacPolicyCollection"/>, so edits to the underlying configuration
/// (e.g. a rotated key delivered via a reloadable mounted ConfigMap/Secret) take effect
/// without restarting the host process.
/// </summary>
internal sealed class HmacPolicyCollectionReloader
{
    private readonly IConfigurationSection ConfigurationSection;
    private readonly ReloadableHmacPolicyCollection Collection;

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
    /// the configuration reload thread.
    /// </remarks>
    internal void Reload()
    {
        HmacPolicyCollection updated;
        try
        {
            updated = ConfigurationSection.GetPolicySection();
        }
        catch (Exception)
        {
            return;
        }

        Collection.Replace(updated);
    }
}
