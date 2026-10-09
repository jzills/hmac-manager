using Microsoft.Extensions.DependencyInjection;
using HmacManager.Mvc.Extensions;
using HmacManager.Components;

namespace Unit.Tests;

public class Test_AddHmac_SigningContentBuilder : TestBase
{
    [Test]
    [TestCaseSource(typeof(TestCaseSource), nameof(TestCaseSource.GetHttpRequestMessages))]
    public async Task Test(HttpRequestMessage request)
    {
        var services = new ServiceCollection()
            .AddDistributedMemoryCache();

        services
            .AddAuthentication()
            .AddHmac(options =>
            {
                options.AddPolicy("MyPolicy", policy =>
                {
                    policy.UsePublicKey(PublicKey);
                    policy.UsePrivateKey(PrivateKey);
                    policy.UseContentHashAlgorithm(ContentHashAlgorithm.SHA256);
                    policy.UseSigningHashAlgorithm(SigningHashAlgorithm.HMACSHA256);
                    policy.UseDistributedCache(30);
                    policy.UseSigningContentBuilder(context => $"{context.Request.Method}:{context.PublicKey}:{context.DateRequested}");
                });
            });

        var hmacManager = services.BuildServiceProvider()
            .GetRequiredService<IHmacManagerFactory>()
            .Create("MyPolicy");
   
        var signingResult = await hmacManager.SignAsync(request);
        Assert.That(signingResult.Hmac.SigningContent, Is.EqualTo($"{request.Method}:{PublicKey}:{signingResult.Hmac.DateRequested}"));
    }
}