using System.Configuration;
using Microsoft.Extensions.Configuration;
using HmacManager.Mvc.Configuration;
using HmacManager.Policies;

namespace HmacManager.Mvc.Extensions.Internal;

/// <summary>
/// Provides extension methods for <see cref="IConfigurationSection"/> to facilitate retrieving and building HMAC policies.
/// </summary>
internal static class IConfigurationSectionExtensions
{
    /// <summary>
    /// Retrieves and builds a collection of HMAC policies from a configuration section.
    /// </summary>
    /// <param name="configurationSection">The <see cref="IConfigurationSection"/> containing the policy configuration.</param>
    /// <returns>A <see cref="HmacPolicyCollection"/> containing the built HMAC policies.</returns>
    /// <exception cref="ConfigurationErrorsException">
    /// Thrown if no policies could be bound from the configuration section.
    /// </exception>
    /// <remarks>
    /// This method attempts to bind the configuration section to a list of <see cref="HmacPolicyConfigurationSection"/> objects.
    /// It then builds and adds the policies to a <see cref="HmacPolicyCollection"/>. If the section is empty or no valid
    /// policies can be found, a <see cref="ConfigurationErrorsException"/> is thrown.
    /// </remarks>
    internal static HmacPolicyCollection GetPolicySection(this IConfigurationSection configurationSection)
    {
        var policiesToBuild = configurationSection.Get<List<HmacPolicyConfigurationSection>>();
        if (policiesToBuild?.Count > 0)
        {
            var policies = new HmacPolicyCollection();
            foreach (var policy in policiesToBuild)
            {
                var configBuilder = new HmacPolicyConfigurationBuilder(policy);
                policies.Add(configBuilder.Build(policy.Name));
            }

            return policies;
        }
        else
        {
            throw new ConfigurationErrorsException($"No policies could be bound from the configuration section: \"{configurationSection.Key}\"");
        }
    }
}