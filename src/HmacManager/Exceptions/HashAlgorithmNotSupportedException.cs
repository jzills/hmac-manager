using HmacManager.Components;

namespace HmacManager.Exceptions;

/// <summary>
/// Represents an exception that is thrown when a hash algorithm is not supported.
/// </summary>
public class HashAlgorithmNotSupportedException : NotSupportedException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HashAlgorithmNotSupportedException"/> class
    /// for an unsupported content hash algorithm.
    /// </summary>
    /// <param name="hashAlgorithm">The unsupported content hash algorithm.</param>
    public HashAlgorithmNotSupportedException(ContentHashAlgorithm hashAlgorithm) 
        : base(CreateMessage(nameof(ContentHashAlgorithm), hashAlgorithm))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HashAlgorithmNotSupportedException"/> class
    /// for an unsupported signing hash algorithm.
    /// </summary>
    /// <param name="hashAlgorithm">The unsupported signing hash algorithm.</param>
    public HashAlgorithmNotSupportedException(SigningHashAlgorithm hashAlgorithm) 
        : base(CreateMessage(nameof(SigningHashAlgorithm), hashAlgorithm))
    {
    }

    /// <summary>
    /// Creates the exception message for the unsupported hash algorithm.
    /// </summary>
    /// <typeparam name="TEnum">The type of the enum representing the hash algorithm.</typeparam>
    /// <param name="algorithmType">The name of the algorithm type.</param>
    /// <param name="hashAlgorithm">The unsupported hash algorithm.</param>
    /// <returns>The formatted exception message.</returns>
    private static string CreateMessage<TEnum>(
        string algorithmType, 
        TEnum hashAlgorithm
    ) where TEnum : Enum =>
        $"The {algorithmType} \"{Enum.GetName(typeof(TEnum), hashAlgorithm)}\" is not supported.";
}
