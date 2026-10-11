namespace HmacManager.Policies;

/// <summary>
/// Defines the contract for a validator that validates an object of type <typeparamref name="TValidatable"/>.
/// </summary>
/// <typeparam name="TValidatable">The type of object that the validator is responsible for validating.</typeparam>
public interface IValidator<TValidatable>
{
    /// <summary>
    /// Validates the provided object and returns a validation result.
    /// </summary>
    /// <param name="validatable">The object to be validated.</param>
    /// <returns>A <see cref="ValidationResult"/> representing the outcome of the validation.</returns>
    ValidationResult Validate(TValidatable validatable);
}