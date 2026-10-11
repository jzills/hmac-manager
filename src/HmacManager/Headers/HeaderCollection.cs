using HmacManager.Common;
using HmacManager.Policies;

namespace HmacManager.Headers;

/// <summary>
/// Represents a collection of <see cref="Header"/> components that can be validated before adding to the collection.
/// </summary>
public class HeaderCollection : ComponentCollection<Header>
{
    /// <summary>
    /// An instance of a header validator.
    /// </summary>
    protected IValidator<Header> Validator = new HeaderValidator();
    
    /// <summary>
    /// Adds a <see cref="Header"/> to the collection after validating it.
    /// </summary>
    /// <param name="header">The <see cref="Header"/> to add to the collection.</param>
    /// <exception cref="Exception">Thrown if the <paramref name="header"/> is invalid.</exception>
    public void Add(Header header)
    {
        var validationResult = Validator.Validate(header);
        if (validationResult.IsValid)
        {
            Add(header.Name!, header);
        }
        else
        {
            throw validationResult.GetError();
        }
    }
}