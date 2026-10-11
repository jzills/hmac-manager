using HmacManager.Policies;

namespace HmacManager.Headers;

/// <summary>
/// Validates instances of <see cref="Header"/>.
/// </summary>
public class HeaderValidator : IValidator<Header>
{
    /// <summary>
    /// Validates a <see cref="Header"/> object.
    /// </summary>
    /// <param name="validatable">The <see cref="Header"/> instance to validate.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating whether the validation was successful or not.</returns>
    public ValidationResult Validate(Header validatable)
    {
        if (string.IsNullOrWhiteSpace(validatable.Name))
        {
            return new ValidationResult(isValid: false, 
                new ArgumentException("The \"Name\" property cannot be null, empty or whitespace."));
        }
        else if (string.IsNullOrWhiteSpace(validatable.ClaimType))
        {
            return new ValidationResult(isValid: false, 
                new ArgumentException("The \"ClaimType\" property cannot be null, empty or whitespace."));
        }
        else
        {
            return new ValidationResult(isValid: true);
        }
    }
}