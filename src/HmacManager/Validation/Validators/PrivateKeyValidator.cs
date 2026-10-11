using HmacManager.Policies;

namespace HmacManager.Validation;

/// <summary>
/// Validates a private key string, ensuring it is not null, empty, or whitespace, 
/// and that it is a valid base64 encoded string.
/// </summary>
internal class PrivateKeyValidator : IValidator<string>
{
    /// <summary>
    /// Validates the specified private key string.
    /// </summary>
    /// <param name="privateKey">The private key string to validate.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating whether the private key is valid or not.</returns>
    public ValidationResult Validate(string privateKey)
    {
        // Check if the private key is null, empty, or whitespace
        if (string.IsNullOrWhiteSpace(privateKey))
        {
            return new ValidationResult(isValid: false,
                new FormatException($"The value for \"{nameof(privateKey)}\" cannot be null, empty or whitespace."));
        }

        // Try to decode the private key from base64 encoding
        var buffer = new Span<byte>(new byte[privateKey.Length]);
        if (!Convert.TryFromBase64String(privateKey, buffer, out _))
        {
            return new ValidationResult(isValid: false,
                new FormatException($"The supplied \"{nameof(privateKey)}\" is not a valid base64 encoded string."));
        }

        // If no issues were found, return a valid result
        return new ValidationResult(isValid: true);
    }
}