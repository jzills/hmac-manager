namespace HmacManager.Components;

/// <summary>
/// An interface representing a factory for creating <see cref="HmacResult"/> objects.
/// </summary>
public interface IHmacResultFactory
{
    /// <summary>
    /// Creates a <see cref="HmacResult"/> representing a successful operation. 
    /// </summary>
    /// <param name="hmac">The incoming hmac.</param>
    /// <returns>The result object containing details about the operation.</returns>
    HmacResult Success(Hmac hmac);

    /// <summary>
    /// Creates a <see cref="HmacResult"/> representing a failure operation. 
    /// </summary>
    /// <returns>The result object containing details about the operation.</returns>
    HmacResult Failure();
}