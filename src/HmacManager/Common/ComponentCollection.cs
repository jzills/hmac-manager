namespace HmacManager.Common;

/// <summary>
/// A class representing a generic <see cref="ComponentCollection{TComponent}"/>.
/// </summary>
public abstract class ComponentCollection<TComponent>
    : IComponentCollection<TComponent> 
        where TComponent : class
{
    /// <summary>
    /// A dictionary of components.
    /// </summary>
    protected readonly IDictionary<string, TComponent> Components = 
        new Dictionary<string, TComponent>();

    /// <summary>
    /// Adds a <typeparamref name="TComponent"/> to the collection stored at the key name.
    /// </summary>
    /// <param name="name">A <c>string</c> representing the name of the component.</param>
    /// <param name="component">A <typeparamref name="TComponent"/>.</param>
    protected void Add(string name, TComponent component)
    {
        if (!Components.ContainsKey(name))
        {
            Components.Add(name, component);
        }
    }

    /// <summary>
    /// Removes a <typeparamref name="TComponent"/> from the collection stored at the key name.
    /// </summary>
    /// <param name="name">A <c>string</c> representing the name of the component.</param>
    protected void Remove(string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Components.Remove(name);
        }
    }

    /// <summary>
    /// Gets a <typeparamref name="TComponent"/> from the collection stored at the key name.
    /// </summary>
    /// <param name="name">A <c>string</c> representing the name of the component.</param>
    public TComponent? Get(string name)
    {
        Components.TryGetValue(name, out var value);
        return value;
    }

    /// <summary>
    /// Gets the entire <typeparamref name="TComponent"/> collection.
    /// </summary>
    public IReadOnlyCollection<TComponent> GetAll() => 
        Components.Values.ToList().AsReadOnly();
}