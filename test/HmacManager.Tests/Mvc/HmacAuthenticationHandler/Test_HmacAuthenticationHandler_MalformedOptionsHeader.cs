using HmacManager.Mvc;
using HmacManager.Mvc.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Unit.Tests.Common;

namespace Unit.Tests.Mvc;

[TestFixture]
public class Test_HmacAuthenticationHandler_MalformedOptionsHeader
{
    [TestCase(true, "not base64!", true)]
    [TestCase(false, null, true)]
    [TestCase(false, "   ", true)]
    [TestCase(false, "unknown-policy", false)]
    [TestCase(false, "MyPolicy", true, "999999999999999999")]
    [TestCase(false, "MyPolicy", true, "-62135596800001")]
    public void Test_InvalidHeaders_FailAuthentication_WithExpectedLog(
        bool consolidated, string? headerValue, bool expectHeaderEvent, string? dateHeader = null)
    {
        var logger = new RecordingLogger<HmacAuthenticationHandler>();
        var services = new ServiceCollection()
            .AddLogging()
            .AddMemoryCache()
            .AddDistributedMemoryCache();
        services.AddSingleton<ILoggerFactory>(new RecordingLoggerFactory(logger));

        services.AddAuthentication().AddHmac(options =>
        {
            if (consolidated)
            {
                options.EnableConsolidatedHeaders();
            }
            options.AddPolicy("MyPolicy", policy =>
            {
                policy.UsePublicKey(Guid.NewGuid());
                policy.UsePrivateKey("Hii9mvaSlUm9RRLwsfuUcg==");
                policy.UseMemoryCache(30);
            });
        });

        using var serviceProvider = services.BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        httpContext.Request.ConfigureFor(new Uri("https://localhost:1122/api/endpoint"), HttpMethod.Get);
        httpContext.Request.Headers[HmacAuthenticationDefaults.Headers.Authorization] = "Hmac x";
        httpContext.Request.Headers[HmacAuthenticationDefaults.Headers.Nonce] = Guid.NewGuid().ToString();
        httpContext.Request.Headers[HmacAuthenticationDefaults.Headers.DateRequested] =
            dateHeader ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        if (headerValue is not null)
        {
            httpContext.Request.Headers[consolidated
                ? HmacAuthenticationDefaults.Headers.Options
                : HmacAuthenticationDefaults.Headers.Policy] = headerValue;
        }

        AuthenticateResult? authenticateResult = null;
        Assert.DoesNotThrowAsync(async () => authenticateResult = await httpContext
            .AuthenticateAsync(HmacAuthenticationDefaults.AuthenticationScheme));

        Assert.Multiple(() =>
        {
            Assert.That(authenticateResult, Is.Not.Null);
            Assert.That(authenticateResult!.Succeeded, Is.False);
            Assert.That(authenticateResult.Failure, Is.Not.Null);
            Assert.That(logger.WithEventId(1304).Count(), Is.EqualTo(expectHeaderEvent ? 1 : 0));
            Assert.That(logger.Entries.Any(entry => entry.Level >= LogLevel.Warning), Is.False);
        });

        if (expectHeaderEvent)
        {
            var entry = logger.WithEventId(1304).Single();
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Debug));
            Assert.That(entry.Exception, Is.Null);
            Assert.That(entry.Message, Is.EqualTo(
                "HMAC authentication rejected because a required header is missing or malformed."));
        }
        else
        {
            Assert.That(logger.WithEventId(1200).Any(), Is.True);
        }
    }
}
