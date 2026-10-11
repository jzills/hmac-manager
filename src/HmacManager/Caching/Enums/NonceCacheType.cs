namespace HmacManager.Caching;

/// <summary>
/// An <c>enum</c> representing a <see cref="NonceCacheType"/>.
/// </summary>
public enum NonceCacheType
{
    /// <summary>
    /// Represents a cache injected through the <see cref="Microsoft.Extensions.Caching.Memory.IMemoryCache"/> interface,
    /// or the cache <c>AddNonceCache</c> registers for this type.
    /// </summary>
    Memory,

    /// <summary>
    /// Represents a cache injected through the <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/> interface,
    /// or the cache <c>AddNonceCache</c> registers for this type.
    /// </summary>
    Distributed
}