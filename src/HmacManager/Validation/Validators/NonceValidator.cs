using HmacManager.Policies;

namespace HmacManager.Validation;

/// <summary>
/// Validates a <see cref="Guid"/> representing a nonce, ensuring that it is not the empty <see cref="Guid"/>.
/// </summary>
internal class NonceValidator : IValidator<Guid>
{
    /// <summary>
    /// Validates the specified <see cref="Guid"/> nonce.
    /// </summary>
    /// <param name="nonce">The nonce (Guid) to validate.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating whether the nonce is valid or not.</returns>
    public ValidationResult Validate(Guid nonce)
    {
        // Check if the nonce is an empty GUID
        if (nonce == Guid.Empty)
        {
            return new ValidationResult(isValid: false,
                new FormatException($"The value for \"{nameof(nonce)}\" cannot be an empty guid."));
        }

        return new ValidationResult(isValid: true);
    }
}