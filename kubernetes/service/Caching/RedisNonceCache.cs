using HmacManager.Caching;
using StackExchange.Redis;

namespace HmacManager.Kubernetes.Caching;

/// <summary>
/// Claims a nonce with a single <c>SET key 1 EX ttl NX</c>, so two copies of a request reaching
/// different replicas at the same moment cannot both be accepted.
/// </summary>
/// <remarks>
/// The library's <c>Distributed</c> cache is built on <c>IDistributedCache</c>, which has no
/// conditional write: it reads and then writes, and copies that arrive within one round trip of
/// each other can both read the nonce as unused. Redis is only configured for multi-replica
/// deployments, which are exactly the ones where that race matters.
/// </remarks>
internal sealed class RedisNonceCache(IConnectionMultiplexer redis, TimeProvider? clock = null)
    : NonceCache(clock)
{
    /// <summary>
    /// The key a nonce is claimed under. The same key the library's <c>Distributed</c> cache uses,
    /// so during a rolling upgrade a nonce recorded by a pod still on that cache is refused here
    /// too: <c>SET … NX</c> fails on any existing key, whatever its type.
    /// </summary>
    public static string CreateKey(Guid nonce) => $"HmacManager:Distributed:{nonce}";

    /// <inheritdoc/>
    protected override Task<bool> TryAddCoreAsync(Guid nonce, TimeSpan timeToLive) =>
        redis.GetDatabase().StringSetAsync(CreateKey(nonce), 1, timeToLive, When.NotExists);
}
