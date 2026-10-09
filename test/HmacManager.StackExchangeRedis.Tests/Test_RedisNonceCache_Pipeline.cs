using System.Net;
using HmacManager.Components;
using HmacManager.Mvc;
using HmacManager.Mvc.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace HmacManager.StackExchangeRedis.Tests;

/// <summary>
/// The cache behind the ASP.NET Core authentication handler, which is where a Redis fault turns
/// into the response a caller sees.
/// </summary>
public class Test_RedisNonceCache_Pipeline
{
    private const string Policy = "MyPolicy";

    private static readonly Guid PublicKey = Guid.NewGuid();
    private static readonly string PrivateKey = Convert.ToBase64String("thisIsMySuperCoolPrivateKey"u8.ToArray());

    private static async Task<WebApplication> StartAsync(string configuration)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthentication().AddHmac(options =>
        {
            options.AddPolicy(Policy, policy =>
            {
                policy.UsePublicKey(PublicKey);
                policy.UsePrivateKey(PrivateKey);
                policy.UseDistributedCache(60);
            });
        });
        builder.Services.AddAuthorization(options =>
            options.AddPolicy("RequireHmac", policy => policy.RequireHmacAuthentication(Policy)));
        builder.Services.AddStackExchangeRedisNonceCache(options =>
        {
            options.Configuration = configuration;
            options.InstanceName = $"test-{Guid.NewGuid():N}:";
        });

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/api", () => "Ok!").RequireAuthorization("RequireHmac");

        await app.StartAsync();
        return app;
    }

    /// <summary>
    /// A client that signs every request it sends, and sends it to the app's own test server.
    /// </summary>
    private static HttpClient CreateSigningClient(WebApplication app)
    {
        var signer = app.Services.GetRequiredService<IHmacManagerFactory>().Create(Policy)!;
        return new HttpClient(new HmacDelegatingHandler(signer) { InnerHandler = app.GetTestServer().CreateHandler() })
        {
            BaseAddress = app.GetTestServer().BaseAddress
        };
    }

    [Test]
    public async Task Test_ARequest_IsAcceptedOnce()
    {
        await using var app = await StartAsync("localhost:6379");
        using var client = CreateSigningClient(app);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api");
        var first = await client.SendAsync(request);
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // The same signed request again, headers and all, sent without re-signing.
        using var raw = app.GetTestClient();
        var replay = new HttpRequestMessage(HttpMethod.Get, "/api");
        foreach (var header in request.Headers)
        {
            replay.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        Assert.That((await raw.SendAsync(replay)).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public void Test_WithoutAConnection_TheHostFailsToStart()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddHmacManager(_ => { });
        builder.Services.AddStackExchangeRedisNonceCache(_ => { });
        var app = builder.Build();

        Assert.That(async () => await app.StartAsync(), Throws.InstanceOf<Microsoft.Extensions.Options.OptionsValidationException>());
    }

    // Failing closed: with Redis unreachable the request is never accepted. The handler does not
    // catch the cache's exception, so it leaves the pipeline as an unhandled exception, which a host
    // turns into a 500 and the test server rethrows to its caller.
    [Test]
    public async Task Test_ARequest_WhenRedisIsUnreachable_IsNotAccepted()
    {
        await using var app = await StartAsync("localhost:1,connectTimeout=250,syncTimeout=250,asyncTimeout=250,connectRetry=0");
        using var client = CreateSigningClient(app);

        Assert.That(async () => await client.GetAsync("/api"),
            Throws.InstanceOf<StackExchange.Redis.RedisException>().Or.InstanceOf<TimeoutException>());
    }
}
