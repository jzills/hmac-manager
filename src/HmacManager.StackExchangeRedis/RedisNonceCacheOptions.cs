using StackExchange.Redis;

namespace HmacManager.Caching.StackExchangeRedis;

/// <summary>
/// Configures how <see cref="RedisNonceCache"/> reaches Redis.
/// </summary>
/// <remarks>
/// The names match <c>RedisCacheOptions</c> from <c>Microsoft.Extensions.Caching.StackExchangeRedis</c>,
/// so an application switching from <c>UseDistributedCache</c> over <c>AddStackExchangeRedisCache</c>
/// can copy its settings across. Set one of <see cref="ConnectionMultiplexerFactory"/>,
/// <see cref="ConfigurationOptions"/> or <see cref="Configuration"/>; that is also their order of
/// precedence.
/// </remarks>
public class RedisNonceCacheOptions
{
    /// <summary>
    /// The StackExchange.Redis configuration string, such as <c>localhost:6379</c>.
    /// </summary>
    /// <remarks>
    /// The connection is made with <c>abortConnect=false</c> unless this string sets
    /// <c>abortConnect</c> itself, so a Redis that is not up yet is retried in the background
    /// instead of failing every claim until the process restarts.
    /// </remarks>
    public string? Configuration { get; set; }

    /// <summary>
    /// The StackExchange.Redis configuration, used as given in place of <see cref="Configuration"/>.
    /// </summary>
    public ConfigurationOptions? ConfigurationOptions { get; set; }

    /// <summary>
    /// Supplies the connection, in place of either configuration: to share one the application
    /// already has, for example. The cache does not dispose a connection it was given.
    /// </summary>
    public Func<Task<IConnectionMultiplexer>>? ConnectionMultiplexerFactory { get; set; }

    /// <summary>
    /// A prefix for every key the cache writes.
    /// </summary>
    /// <remarks>
    /// Set it to the <c>InstanceName</c> of the <c>RedisCache</c> the built-in distributed cache was
    /// using, if any, so nonces recorded before the switch stay claimed after it.
    /// </remarks>
    public string? InstanceName { get; set; }

    /// <summary>
    /// Whether any way to connect has been set.
    /// </summary>
    internal bool IsConfigured =>
        ConnectionMultiplexerFactory is not null || ConfigurationOptions is not null || !string.IsNullOrWhiteSpace(Configuration);

    /// <summary>
    /// The configuration to connect with, when no <see cref="ConnectionMultiplexerFactory"/> is set.
    /// </summary>
    /// <returns>A copy of <see cref="ConfigurationOptions"/> if it is set, otherwise
    /// <see cref="Configuration"/> parsed.</returns>
    internal ConfigurationOptions GetConfigurationOptions()
    {
        if (ConfigurationOptions is not null)
        {
            return ConfigurationOptions.Clone();
        }

        var options = ConfigurationOptions.Parse(Configuration!);
        if (!SetsAbortConnect(Configuration!))
        {
            options.AbortOnConnectFail = false;
        }

        return options;
    }

    // ConfigurationOptions.AbortOnConnectFail reports a default when the string leaves it unset, so
    // whether it was asked for can only be read from the string.
    private static bool SetsAbortConnect(string configuration) =>
        configuration
            .Split(',')
            .Any(option => option.TrimStart().StartsWith("abortConnect=", StringComparison.OrdinalIgnoreCase));
}
