namespace HmacManager.Exceptions;

/// <summary>
/// A class representing an exception that occurs due to authentication issues.
/// </summary>
public class HmacAuthenticationException : Exception
{
    /// <summary>
    /// Creates an instance of <see cref="HmacAuthenticationException"/>.
    /// </summary>
    /// <param name="message">An optional error message.</param>
    public HmacAuthenticationException(string? message = null) 
        : base(message ?? "Hmac authentication failed with the current manager configuration.")
    {
    }
}