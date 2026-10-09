using HmacManager.Policies;

namespace HmacManager.Mvc;

/// <summary>
/// A class representing an instance of <see cref="HmacManagerOptions"/>.
/// </summary>
public class HmacManagerOptions
{
    /// <summary>
    /// The policy collection options.
    /// </summary>
    protected readonly HmacPolicyCollectionOptions PolicyCollectionOptions = new HmacPolicyCollectionOptions();

    /// <summary>
    /// Represents the flag toggling consolidated header functionality. If enabled, only one header will be
    /// added to each request that represents the policy and scheme configurations. This value will also be base64 encoded. 
    /// </summary>
    internal bool IsConsolidatedHeadersEnabled = false;

    /// <summary>
    /// Creates an options instance.
    /// </summary>
    public HmacManagerOptions() { }

    /// <summary>
    /// Creates an options instance with the specified policies.
    /// </summary>
    /// <param name="policies">A collection of policies.</param>
    internal HmacManagerOptions(IHmacPolicyCollection policies) => PolicyCollectionOptions.Policies = policies;

    /// <summary>
    /// Enables scoped policies using the specified delegate.
    /// </summary>
    /// <param name="policiesAccessor">The accessor used to retrieve an <see cref="IHmacPolicyCollection"/> implementation.</param> 
    public void EnableScopedPolicies(Func<IServiceProvider, IHmacPolicyCollection> policiesAccessor)
    {
        ArgumentNullException.ThrowIfNull(policiesAccessor, nameof(policiesAccessor));

        PolicyCollectionOptions.PoliciesAccessor = policiesAccessor;
    }

    /// <summary>
    /// Enables scoped policies using the specified delegate.
    /// </summary>
    public void EnableConsolidatedHeaders() => IsConsolidatedHeadersEnabled = true;

    /// <summary>
    /// Adds an <see cref="HmacPolicy"/> to the <see cref="HmacPolicyCollection"/>
    /// with the specified name and configuration defined by the <see cref="HmacPolicyBuilder"/> action.
    /// </summary>
    /// <param name="name">The name of the <see cref="HmacPolicy"/>.</param>
    /// <param name="configurePolicy">The configuration action for <see cref="HmacPolicyBuilder"/>.</param>
    public void AddPolicy(string name, Action<HmacPolicyBuilder> configurePolicy)
    {
        var builder = new HmacPolicyBuilder();
        configurePolicy.Invoke(builder);

        var policy = builder.Build(name);
        PolicyCollectionOptions.Policies.Add(policy);
    }

    /// <summary>
    /// Gets the options for this policy collection.
    /// </summary>
    /// <returns>The options.</returns> 
    internal HmacPolicyCollectionOptions GetPolicyCollectionOptions() => PolicyCollectionOptions;
}