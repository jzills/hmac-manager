using HmacManager.Mvc;
using HmacManager.Mvc.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Unit.Tests.Common;

namespace Unit.Tests.Mvc;

[TestFixture]
public class Test_HmacAuthenticationHandler_MalformedOptionsHeader
{
    [Test]
    public void Test_InvalidBase64OptionsHeader_FailsAuthentication_DoesNotThrow()
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

        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        httpContext.Request.ConfigureFor(new Uri("https://localhost:1122/api/endpoint"), HttpMethod.Get);
        httpContext.Request.Headers[HmacAuthenticationDefaults.Headers.Authorization] = "Hmac x";
        httpContext.Request.Headers[HmacAuthenticationDefaults.Headers.Options] = "not base64!";

        AuthenticateResult? authenticateResult = null;
        Assert.DoesNotThrowAsync(async () => authenticateResult = await httpContext
            .AuthenticateAsync(HmacAuthenticationDefaults.AuthenticationScheme));

        Assert.Multiple(() =>
        {
            Assert.That(authenticateResult, Is.Not.Null);
            Assert.That(authenticateResult!.Succeeded, Is.False);
            Assert.That(authenticateResult.Failure, Is.Not.Null);
        });
    }
}
