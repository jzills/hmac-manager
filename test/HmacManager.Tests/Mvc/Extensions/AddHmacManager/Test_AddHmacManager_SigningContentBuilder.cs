using HmacManager.Components;
using HmacManager.Mvc.Extensions;
using HmacManager.Policies;
using Microsoft.Extensions.DependencyInjection;

namespace Unit.Tests;

public class Test_AddHmacManager_SigningContentBuilder : TestBase
{
    [Test]
    [TestCaseSource(typeof(TestCaseSource), nameof(TestCaseSource.GetHttpRequestMessages))]
    public async Task Test(HttpRequestMessage request)
    {
        var services = new ServiceCollection()
            .AddMemoryCache() 
            .AddHmacManager(options =>
            {
                options.AddPolicy("MyPolicy_1", policy =>
                {
                    policy.UsePublicKey(PublicKey);
                    policy.UsePrivateKey(PrivateKey);
                    policy.UseMemoryCache(30);
                    policy.UseSigningContentBuilder(context => 
                    {
                        return "Test:1:2:3";
                    });
                });
            });

        var serviceProvider = services.BuildServiceProvider();
        var hmacManagerFactory = serviceProvider.GetRequiredService<IHmacManagerFactory>();

        var hmacManager = hmacManagerFactory.Create("MyPolicy_1");
        if (hmacManager is null)
        {
            Assert.Fail();
        }
        else
        {
            var (signingResult, verificationResult) = (
                await hmacManager.SignAsync(request), 
                await hmacManager.VerifyAsync(request)
            );

            Assert.That(signingResult.Hmac, Is.Not.Null);
            Assert.That(signingResult.Hmac.SigningContent, Is.EqualTo("Test:1:2:3"));
            Assert.That(verificationResult.Hmac, Is.Not.Null);
            Assert.That(verificationResult.Hmac.SigningContent, Is.EqualTo("Test:1:2:3"));
            Assert.That(verificationResult.Hmac.Signature, Is.EqualTo(signingResult.Hmac.Signature));
        }
    }
}