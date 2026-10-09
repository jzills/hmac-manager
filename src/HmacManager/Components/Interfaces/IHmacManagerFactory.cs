namespace HmacManager.Components;

/// <summary>
/// An interface representing an <see cref="IHmacManagerFactory"/>, which
/// handles the creation of a configured <see cref="HmacManager"/>.
/// </summary>
public interface IHmacManagerFactory
{
    /// <summary>
    /// Creates an implementation of <see cref="IHmacManager"/> based on the specified policy.
    /// </summary>
    /// <param name="policy">The name of the policy.</param>
    /// <returns>An implementation of <see cref="IHmacManager"/>, by default, a <see cref="HmacManager"/> object.</returns>
    IHmacManager? Create(string policy);
    
    /// <summary>
    /// Creates an implementation of <see cref="IHmacManager"/> based on the specified policy and header scheme.
    /// </summary>
    /// <param name="policy">The name of the policy.</param>
    /// <param name="scheme">The name of the header scheme.</param>
    /// <returns>An implementation of <see cref="IHmacManager"/>, by default, a <see cref="HmacManager"/> object.</returns>
    IHmacManager? Create(string policy, string? scheme);
}