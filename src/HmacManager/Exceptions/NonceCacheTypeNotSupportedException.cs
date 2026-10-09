namespace HmacManager.Exceptions;

/// <summary>
/// A class representing an exception that occurs due to an invalid nonce cache type.
/// </summary>
public class NonceCacheTypeNotSupportedException : NotSupportedException
{
    /// <summary>
    /// Creates an instance of <see cref="NonceCacheTypeNotSupportedException"/>.
    /// </summary>
    /// <param name="message">An optional error message.</param>
    public NonceCacheTypeNotSupportedException(string? message = null) 
        : base(message ?? "The specified nonce cache type is not supported.")
    {
    }
} 