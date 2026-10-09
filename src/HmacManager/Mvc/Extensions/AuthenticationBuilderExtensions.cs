using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using HmacManager.Mvc.Extensions.Internal;
using HmacManager.Policies;

namespace HmacManager.Mvc.Extensions;

/// <summary>
/// A class representing extension methods on an <see cref="AuthenticationBuilder"/>.
/// </summary>
public static class AuthenticationBuilderExtensions
{
    /// <summary>
    /// Adds HmacAuthentication to the <see cref="AuthenticationBuilder"/> with the
    /// configured <see cref="HmacAuthenticationOptions"/>. This method also adds
    /// the implementation for <see cref="HmacManager.Components.IHmacManagerFactory"/> to the DI container.
    /// </summary>
    /// <param name="builder">The calling <see cref="AuthenticationBuilder"/>.</param>
    /// <param name="configureOptions">The configuration action for <see cref="HmacAuthenticationOptions"/>.</param>
    /// <returns>An <see cref="AuthenticationBuilder"/></returns>
    public static AuthenticationBuilder AddHmac(
        this AuthenticationBuilder builder,
        Action<HmacAuthenticationOptions> configureOptions
    )
    {
        var options = new HmacAuthenticationOptions();
        configureOptions.Invoke(options);

        builder.AddHmacScheme(configureOptions);
        builder.Services.AddHmacManager(options.GetOptions());

        return builder;
    }

    /// <summary>
    /// Adds HmacAuthentication to the <see cref="AuthenticationBuilder"/> with the
    /// configured <see cref="IConfigurationSection"/>. This method also adds
    /// the implementation for <see cref="HmacManager.Components.IHmacManagerFactory"/> to the DI container.
    /// </summary>
    /// <param name="builder">The calling <see cref="AuthenticationBuilder"/>.</param>
    /// <param name="configurationSection">The <see cref="IConfigurationSection"/> representing <see cref="HmacPolicy"/> objects for <see cref="HmacManagerOptions"/>.</param>
    /// <returns>An <see cref="AuthenticationBuilder"/></returns>
    public static AuthenticationBuilder AddHmac(
        this AuthenticationBuilder builder,
        IConfigurationSection configurationSection
    )
    {
        var policies = configurationSection.GetPolicySection();

        builder.AddHmacScheme(policies);
        builder.Services.AddHmacManager(new HmacManagerOptions(policies));
        
        return builder;
    }

    /// <summary>
    /// Adds HmacAuthentication to the <see cref="AuthenticationBuilder"/> with the
    /// configured <see cref="IConfigurationSection"/>. This method also adds
    /// the implementation for <see cref="HmacManager.Components.IHmacManagerFactory"/> to the DI container.
    /// </summary>
    /// <param name="builder">The calling <see cref="AuthenticationBuilder"/>.</param>
    /// <param name="configurationSection">The <see cref="IConfigurationSection"/> representing <see cref="HmacPolicy"/> objects for <see cref="HmacManagerOptions"/>.</param>
    /// <param name="events">An instance of <see cref="HmacEvents"/>.</param>
    /// <returns>An <see cref="AuthenticationBuilder"/></returns>
    public static AuthenticationBuilder AddHmac(
        this AuthenticationBuilder builder,
        IConfigurationSection configurationSection,
        HmacEvents events
    )
    {
        var policies = configurationSection.GetPolicySection();

        builder.AddHmacScheme(policies, events);
        builder.Services.AddHmacManager(new HmacManagerOptions(policies));
        
        return builder;
    }

    /// <summary>
    /// Adds an HMAC authentication scheme to the <see cref="AuthenticationBuilder"/>.
    /// </summary>
    /// <param name="builder">The authentication builder to which the scheme will be added.</param>
    /// <param name="configureOptions">A callback to configure the <see cref="HmacAuthenticationOptions"/>.</param>
    /// <remarks>
    /// This method adds a custom HMAC authentication scheme to the authentication system, allowing the use of
    /// HMAC (Hash-based Message Authentication Code) for secure authentication.
    /// </remarks>
    internal static void AddHmacScheme(
        this AuthenticationBuilder builder,
        Action<HmacAuthenticationOptions> configureOptions
    ) => builder.AddScheme<HmacAuthenticationOptions, HmacAuthenticationHandler>(
            HmacAuthenticationDefaults.AuthenticationScheme,
            HmacAuthenticationDefaults.AuthenticationScheme,
            configureOptions
        );

    /// <summary>
    /// Adds an HMAC authentication scheme with a specific set of policies to the <see cref="AuthenticationBuilder"/>.
    /// </summary>
    /// <param name="builder">The authentication builder to which the scheme will be added.</param>
    /// <param name="policies">The set of HMAC policies that will be applied to the scheme.</param>
    /// <param name="events">Optional events for the HMAC authentication scheme.</param>
    /// <remarks>
    /// This method adds an HMAC authentication scheme with a pre-configured set of policies and events.
    /// It allows users to specify custom HMAC policies and authentication events for more control over
    /// the authentication process.
    /// </remarks>
    internal static void AddHmacScheme(
        this AuthenticationBuilder builder,
        IHmacPolicyCollection policies,
        HmacEvents? events = null
    ) => builder.AddHmacScheme(_ => new HmacAuthenticationOptions(policies) 
            { Events = events ?? new HmacEvents() });
}