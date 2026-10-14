using Microsoft.AspNetCore.Authentication;
using HmacManager.Policies;

namespace HmacManager.Mvc;

/// <summary>
/// A class representing <see cref="HmacAuthenticationOptions"/>.
/// </summary>
public class HmacAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// Creates an instance of <see cref="HmacAuthenticationOptions"/>.
    /// </summary>
    public HmacAuthenticationOptions()
    {
    }

    internal HmacAuthenticationOptions(IHmacPolicyCollection policies) =>
        Options = new HmacManagerOptions(policies);

    /// <summary>
    /// The options for configuring an <see cref="HmacManager"/> instance.
    /// </summary>
    protected readonly HmacManagerOptions Options = new();

    /// <summary>
    /// The available <see cref="HmacEvents"/> for authentication handling.
    /// </summary>
    public new HmacEvents Events { get; set; } = new();

    /// <summary>
    /// Enables scoped policies using the specified delegate.
    /// </summary>
    /// <param name="policiesAccessor">The accessor used to retrieve an <see cref="IHmacPolicyCollection"/> implementation.</param> 
    public void EnableScopedPolicies(Func<IServiceProvider, IHmacPolicyCollection> policiesAccessor) =>
        Options.EnableScopedPolicies(policiesAccessor);

    /// <summary>
    /// Enables consolidated base64 encoded hmac headers. Instead of having a header for each
    /// hmac header value, i.e. Hmac-Policy, Hmac-Scheme, Hmac-DateRequested and so on, they will be
    /// rolled up into a single header.  
    /// </summary>
    public void EnableConsolidatedHeaders() => Options.EnableConsolidatedHeaders();

    /// <summary>
    /// Adds an <see cref="HmacPolicy"/> to the <see cref="HmacPolicyCollection"/>
    /// with the specified name and configuration defined by the <see cref="HmacPolicyBuilder"/> action.
    /// </summary>
    /// <param name="name">The name of the <see cref="HmacPolicy"/>.</param>
    /// <param name="configurePolicy">The configuration action for <see cref="HmacPolicyBuilder"/>.</param>
    public void AddPolicy(string name, Action<HmacPolicyBuilder> configurePolicy) => 
        Options.AddPolicy(name, configurePolicy);

    /// <summary>
    /// Gets the options for this authentication.
    /// </summary>
    /// <returns>The options.</returns> 
    internal HmacManagerOptions GetOptions() => Options;
}