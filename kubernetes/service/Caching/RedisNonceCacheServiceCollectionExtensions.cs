using HmacManager.Caching;
using HmacManager.Common;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace HmacManager.Kubernetes.Caching;

internal static class RedisNonceCacheServiceCollectionExtensions
{
    /// <summary>
    /// Serves every policy whose nonce cache type is <c>Distributed</c> from <see cref="RedisNonceCache"/>.
    /// Policies on <c>Memory</c> keep the library's in-process cache.
    /// </summary>
    /// <remarks>
    /// Call after <c>AddHmacManager</c>: this wraps the cache collection it registers. A cache
    /// collection is all-or-nothing, and the library's memory cache is internal, so the only way to
    /// keep it is to take it from the collection the library builds.
    /// </remarks>
    public static IServiceCollection AddRedisNonceCache(this IServiceCollection services, string connectionString)
    {
        var builtIn = services
            .LastOrDefault(descriptor => descriptor.ServiceType == typeof(IComponentCollection<INonceCache>))?
            .ImplementationFactory
                ?? throw new InvalidOperationException(
                    $"{nameof(AddRedisNonceCache)} wraps the nonce caches AddHmacManager registers; call AddHmacManager first.");

        // Connect in the background and keep retrying, rather than failing startup when Redis
        // is not up yet — in the chart it is a separate pod that can be scheduled after this one.
        var options = ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = false;

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(options));
        services.AddSingleton(provider => new RedisNonceCache(provider.GetRequiredService<IConnectionMultiplexer>()));

        return services.Replace(ServiceDescriptor.Scoped<IComponentCollection<INonceCache>>(provider =>
        {
            var defaults = (IComponentCollection<INonceCache>)builtIn(provider);

            var caches = new NonceCacheCollection();
            caches.Add(NonceCacheType.Distributed, provider.GetRequiredService<RedisNonceCache>());
            caches.Add(NonceCacheType.Memory, defaults.Get(nameof(NonceCacheType.Memory))!);
            return caches;
        }));
    }
}
