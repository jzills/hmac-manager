using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace HmacManager.Caching.StackExchangeRedis.Diagnostics;

/// <summary>
/// The complete catalogue of log messages emitted by HmacManager.StackExchangeRedis.
/// </summary>
/// <remarks>
///     <para>
///         The same conventions as the library's own catalogue, <c>HmacLog</c>, in the range after
///         its last: event ids 1400–1499, published in <c>site/content/docs/reference/log-events.md</c>
///         beside the library's, and never reused for a different event.
///     </para>
///     <para>
///         Every event here is a server-side fault or the end of one, which is why none is below
///         <c>Warning</c> but the restore. None can be driven by a caller: a claim is only attempted for
///         a request whose signature has already verified. No message carries the connection
///         configuration, which can hold a password.
///     </para>
/// </remarks>
internal static partial class RedisNonceCacheLog
{
    /// <summary>
    /// Records a claim that could not be made: Redis was unreachable, timed out or returned an error.
    /// </summary>
    /// <remarks>
    /// The exception is rethrown, so the request fails rather than being accepted without replay
    /// protection. It is logged here as well because the host would otherwise record it only as an
    /// unhandled exception, with no event id to alert on.
    /// </remarks>
    [LoggerMessage(
        EventId = 1400,
        Level = LogLevel.Warning,
        Message = "Could not claim a nonce in Redis, so the request it belongs to fails.")]
    public static partial void ClaimFailed(ILogger logger, Exception exception);

    /// <summary>
    /// Records the connection to Redis dropping. Claims fail until it is restored.
    /// </summary>
    [LoggerMessage(
        EventId = 1401,
        Level = LogLevel.Warning,
        Message = "Lost the Redis connection to {EndPoint} ({FailureType}). Nonce claims fail until it is restored.")]
    public static partial void ConnectionFailed(ILogger logger, string? endPoint, ConnectionFailureType failureType, Exception? exception);

    /// <summary>
    /// Records the connection to Redis coming back after <see cref="ConnectionFailed"/>.
    /// </summary>
    /// <remarks>
    /// Information rather than Debug: it is what tells an operator watching for 1401 that the outage
    /// is over.
    /// </remarks>
    [LoggerMessage(
        EventId = 1402,
        Level = LogLevel.Information,
        Message = "Restored the Redis connection to {EndPoint}.")]
    public static partial void ConnectionRestored(ILogger logger, string? endPoint);
}
