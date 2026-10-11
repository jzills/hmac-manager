using HmacManager.Policies;

namespace HmacManager.Validation;

/// <summary>
/// Validates a <see cref="HmacPolicy"/> object, ensuring that its name and keys are valid.
/// </summary>
internal class HmacPolicyValidator : IValidator<HmacPolicy>
{
    /// <summary>
    /// An instance of <see cref="NameValidator"/> to validate the name property of the <see cref="HmacPolicy"/>.
    /// </summary>
    private readonly NameValidator _nameValidator = new();

    /// <summary>
    /// An instance of <see cref="KeyCredentialsValidator"/> to validate the keys property of the <see cref="HmacPolicy"/>.
    /// </summary>
    private readonly KeyCredentialsValidator _keysValidator = new();

    /// <summary>
    /// Validates the specified <see cref="HmacPolicy"/>.
    /// </summary>
    /// <param name="validatable">The <see cref="HmacPolicy"/> object to validate.</param>
    /// <returns>A <see cref="ValidationResult"/> that contains validation results for the <see cref="HmacPolicy"/>.</returns>
    public ValidationResult Validate(HmacPolicy validatable) =>
        new ValidationResult(
            _nameValidator.Validate(validatable.Name),
            _keysValidator.Validate(validatable.Keys)
        );
}