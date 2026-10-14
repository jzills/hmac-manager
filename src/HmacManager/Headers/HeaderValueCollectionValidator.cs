using HmacManager.Policies;

namespace HmacManager.Headers;

/// <summary>
/// Validates a collection of <see cref="HeaderValue"/> instances.
/// </summary>
public class HeaderValueCollectionValidator : IValidator<IEnumerable<HeaderValue>>
{
    /// <summary>
    /// The <see cref="HttpRequestMessage"/> associated with the validation.
    /// </summary>
    protected readonly HttpRequestMessage Request;

    /// <summary>
    /// The <see cref="HeaderValueValidator"/> used to validate individual <see cref="HeaderValue"/> instances.
    /// </summary>
    protected readonly HeaderValueValidator Validator;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeaderValueCollectionValidator"/> class.
    /// </summary>
    /// <param name="request">The <see cref="HttpRequestMessage"/> associated with the validation.</param>
    public HeaderValueCollectionValidator(HttpRequestMessage request)
    {
        Request = request;
        Validator = new HeaderValueValidator(Request);
    }

    /// <summary>
    /// Validates a collection of <see cref="HeaderValue"/> instances.
    /// </summary>
    /// <param name="headerValues">The collection of <see cref="HeaderValue"/> instances to validate.</param>
    /// <returns>A <see cref="ValidationResult"/> containing the results of validating each <see cref="HeaderValue"/>.</returns>
    public ValidationResult Validate(IEnumerable<HeaderValue> headerValues) =>
        new ValidationResult(headerValues.Select(Validator.Validate).ToArray());
}