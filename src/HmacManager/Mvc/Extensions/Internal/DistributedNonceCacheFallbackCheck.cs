using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using HmacManager.Caching;
using HmacManager.Common;
using HmacManager.Diagnostics;
using HmacManager.Policies;

namespace HmacManager.Mvc.Extensions.Internal;

/// <summary>
/// Warns, once at startup, when policies on the <c>Distributed</c> nonce cache are served by the
/// built-in cache over the <see cref="MemoryDistributedCache"/> that <c>AddHmacManager</c> falls back
/// to, which shares nothing between instances.
/// </summary>
/// <remarks>
///     <para>
///         Says nothing when that cache is not the built-in one: when <c>AddNonceCache</c> replaced it,
///         or a cache collection of the application's own replaced every type. In the first case the
///         store is never resolved, so the check does not resolve it either.
///     </para>
///     <para>
///         Not registered for scoped policies, which are resolved per request from wherever the
///         application keeps them, so there is no policy set to inspect at startup.
///     </para>
/// </remarks>
internal sealed class DistributedNonceCacheFallbackCheck : IHostedService
{
    private readonly IServiceCollection Services;
    private readonly IServiceProvider Provider;
    private readonly IHmacPolicyCollection Policies;
    private readonly ILogger Logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedNonceCacheFallbackCheck"/> class.
    /// </summary>
    /// <param name="services">The registrations, to tell whether the cache collection is the built-in one.</param>
    /// <param name="provider">The <see cref="IServiceProvider"/> the store is resolved from.</param>
    /// <param name="policies">The policies the host serves.</param>
    /// <param name="logger">The <see cref="ILogger"/> the warning is written to.</param>
    public DistributedNonceCacheFallbackCheck(
        IServiceCollection services,
        IServiceProvider provider,
        IHmacPolicyCollection policies,
        ILogger<DistributedNonceCacheFallbackCheck> logger)
    {
        Services = services;
        Provider = provider;
        Policies = policies;
        Logger = logger;
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var distributed = Policies.Values
            .Where(policy => policy.Nonce.CacheType == NonceCacheType.Distributed)
            .Select(policy => policy.Name)
            .ToList();

        if (distributed.Count > 0 && IsServedByTheBuiltInCache() && IsInProcess())
        {
            HmacLog.DistributedNonceCacheNotShared(Logger, string.Join(", ", distributed));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private bool IsServedByTheBuiltInCache() =>
        !Provider.GetServices<NonceCacheReplacement>().Any(replacement => replacement.CacheType == NonceCacheType.Distributed) &&
        Services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(IComponentCollection<INonceCache>)) is { } collection &&
        ReferenceEquals(collection.ImplementationFactory, IServiceCollectionExtensions.CacheCollectionFactory);

    // A store that cannot be resolved is left for the first request to report: a check that cannot
    // tell must not stop the host.
    private bool IsInProcess()
    {
        try
        {
            using var scope = Provider.CreateScope();
            return scope.ServiceProvider.GetService<IDistributedCache>() is MemoryDistributedCache;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
