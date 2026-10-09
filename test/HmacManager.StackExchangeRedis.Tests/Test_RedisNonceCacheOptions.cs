using HmacManager.Caching.StackExchangeRedis;
using StackExchange.Redis;

namespace HmacManager.StackExchangeRedis.Tests;

/// <summary>
/// How the cache turns its options into a connection. No Redis is needed: nothing here connects.
/// </summary>
public class Test_RedisNonceCacheOptions
{
    // An unreachable Redis at startup must not fail every request until the process restarts:
    // the connection keeps retrying in the background instead.
    [Test]
    public void Test_Configuration_WithoutAbortConnect_KeepsRetrying()
    {
        var options = new RedisNonceCacheOptions { Configuration = "localhost:6379" };

        Assert.IsFalse(options.GetConfigurationOptions().AbortOnConnectFail);
    }

    [TestCase("localhost:6379,abortConnect=true")]
    [TestCase("localhost:6379, AbortConnect=True")]
    public void Test_Configuration_WithAbortConnect_IsHonoured(string configuration)
    {
        var options = new RedisNonceCacheOptions { Configuration = configuration };

        Assert.IsTrue(options.GetConfigurationOptions().AbortOnConnectFail);
    }

    [Test]
    public void Test_ConfigurationOptions_AreUsedAsGiven()
    {
        var configurationOptions = ConfigurationOptions.Parse("localhost:6379");
        configurationOptions.AbortOnConnectFail = true;
        var options = new RedisNonceCacheOptions { ConfigurationOptions = configurationOptions };

        var resolved = options.GetConfigurationOptions();

        Assert.IsTrue(resolved.AbortOnConnectFail);
        Assert.That(resolved, Is.Not.SameAs(configurationOptions), "a copy, so the cache cannot change the caller's instance");
    }

    [Test]
    public void Test_ConfigurationOptions_TakePrecedenceOverConfiguration()
    {
        var options = new RedisNonceCacheOptions
        {
            Configuration = "first:6379",
            ConfigurationOptions = ConfigurationOptions.Parse("second:6379")
        };

        Assert.That(options.GetConfigurationOptions().EndPoints.Single().ToString(), Does.Contain("second"));
    }
}
