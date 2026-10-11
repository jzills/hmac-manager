using HmacManager.Common;
using HmacManager.Common.Extensions;

namespace HmacManager.Policies.Extensions;

/// <summary>
/// A class representing extensions for <see cref="IHmacPolicyCollection"/>.
/// </summary>
public static class IHmacPolicyCollectionExtensions
{
    /// <summary>
    /// Attempts to retrieve an <see cref="HmacPolicy"/> based on the specified name.
    /// </summary>
    /// <param name="source">An <see cref="IHmacPolicyCollection"/> implementation instance.</param>
    /// <param name="name">A <c>string</c> name representing an <see cref="HmacPolicy"/>.</param>
    /// <param name="policy">An <see cref="HmacPolicy"/>.</param>
    /// <returns>A <c>bool</c> indicating the success of the operation.</returns>
    public static bool TryGetValue(this IHmacPolicyCollection source, string name, out HmacPolicy policy)
    {
        if (source is IComponentCollection<HmacPolicy> components)
        {
            return components.TryGetValue(name, out policy);
        }
        else
        {
            policy = default!;
            return false;
        }
    }
}