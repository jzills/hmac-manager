using HmacManager.Policies;

namespace HmacManager.Validation;

/// <summary>
/// Validates a <see cref="string"/> representing a name, ensuring that it is not null, empty, or whitespace.
/// </summary>
internal class NameValidator : IValidator<string?>
{
    /// <summary>
    /// Validates the specified <see cref="string"/> name.
    /// </summary>
    /// <param name="name">The name to validate.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating whether the name is valid or not.</returns>
    public ValidationResult Validate(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return new ValidationResult(isValid: false, 
                new ArgumentException($"The \"{nameof(name)}\" cannot be null, empty or whitespace."));
        }
        else
        {
            return new ValidationResult(isValid: true);
        }
    }
}