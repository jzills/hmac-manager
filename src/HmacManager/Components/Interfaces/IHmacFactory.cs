using HmacManager.Headers;
using HmacManager.Schemes;

namespace HmacManager.Components;

/// <summary>
/// Defines a factory interface for creating HMAC objects.
/// </summary>
public interface IHmacFactory
{
    /// <summary>
    /// Asynchronously creates an <see cref="Hmac"/> object based on the specified HTTP request, policy, and optional header scheme.
    /// </summary>
    /// <param name="request">The HTTP request message to use for creating the HMAC.</param>
    /// <param name="policy">The policy associated with the HMAC.</param>
    /// <param name="scheme">An optional header scheme to apply when creating the HMAC.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the created <see cref="Hmac"/> object, or null if creation fails.</returns>
    Task<Hmac> CreateAsync(HttpRequestMessage request, string policy, Scheme? scheme = null);

    /// <summary>
    /// Asynchronously creates an <see cref="Hmac"/> object based on the specified HTTP request and partial HMAC details.
    /// </summary>
    /// <param name="request">The HTTP request message to use for creating the HMAC.</param>
    /// <param name="hmac">A partial HMAC object containing details required to create the final HMAC.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the created <see cref="Hmac"/> object, or null if creation fails.</returns>
    Task<Hmac> CreateAsync(HttpRequestMessage request, HmacPartial? hmac);
}