namespace HmacManager.Exceptions;

/// <summary>
/// A class representing an exception that occurs when attempting to authenticate against
/// a specific scheme but required headers are missing from the request.
/// </summary>
public class MissingHeaderException : Exception
{
    /// <summary>
    /// Creates an instance of <see cref="MissingHeaderException"/>.
    /// </summary>
    /// <param name="message">An optional error message.</param>
    public MissingHeaderException(string? message = null) 
        : base(message ?? "One or more required headers are missing.")
    {
    }
}