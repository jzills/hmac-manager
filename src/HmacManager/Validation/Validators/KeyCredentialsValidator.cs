using HmacManager.Policies;

namespace HmacManager.Validation;

/// <summary>
/// Validates a <see cref="KeyCredentials"/> object, ensuring that its public and private keys are valid.
/// </summary>
internal class KeyCredentialsValidator : IValidator<KeyCredentials>
{
    /// <summary>
    /// An instance of <see cref="PublicKeyValidator"/> to validate the public key property of the <see cref="KeyCredentials"/>.
    /// </summary>
    private readonly PublicKeyValidator _publicKeyValidator = new();

    /// <summary>
    /// An instance of <see cref="PrivateKeyValidator"/> to validate the private key property of the <see cref="KeyCredentials"/>.
    /// </summary>
    private readonly PrivateKeyValidator _privateKeyValidator = new();

    /// <summary>
    /// Validates the specified <see cref="KeyCredentials"/>.
    /// </summary>
    /// <param name="validatable">The <see cref="KeyCredentials"/> object to validate.</param>
    /// <returns>A <see cref="ValidationResult"/> that contains validation results for the <see cref="KeyCredentials"/>.</returns>
    public ValidationResult Validate(KeyCredentials validatable) =>
        new ValidationResult(
            _publicKeyValidator.Validate(validatable.PublicKey),   // Validates the public key
            _privateKeyValidator.Validate(validatable.PrivateKey)  // Validates the private key
        );
}