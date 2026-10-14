namespace HmacManager.Policies;

/// <summary>
/// Represents the result of a validation process, including whether the validation passed,
/// the associated error (if any), and a collection of child validation results.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationResult"/> class with the specified validity
    /// and optional error.
    /// </summary>
    /// <param name="isValid">A boolean value indicating whether the validation passed.</param>
    /// <param name="error">An optional <see cref="Exception"/> that describes the error, if validation failed.</param>
    public ValidationResult(bool isValid, Exception? error = null)
    {
        IsValid = isValid;
        Error = error;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationResult"/> class with the specified validity,
    /// optional error, and a collection of child validation results.
    /// </summary>
    /// <param name="isValid">A boolean value indicating whether the validation passed.</param>
    /// <param name="error">An optional <see cref="Exception"/> that describes the error, if validation failed.</param>
    /// <param name="validationResults">An array of <see cref="ValidationResult"/> objects to aggregate into this result.</param>
    public ValidationResult(bool isValid, Exception? error, params ValidationResult[] validationResults) 
        : this(isValid && validationResults.All(validationResult => validationResult.IsValid), error)
    {
        ValidationResultAggregate.AddRange(validationResults);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationResult"/> class by combining multiple validation results.
    /// </summary>
    /// <param name="validationResults">An array of <see cref="ValidationResult"/> objects to aggregate into this result.</param>
    public ValidationResult(params ValidationResult[] validationResults) 
        : this(validationResults.All(validationResult => validationResult.IsValid))
    {
        ValidationResultAggregate.AddRange(validationResults);
    }

    /// <summary>
    /// Gets a value indicating whether the validation passed.
    /// </summary>
    public readonly bool IsValid;

    /// <summary>
    /// Gets an optional error that describes the reason for validation failure.
    /// </summary>
    public readonly Exception? Error;

    /// <summary>
    /// A collection of <see cref="ValidationResult"/> objects that are aggregated into this result.
    /// </summary>
    public readonly List<ValidationResult> ValidationResultAggregate = 
        new List<ValidationResult>();

    /// <summary>
    /// Gets the error associated with this validation result, either from the current result or from
    /// the first child validation result that failed.
    /// </summary>
    /// <returns>An <see cref="Exception"/> that describes the validation error.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no errors are found in the validation results.</exception>
    public Exception GetError() 
    {
        if (Error is not null)
        {
            return Error;
        }
        else
        {
            return ValidationResultAggregate.First(result => !result.IsValid).GetError();
        }
    }
}