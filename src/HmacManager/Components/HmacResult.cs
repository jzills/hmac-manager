namespace HmacManager.Components;

/// <summary>
/// A class representing a <see cref="HmacResult"/>.
/// </summary>
public sealed class HmacResult
{
    /// <summary>
    /// Represents the resulting <see cref="Hmac"/>.
    /// </summary>
    /// <value>The <see cref="Hmac"/>.</value>
    public Hmac? Hmac { get; }

    /// <summary>
    /// Represents the success of the authentication operation, i.e. signing or verification.
    /// </summary>
    /// <value>True if the <see cref="HmacResult"/> generation is successful, otherwise false.</value>
    public bool IsSuccess { get; }

    /// <summary>
    /// Represents the date and time the <see cref="HmacResult"/> is generated.
    /// </summary>
    /// <value>A UTC date and time.</value>
    public DateTime DateGenerated { get; } = DateTime.UtcNow;

    /// <summary>
    /// Initializes a new instance of the <see cref="HmacResult"/> class.
    /// </summary>
    /// <param name="isSuccess">Indicates whether the operation was successful.</param>
    /// <param name="hmac">The HMAC (Hash-based Message Authentication Code) associated with the result, or null if not available.</param>
    internal HmacResult(bool isSuccess, Hmac? hmac)
    {
        IsSuccess = isSuccess;
        Hmac = hmac;
    }
}