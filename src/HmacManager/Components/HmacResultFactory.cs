namespace HmacManager.Components;

/// <summary>
/// Factory class for creating <see cref="HmacResult"/> instances.
/// </summary>
internal class HmacResultFactory : IHmacResultFactory
{
    /// <summary>
    /// Gets the policy associated with the HMAC result.
    /// </summary>
    protected readonly string Policy;

    /// <summary>
    /// Gets the optional header scheme associated with the HMAC result.
    /// </summary>
    protected readonly string? Scheme;

    /// <summary>
    /// Initializes a new instance of the <see cref="HmacResultFactory"/> class with the specified policy and optional header scheme.
    /// </summary>
    /// <param name="policy">The policy to associate with the HMAC result.</param>
    /// <param name="scheme">The optional header scheme to associate with the HMAC result.</param>
    internal HmacResultFactory(string policy, string? scheme = null)
    {
        Policy = policy;
        Scheme = scheme;
    }

    /// <summary>
    /// Creates a successful <see cref="HmacResult"/> with the specified <see cref="Hmac"/> data.
    /// </summary>
    /// <param name="hmac">The HMAC data to include in the result.</param>
    /// <returns>A successful <see cref="HmacResult"/> instance.</returns>
    public HmacResult Success(Hmac hmac) => Create(isSuccess: true, hmac);

    /// <summary>
    /// Creates a failed <see cref="HmacResult"/>.
    /// </summary>
    /// <returns>A failed <see cref="HmacResult"/> instance.</returns>
    public HmacResult Failure() => Create(isSuccess: false);

    /// <summary>
    /// Creates an <see cref="HmacResult"/> with the specified success status and optional HMAC data.
    /// </summary>
    /// <param name="isSuccess">A boolean value indicating whether the result represents success.</param>
    /// <param name="hmac">The optional HMAC data to include in the result.</param>
    /// <returns>An <see cref="HmacResult"/> instance.</returns>
    private HmacResult Create(bool isSuccess, Hmac? hmac = null) =>
        new HmacResult(isSuccess, hmac);
}