using HmacManager.Policies;

namespace HmacManager.Mvc;

/// <summary>
/// Represents a configuration section for an HMAC policy, extending the base <see cref="HmacPolicy"/> class.
/// This class allows for configuration of header schemes and other policy-related settings.
/// </summary>
internal class HmacPolicyConfigurationSection : HmacPolicy
{
    /// <summary>
    /// Gets or sets the collection of header schemes that are part of the HMAC policy configuration.
    /// </summary>
    /// <value>
    /// A list of <see cref="SchemeConfigurationSection"/> representing the header schemes to be used.
    /// </value>
    public new IReadOnlyList<SchemeConfigurationSection>? Schemes { get; set; }
}