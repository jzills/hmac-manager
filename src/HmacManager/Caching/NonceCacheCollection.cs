using HmacManager.Common;

namespace HmacManager.Caching;

/// <summary>
/// An <c>class</c> representing a <see cref="NonceCacheCollection"/>.
/// </summary>
public class NonceCacheCollection : ComponentCollection<INonceCache>
{
    /// <summary>
    /// Adds an <see cref="INonceCache"/> specified by <see cref="NonceCacheType"/>.
    /// </summary>
    /// <param name="cacheType">A <see cref="NonceCacheType"/>.</param>
    /// <param name="cache">A <see cref="INonceCache"/>.</param>
    public void Add(NonceCacheType cacheType, INonceCache cache) => 
        Add(Enum.GetName(cacheType)!, cache);
}