using System.Text.Json.Serialization;
using HmacManager.Common;
using HmacManager.Policies;

namespace HmacManager.Schemes;

/// <summary>
/// A class representing a <see cref="SchemeCollection"/>.
/// </summary>
[JsonConverter(typeof(SchemeCollectionJsonConverter))]
public class SchemeCollection : ComponentCollection<Scheme>
{
    /// <summary>
    /// An instance of a header scheme validator.
    /// </summary>
    protected IValidator<Scheme> Validator => new SchemeValidator();

    /// <summary>
    /// Adds a <see cref="Scheme"/> to the collection after validating it.
    /// </summary>
    /// <param name="scheme">The <see cref="Scheme"/> to add to the collection.</param>
    /// <exception cref="Exception">Thrown if the <paramref name="scheme"/> is invalid.</exception>
    public void Add(Scheme scheme)
    {
        var validationResult = Validator.Validate(scheme);
        if (validationResult.IsValid)
        {
            Add(scheme.Name, scheme); 
        }
        else
        {
            throw validationResult.GetError();
        }
    }
}