namespace HmacManager.Exceptions;

/// <summary>
/// Represents an exception that is thrown when an URI is not absolute.
/// </summary>
public class AbsoluteUriException : UriFormatException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AbsoluteUriException"/> class.
    /// </summary>
    public AbsoluteUriException()
        : base("The request URI must be an absolute URI.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AbsoluteUriException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public AbsoluteUriException(string message)
        : base(message)
    {
    }
}