namespace HmacManager.Exceptions;

/// <summary>
/// A class representing exception thrown due to issues in signing content derivation
/// or the signing process. 
/// </summary>
public class HmacSignatureComputationException : Exception
{
    /// <summary>
    /// Creates an instance of <see cref="HmacSignatureComputationException"/>.
    /// </summary>
    /// <param name="message">An optional error message.</param>
    public HmacSignatureComputationException(string? message = null) 
        : base(message ?? "The hmac signature could not be computed due to either no content to sign or an error during signing.")
    {
    }
}