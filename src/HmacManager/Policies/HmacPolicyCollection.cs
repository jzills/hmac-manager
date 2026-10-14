using HmacManager.Common;
using HmacManager.Validation;

namespace HmacManager.Policies;

/// <summary>
/// A class representing a <see cref="HmacPolicyCollection"/>.
/// </summary>
public class HmacPolicyCollection : ComponentCollection<HmacPolicy>, IHmacPolicyCollection
{
    /// <summary>
    /// A simple <c>object</c> lock to restrict collection mutation
    /// since the <see cref="HmacPolicyCollection"/> is registered as
    /// a singleton with the DI container.
    /// </summary>
    private readonly object _locker = new();

    /// <summary>
    /// Validates an <see cref="HmacPolicy"/> before it is added to the collection.
    /// </summary>
    protected readonly IValidator<HmacPolicy> Validator;

    /// <summary>
    /// Creates an empty <see cref="HmacPolicyCollection"/>.
    /// </summary>
    public HmacPolicyCollection()
    {
        Validator = new HmacPolicyValidator(); 
    }

    /// <summary>
    /// Creates an <see cref="HmacPolicyCollection"/> from a specified policy collection.
    /// </summary>
    /// <param name="policies">An <see cref="IEnumerable{T}"/> of <see cref="HmacPolicy"/> objects.</param>
    public HmacPolicyCollection(IEnumerable<HmacPolicy> policies) : this()
    {
        foreach (var policy in policies)
        {
            Add(policy);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<HmacPolicy> Values => GetAll();

    /// <inheritdoc/>
    public void Add(HmacPolicy policy)
    {
        var validationResult = Validator.Validate(policy);
        if (validationResult.IsValid)
        {
            lock (_locker)
            {
                Add(policy.Name!, policy); 
            }
        }
        else
        {
            throw validationResult.GetError();
        }
    }

    /// <inheritdoc/>
    public new void Remove(string policy)
    {
        lock (_locker)
        {
            base.Remove(policy);
        }
    }
}