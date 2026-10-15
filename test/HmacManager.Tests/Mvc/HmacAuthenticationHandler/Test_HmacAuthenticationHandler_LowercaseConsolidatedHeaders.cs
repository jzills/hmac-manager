using HmacManager.Components;
using HmacManager.Mvc;
using HmacManager.Mvc.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Unit.Tests.Common;

namespace Unit.Tests.Mvc;

// HTTP/2 requires lowercase field names and Kestrel passes them through as received, so with
// consolidated headers every HTTP/2 request arrives as "hmac-options". The handler parses the
// headers twice — once to find the policy, once inside VerifyAsync — and both must ignore case.
[TestFixture]
public class Test_HmacAuthenticationHandler_LowercaseConsolidatedHeaders
{
    [Test]
    public async Task Test_LowercaseHeaderNames_Authenticate()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddMemoryCache()
            .AddDistributedMemoryCache();

        services.AddAuthentication().AddHmac(options =>
        {
            options.EnableConsolidatedHeaders();
            options.AddPolicy("MyPolicy", policy =>
            {
                policy.UsePublicKey(Guid.NewGuid());
                policy.UsePrivateKey("Hii9mvaSlUm9RRLwsfuUcg==");
                policy.UseMemoryCache(30);
            });
        });

        using var serviceProvider = services.BuildServiceProvider();

        var uri = new Uri("https://localhost:1122/api/endpoint");
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        var signingResult = await serviceProvider
            .GetRequiredService<IHmacManagerFactory>()
            .Create("MyPolicy")!
            .SignAsync(request);
        Assert.That(signingResult.IsSuccess);

        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        httpContext.Request.ConfigureFor(uri, HttpMethod.Get);
        foreach (var header in request.Headers)
        {
            httpContext.Request.Headers[header.Key.ToLowerInvariant()] = header.Value.ToArray();
        }

        Assert.That(httpContext.Request.Headers.ContainsKey(
            HmacAuthenticationDefaults.Headers.Options.ToLowerInvariant()));

        AuthenticateResult? authenticateResult = null;
        Assert.DoesNotThrowAsync(async () => authenticateResult = await httpContext
            .AuthenticateAsync(HmacAuthenticationDefaults.AuthenticationScheme));

        Assert.That(authenticateResult!.Succeeded, Is.True);
    }
}
