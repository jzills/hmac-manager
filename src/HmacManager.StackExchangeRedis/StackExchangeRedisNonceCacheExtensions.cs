using HmacManager.Caching;
using HmacManager.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HmacManager.Mvc.Extensions;

/// <summary>
/// Extension methods on an <see cref="IServiceCollection"/> for <see cref="RedisNonceCache"/>.
/// </summary>
public static class StackExchangeRedisNonceCacheExtensions
{
    /// <summary>
    /// Protects every policy on the <see cref="NonceCacheType.Distributed"/> nonce cache with Redis,
    /// claiming each nonce with one atomic <c>SET NX</c>. Policies on
    /// <see cref="NonceCacheType.Memory"/> keep the built-in in-process cache.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="setupAction">Configures how to reach Redis. One of
    /// <see cref="RedisNonceCacheOptions.Configuration"/>, <see cref="RedisNonceCacheOptions.ConfigurationOptions"/>
    /// or <see cref="RedisNonceCacheOptions.ConnectionMultiplexerFactory"/> must be set.</param>
    /// <returns>An <see cref="IServiceCollection"/> that can be used to further configure services.</returns>
    /// <remarks>
    /// Can be called before or after <c>AddHmacManager</c>. The cache is a singleton, disposed with the
    /// container, and connects on its first claim rather than here. Under a host, missing options fail
    /// at startup; without one, on the first request that needs the cache.
    /// </remarks>
    public static IServiceCollection AddStackExchangeRedisNonceCache(
        this IServiceCollection services,
        Action<RedisNonceCacheOptions> setupAction
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(setupAction);

        services.AddOptions<RedisNonceCacheOptions>()
            .Configure(setupAction)
            .Validate(
                options => options.IsConfigured,
                $"Set {nameof(RedisNonceCacheOptions.Configuration)}, {nameof(RedisNonceCacheOptions.ConfigurationOptions)} " +
                $"or {nameof(RedisNonceCacheOptions.ConnectionMultiplexerFactory)} to tell the Redis nonce cache how to connect.")
            .ValidateOnStart();

        // Registered as itself, as well as behind the cache type, so the container owns it and
        // disposes it, closing the connection it opened.
        services.TryAddSingleton<RedisNonceCache>();

        return services.AddNonceCache<RedisNonceCache>(NonceCacheType.Distributed);
    }
}
