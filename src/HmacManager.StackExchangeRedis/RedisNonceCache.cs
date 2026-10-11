using HmacManager.Caching.StackExchangeRedis.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace HmacManager.Caching.StackExchangeRedis;

/// <summary>
/// A nonce cache backed by Redis, which claims each nonce with a single atomic <c>SET NX</c>: of any
/// number of copies of a request, on any number of instances sharing the Redis, exactly one is accepted.
/// </summary>
/// <remarks>
///     <para>
///         Keys are the ones the built-in distributed cache uses, prefixed with
///         <see cref="RedisNonceCacheOptions.InstanceName"/>, so a nonce it recorded through
///         <c>RedisCache</c> stays claimed after switching to this cache.
///     </para>
///     <para>
///         The connection is opened on the first claim, asynchronously, and kept for the life of the
///         cache. A claim Redis cannot answer throws: the request fails rather than being accepted
///         without replay protection.
///     </para>
///     <para>
///         Only <see cref="INonceCache.TryAddAsync"/> is implemented, which is all the library calls. The
///         obsolete <c>SetAsync</c> and <c>ContainsAsync</c> throw <see cref="NotSupportedException"/>.
///     </para>
/// </remarks>
public sealed class RedisNonceCache : NonceCache, IDisposable, IAsyncDisposable
{
    private readonly RedisNonceCacheOptions Options;

    private readonly ILogger Logger;

    /// <summary>
    /// Serializes connecting, so a burst of first claims opens one connection rather than one each.
    /// Waited on asynchronously, so none of them holds a thread while it waits.
    /// </summary>
    private readonly SemaphoreSlim ConnectionLock = new(1, 1);

    private IConnectionMultiplexer? Connection;

    /// <summary>
    /// Whether <see cref="Connection"/> was opened here, and so is closed here. One supplied through
    /// <see cref="RedisNonceCacheOptions.ConnectionMultiplexerFactory"/> belongs to whoever supplied it.
    /// </summary>
    private bool OwnsConnection;

    private volatile bool IsDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisNonceCache"/> class that does not log.
    /// </summary>
    /// <param name="optionsAccessor">How to reach Redis.</param>
    public RedisNonceCache(IOptions<RedisNonceCacheOptions> optionsAccessor)
        : this(optionsAccessor, NullLogger<RedisNonceCache>.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisNonceCache"/> class.
    /// </summary>
    /// <param name="optionsAccessor">How to reach Redis.</param>
    /// <param name="logger">The <see cref="ILogger"/> Redis faults are recorded to.</param>
    public RedisNonceCache(IOptions<RedisNonceCacheOptions> optionsAccessor, ILogger<RedisNonceCache> logger)
    {
        ArgumentNullException.ThrowIfNull(optionsAccessor);
        ArgumentNullException.ThrowIfNull(logger);

        Options = optionsAccessor.Value;
        Logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task<bool> TryAddCoreAsync(Guid nonce, TimeSpan timeToLive)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        try
        {
            var connection = await GetConnectionAsync();
            return await connection.GetDatabase().StringSetAsync(
                Options.InstanceName + CreateKey(NonceCacheType.Distributed, nonce),
                1,
                timeToLive,
                When.NotExists);
        }
        catch (Exception exception) when (exception is not ObjectDisposedException)
        {
            RedisNonceCacheLog.ClaimFailed(Logger, exception);
            throw;
        }
    }

    private async ValueTask<IConnectionMultiplexer> GetConnectionAsync()
    {
        if (Volatile.Read(ref Connection) is { } connection)
        {
            return connection;
        }

        await ConnectionLock.WaitAsync();
        try
        {
            if (Connection is { } existing)
            {
                return existing;
            }

            // Not assigned until it has connected: a factory or connect that throws leaves nothing
            // behind, and the next claim tries again.
            var (opened, owned) = Options.ConnectionMultiplexerFactory is { } factory
                ? (await factory(), false)
                : (await ConnectionMultiplexer.ConnectAsync(Options.GetConfigurationOptions()), true);

            opened.ConnectionFailed += OnConnectionFailed;
            opened.ConnectionRestored += OnConnectionRestored;

            OwnsConnection = owned;
            Volatile.Write(ref Connection, opened);

            // Disposed while connecting: nothing else will close it.
            if (IsDisposed)
            {
                if (Detach() is { } orphaned)
                {
                    await orphaned.DisposeAsync();
                }

                throw new ObjectDisposedException(nameof(RedisNonceCache));
            }

            return opened;
        }
        finally
        {
            ConnectionLock.Release();
        }
    }

    // The subscription connection fails and recovers alongside the interactive one; this cache does
    // not use it, so it would only double every event.
    private void OnConnectionFailed(object? sender, ConnectionFailedEventArgs args)
    {
        if (args.ConnectionType == ConnectionType.Interactive)
        {
            RedisNonceCacheLog.ConnectionFailed(Logger, args.EndPoint?.ToString(), args.FailureType, args.Exception);
        }
    }

    private void OnConnectionRestored(object? sender, ConnectionFailedEventArgs args)
    {
        if (args.ConnectionType == ConnectionType.Interactive)
        {
            RedisNonceCacheLog.ConnectionRestored(Logger, args.EndPoint?.ToString());
        }
    }

    /// <summary>
    /// Detaches from the connection.
    /// </summary>
    /// <returns>The connection, if it was opened here and so is for the caller to close.</returns>
    private IConnectionMultiplexer? Detach()
    {
        var connection = Interlocked.Exchange(ref Connection, null);
        if (connection is null)
        {
            return null;
        }

        connection.ConnectionFailed -= OnConnectionFailed;
        connection.ConnectionRestored -= OnConnectionRestored;

        return OwnsConnection ? connection : null;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return Detach()?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        IsDisposed = true;
        Detach()?.Dispose();
    }
}
