using HmacManager.Policies;

namespace HmacManager.Headers;

/// <summary>
/// Validates a <see cref="HeaderValue"/> instance by comparing its value with the corresponding header value in the <see cref="HttpRequestMessage"/>.
/// </summary>
public class HeaderValueValidator : IValidator<HeaderValue>
{
    /// <summary>
    /// The <see cref="HttpRequestMessage"/> associated with the validation.
    /// </summary>
    protected readonly HttpRequestMessage Request;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeaderValueValidator"/> class.
    /// </summary>
    /// <param name="request">The <see cref="HttpRequestMessage"/> associated with the validation.</param>
    public HeaderValueValidator(HttpRequestMessage request) => Request = request;

    /// <summary>
    /// Validates a <see cref="HeaderValue"/> instance.
    /// </summary>
    /// <param name="validatable">The <see cref="HeaderValue"/> instance to validate.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating whether the validation was successful or not.</returns>
    public ValidationResult Validate(HeaderValue validatable)
    {
        IEnumerable<string>? values;
        if (!Request.Headers.TryGetValues(validatable.Name!, out values))
        {
            return new ValidationResult(isValid: false, 
                new ArgumentException($"The \"{nameof(HeaderValue)}\" with name \"{validatable.Name}\" is not present in the request."));
        }
        else if (values is null)
        {
            return new ValidationResult(isValid: false, 
                new ArgumentException($"The \"{nameof(HeaderValue)}\" with name \"{validatable.Name}\" is present in the request but is missing a value."));
        }

        var value = values.FirstOrDefault();
        if (value is null)
        {
            return new ValidationResult(isValid: false, 
                new ArgumentException($"The \"{nameof(HeaderValue)}\" with name \"{validatable.Name}\" is present in the request but is missing a value."));
        }
        else if (value != validatable.Value)
        {
            return new ValidationResult(isValid: false, 
                new ArgumentException($"The \"{nameof(HeaderValue)}\" with name \"{validatable.Name}\" does not match the value present in the header."));
        }
        else
        {
            return new ValidationResult(isValid: true);
        }
    }
}