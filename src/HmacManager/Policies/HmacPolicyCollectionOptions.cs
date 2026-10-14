namespace HmacManager.Policies;

/// <summary>
/// A class representing an instance of <see cref="HmacPolicyCollectionOptions"/>.
/// </summary>
public class HmacPolicyCollectionOptions
{
    /// <summary>
    /// An implementation of <see cref="IHmacPolicyCollection"/>.
    /// </summary>
    public IHmacPolicyCollection Policies = new HmacPolicyCollection();

    /// <summary>
    /// An accessor given an <see cref="IServiceProvider"/> that returns an implementation
    /// of an <see cref="IHmacPolicyCollection"/>.
    /// </summary> 
    public Func<IServiceProvider, IHmacPolicyCollection>? PoliciesAccessor = null;

    /// <summary>
    /// True if a <see cref="PoliciesAccessor"/> has been initialized with a non null value, otherwise false. 
    /// </summary>
    public bool IsScopedPoliciesEnabled => PoliciesAccessor is not null;
}