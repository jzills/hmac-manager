using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using HmacManager.Mvc.Extensions.Internal;

namespace HmacManager.Mvc.Extensions;

/// <summary>
/// A class representing extension methods on an <see cref="IServiceCollection"/>.
/// </summary>
public static class IServiceCollectionExtensions
{
    /// <summary>
    /// Registers the necessary dependencies to use <see cref="HmacManager.Components.IHmacManagerFactory"/>
    /// in the dependency injection container with the configured <see cref="HmacManagerOptions"/>.
    ///     <para>
    ///         See <see href="https://github.com/jzills/hmac-manager/tree/main/samples/">here</see> for examples.
    ///     </para>
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="configureOptions">The configuration action for <see cref="HmacManagerOptions"/>.</param>
    /// <returns>An <see cref="IServiceCollection"/> that can be used to further configure services.</returns>
    public static IServiceCollection AddHmacManager(
        this IServiceCollection services, 
        Action<HmacManagerOptions> configureOptions
    )
    {
        var options = new HmacManagerOptions();
        configureOptions(options);
        
        return services.AddHmacManager(options);
    }

    /// <summary>
    /// Registers the necessary dependencies to use <see cref="HmacManager.Components.IHmacManagerFactory"/>
    /// in the dependency injection container with the corresponding <see cref="IConfiguration"/> settings.
    ///     <para>
    ///         See <see href="https://github.com/jzills/hmac-manager/tree/main/samples/">here</see> for examples.
    ///     </para>
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="configurationSection">The <see cref="IConfigurationSection"/> for an array of JSON objects representing an <see cref="HmacManager.Policies.HmacPolicy"/>.</param>
    /// <returns>An <see cref="IServiceCollection"/> that can be used to further configure services.</returns>
    /// <remarks>
    /// If <paramref name="configurationSection"/> supports reload notifications (e.g. it was bound from a
    /// configuration source added with <c>reloadOnChange: true</c>), the resulting policy collection stays
    /// in sync with configuration changes for the lifetime of the process — see <see cref="HmacPolicyCollectionReloader"/>.
    /// </remarks>
    public static IServiceCollection AddHmacManager(
        this IServiceCollection services,
        IConfigurationSection configurationSection
    )
    {
        var policies = new ReloadableHmacPolicyCollection(configurationSection.GetPolicySection());
        _ = new HmacPolicyCollectionReloader(configurationSection, policies);

        return services.AddHmacManager(new HmacManagerOptions(policies));
    }
}