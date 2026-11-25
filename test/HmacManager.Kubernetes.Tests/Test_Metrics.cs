using HmacManager.Components;
using HmacManager.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace HmacManager.Kubernetes.Tests;

/// <summary>
/// The Prometheus scrape is served on the metrics port and nowhere else. On the verify port a
/// 200 is an "allow": a /metrics route there would let any request for /metrics through the mesh
/// unverified, which is the whole of what these tests guard besides the scrape working.
/// </summary>
public class Test_Metrics
{
    private const int MetricsPort = 9464;

    private static WebApplicationFactory<Program> CreateFactory(
        bool? metricsEnabled = null, string environment = "Production") =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);

            builder.UseSetting("HmacManager:0:Name", "MyPolicy");
            builder.UseSetting("HmacManager:0:Keys:PublicKey", "eb8e9dae-08bd-4883-80fe-1d9a103b30b5");
            builder.UseSetting("HmacManager:0:Keys:PrivateKey", "Hii9mvaSlUm9RRLwsfuUcg==");
            builder.UseSetting("HmacManager:0:Nonce:CacheType", "Memory");
            builder.UseSetting("HmacManager:0:Nonce:MaxAgeInSeconds", "30");

            if (metricsEnabled is not null)
            {
                builder.UseSetting("Metrics:Enabled", metricsEnabled.Value ? "true" : "false");
            }
        });

    private static Task<HttpContext> GetAsync(WebApplicationFactory<Program> factory, int localPort, string path) =>
        factory.Server.SendAsync(context =>
        {
            context.Connection.LocalPort = localPort;
            context.Request.Method = HttpMethods.Get;
            context.Request.Path = path;
        });

    private static async Task<string> ReadBodyAsync(HttpContext context)
    {
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }

    [Test]
    public async Task Test_AVerifiedRequest_IsInTheScrape()
    {
        using var factory = CreateFactory();
        var hmacManager = factory.Services.GetRequiredService<IHmacManagerFactory>().Create("MyPolicy")!;
        using var client = factory.CreateDefaultClient(new HmacDelegatingHandler(hmacManager));

        var response = await client.GetAsync("/api/orders");
        Assert.That((int)response.StatusCode, Is.EqualTo(200));

        var scrape = await GetAsync(factory, MetricsPort, "/metrics");
        var body = await ReadBodyAsync(scrape);

        Assert.That(scrape.Response.StatusCode, Is.EqualTo(200));
        Assert.That(body, Does.Match(
            "hmac_manager_verifications_total\\{[^}]*hmac_manager_outcome=\"verified\"[^}]*hmac_manager_policy=\"MyPolicy\""));
        Assert.That(body, Does.Contain("hmac_manager_verification_duration_seconds_bucket"));
    }

    [Test]
    public async Task Test_MetricsOnTheVerifyPort_IsVerifiedNotServed()
    {
        using var factory = CreateFactory();

        // TestServer's default local port is not the metrics port: this is the verify port.
        var response = await GetAsync(factory, localPort: 8080, "/metrics");

        Assert.That(response.Response.StatusCode, Is.EqualTo(403),
            "A 200 here would be the waypoint letting an unsigned request for /metrics through.");
    }

    [Test]
    public async Task Test_AnyOtherPathOnTheMetricsPort_IsNotFound()
    {
        using var factory = CreateFactory();

        var response = await GetAsync(factory, MetricsPort, "/api/orders");

        Assert.That(response.Response.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public async Task Test_AnUnsignedRequest_IsCountedAsUnsigned()
    {
        using var factory = CreateFactory();

        var response = await GetAsync(factory, localPort: 8080, "/api/orders");
        Assert.That(response.Response.StatusCode, Is.EqualTo(403));

        var body = await ReadBodyAsync(await GetAsync(factory, MetricsPort, "/metrics"));

        Assert.That(body, Does.Match("hmac_manager_verifications_total\\{[^}]*hmac_manager_outcome=\"unsigned\""));
    }

#if NET9_0_OR_GREATER
    [Test]
    public async Task Test_VerificationDuration_HasSecondsSizedBuckets()
    {
        using var factory = CreateFactory();
        var hmacManager = factory.Services.GetRequiredService<IHmacManagerFactory>().Create("MyPolicy")!;
        using var client = factory.CreateDefaultClient(new HmacDelegatingHandler(hmacManager));
        await client.GetAsync("/api/orders");

        // HttpClient returns before the request's duration is recorded; SendAsync waits for it.
        await GetAsync(factory, localPort: 8080, "/api/orders");

        var body = await ReadBodyAsync(await GetAsync(factory, MetricsPort, "/metrics"));

        // The exporter's defaults start at 5, ASP.NET Core's at 0.005: in seconds, every
        // verification and nearly every check in the first bucket. (`.*`, as the route label
        // carries a brace.)
        Assert.That(body, Does.Match("hmac_manager_verification_duration_seconds_bucket\\{[^}]*le=\"0.001\""));
        Assert.That(body, Does.Match("http_server_request_duration_seconds_bucket\\{.*le=\"0.001\"\\}"));
    }

    [Test]
    public async Task Test_RequestMetrics_CountOnlyTheChecks()
    {
        // Development, for the signing helper: the settings above replace its placeholder policy.
        using var factory = CreateFactory(environment: "Development");

        var sign = await factory.Server.SendAsync(context =>
        {
            context.Connection.LocalPort = 8081;
            context.Request.Method = HttpMethods.Post;
            context.Request.Path = "/sign";
            context.Request.ContentType = "application/json";
            context.Request.Body = new MemoryStream(
                """{"Policy":"MyPolicy","Method":"GET","Uri":"http://localhost/"}"""u8.ToArray());
        });
        Assert.That(sign.Response.StatusCode, Is.EqualTo(200));

        await GetAsync(factory, localPort: 8080, "/api/orders");
        var body = await ReadBodyAsync(await GetAsync(factory, MetricsPort, "/metrics"));

        // The one check, a 403, and not the signing call: the ext-authz panels read a 200 here as
        // a request the mesh allowed.
        Assert.That(body, Does.Contain("http_server_request_duration_seconds_count"));
        Assert.That(body, Does.Not.Match("http_server_request_duration_seconds_count\\{[^}]*http_request_method=\"POST\""));
    }
#endif

    [Test]
    public async Task Test_WithMetricsDisabled_TheMetricsPortServesNothing()
    {
        using var factory = CreateFactory(metricsEnabled: false);

        var response = await GetAsync(factory, MetricsPort, "/metrics");

        Assert.That(response.Response.StatusCode, Is.Not.EqualTo(200));
    }
}
