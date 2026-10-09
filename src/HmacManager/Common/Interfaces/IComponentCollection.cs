namespace HmacManager.Common;

/// <summary>
/// Defines a collection interface for managing and retrieving components by name.
/// </summary>
/// <typeparam name="TComponent">The type of component in the collection.</typeparam>
public interface IComponentCollection<TComponent>
{
    /// <summary>
    /// Retrieves a component by its name.
    /// </summary>
    /// <param name="name">The name of the component to retrieve.</param>
    /// <returns>The component of type <typeparamref name="TComponent"/> if found; otherwise, <c>null</c>.</returns>
    TComponent? Get(string name);

    /// <summary>
    /// Retrieves all components in the collection.
    /// </summary>
    /// <returns>A read-only collection of all components of type <typeparamref name="TComponent"/>.</returns>
    IReadOnlyCollection<TComponent> GetAll();
}