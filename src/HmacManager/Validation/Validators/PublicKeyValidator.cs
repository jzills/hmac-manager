using HmacManager.Policies;

namespace HmacManager.Validation;

/// <summary>
/// Validates a public key represented by a <see cref="Guid"/>, ensuring it is not an empty GUID.
/// </summary>
internal class PublicKeyValidator : IValidator<Guid>
{
    /// <summary>
    /// Validates the specified public key GUID.
    /// </summary>
    /// <param name="publicKey">The public key GUID to validate.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating whether the public key is valid or not.</returns>
    public ValidationResult Validate(Guid publicKey)
    {
        // Check if the public key is an empty GUID
        if (publicKey == Guid.Empty)
        {
            return new ValidationResult(isValid: false,
                new FormatException($"The value for \"{nameof(publicKey)}\" cannot be an empty guid."));
        }

        // If no issues were found, return a valid result
        return new ValidationResult(isValid: true);
    }
}