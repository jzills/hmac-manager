using HmacManager.Policies;

namespace HmacManager.Schemes;

/// <summary>
/// Validates instances of <see cref="Scheme"/>.
/// </summary>
public class SchemeValidator : IValidator<Scheme>
{
    /// <summary>
    /// Validates a <see cref="Scheme"/> object.
    /// </summary>
    /// <param name="validatable">The <see cref="Scheme"/> instance to validate.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating whether the validation was successful or not.</returns>
    public ValidationResult Validate(Scheme validatable)
    {
        if (string.IsNullOrWhiteSpace(validatable.Name))
        {
            return new ValidationResult(isValid: false, 
                new ArgumentException("The \"Name\" property cannot be null, empty or whitespace."));
        }
        
        if (validatable.Headers.GetAll().Count == 0)
        {
            return new ValidationResult(isValid: false, 
                new ArgumentException("The \"Headers\" property cannot be an empty collection."));
        }
        else
        {
            return new ValidationResult(isValid: true);
        }
    }
}