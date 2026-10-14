namespace HmacManager.Common.Extensions;

internal static class IComponentCollectionExtensions
{
    public static bool TryGetValue<TComponent>(
        this IComponentCollection<TComponent> source, 
        string key,
        out TComponent value
    )
    {
        var component = source.Get(key);
        if (component is not null)
        {
            value = component;
            return true;
        }
        else
        {
            value = default!;
            return false;
        }
    }
}