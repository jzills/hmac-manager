namespace HmacManager.Caching;

/// <summary>
/// An <c>enum</c> representing a <see cref="NonceCacheType"/>.
/// </summary>
public enum NonceCacheType
{
    /// <summary>
    /// Represents a cache injected through the <see cref="Microsoft.Extensions.Caching.Memory.IMemoryCache"/> interface.
    /// </summary>
    Memory,

    /// <summary>
    /// Represents a cache injected through the <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/> interface.
    /// </summary>
    Distributed,

    /// <summary>
    /// Represents a Redis cache injected through the <see cref="StackExchange.Redis.IConnectionMultiplexer"/> interface.
    /// </summary>
    Redis
}