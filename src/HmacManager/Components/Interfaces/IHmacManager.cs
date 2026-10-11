namespace HmacManager.Components;

/// <summary>
/// An interface representing an <see cref="IHmacManager"/>. This contract
/// handles signing outgoing requests and verifying incoming requests.
/// </summary>
public interface IHmacManager
{
    /// <summary>
    /// An instance of <see cref="HmacManagerOptions"/>.
    /// </summary>
    public HmacManagerOptions Options { get; }

    /// <summary>
    /// Signs a <see cref="HttpRequestMessage"/> asynchronously.
    /// </summary>
    /// <param name="request"><see cref="HttpRequestMessage"/></param>
    /// <returns>A <see cref="HmacResult"/> that contains the success of the operation, the generated signature and associated metadata for the signature.</returns>
    Task<HmacResult> SignAsync(HttpRequestMessage request);
    
    /// <summary>
    /// Verifies a signed <see cref="HttpRequestMessage"/> asynchronously.
    /// </summary>
    /// <param name="request"><see cref="HttpRequestMessage"/></param>
    /// <returns>A <see cref="HmacResult"/> that contains the success of the operation, the generated signature and associated metadata for the signature.</returns>
    Task<HmacResult> VerifyAsync(HttpRequestMessage request);
}