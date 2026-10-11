using HmacManager.Policies;

namespace HmacManager.Validation;

/// <summary>
/// Validates a <see cref="DateTimeOffset"/> value to ensure that it is not the minimum or maximum allowed value.
/// </summary>
internal class DateTimeOffsetValidator : IValidator<DateTimeOffset>
{
    /// <summary>
    /// Validates the specified <see cref="DateTimeOffset"/> value.
    /// </summary>
    /// <param name="dateTimeOffset">The <see cref="DateTimeOffset"/> value to validate.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating whether the validation was successful or failed.</returns>
    public ValidationResult Validate(DateTimeOffset dateTimeOffset)
    {
        // Check if the value is the minimum allowed value
        if (dateTimeOffset == DateTimeOffset.MinValue)
        {
            return new ValidationResult(isValid: false,
                new FormatException($"The value for \"{nameof(dateTimeOffset)}\" cannot be the minimum value."));
        }

        // Check if the value is the maximum allowed value
        if (dateTimeOffset == DateTimeOffset.MaxValue)
        {
            return new ValidationResult(isValid: false,
                new FormatException($"The value for \"{nameof(dateTimeOffset)}\" cannot be the maximum value."));
        }

        // Return a valid result if no issues were found
        return new ValidationResult(isValid: true);
    }
}