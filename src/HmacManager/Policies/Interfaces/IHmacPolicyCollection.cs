namespace HmacManager.Policies;

/// <summary>
/// An interface representing an <see cref="IHmacPolicyCollection"/>.
/// </summary>
public interface IHmacPolicyCollection
{
    /// <summary>
    /// Contains the entire collection of <see cref="HmacPolicy"/> objects.
    /// </summary>
    IReadOnlyCollection<HmacPolicy> Values { get; }

    /// <summary>
    /// Adds a specified <see cref="HmacPolicy"/> to the <see cref="HmacPolicyCollection"/>.
    /// </summary>
    /// <param name="policy">An <see cref="HmacPolicy"/>.</param>
    void Add(HmacPolicy policy);

    /// <summary>
    /// Removes a specified <see cref="HmacPolicy"/> from the <see cref="HmacPolicyCollection"/>.
    /// </summary>
    /// <param name="policy">The name of the policy to remove.</param>
    void Remove(string policy);
}