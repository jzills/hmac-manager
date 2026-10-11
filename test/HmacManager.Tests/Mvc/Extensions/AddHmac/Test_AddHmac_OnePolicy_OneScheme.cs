using Microsoft.Extensions.DependencyInjection;
using HmacManager.Mvc.Extensions;
using HmacManager.Components;

namespace Unit.Tests;

public class Test_AddHmac_OnePolicy_OneScheme : TestBase
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

                    policy.AddScheme("MyScheme", scheme =>
                    {
                        scheme.AddHeader("MyHeader1");
                        scheme.AddHeader("MyHeader2");
                        scheme.AddHeader("MyHeader3");
                    });
                });
            });

        var serviceProvider = services.BuildServiceProvider();
        var hmacManagerFactory = serviceProvider.GetRequiredService<IHmacManagerFactory>();
        Assert.IsNotNull(hmacManagerFactory, "An implementation of \"IHmacManagerFactory\" is required but has not been registered.");

        var hmacManager = hmacManagerFactory.Create("MyPolicy");
        Assert.IsNotNull(hmacManager, "An implementation of \"IHmacManager\" is required but has not been registered.");

        var signingResult = await hmacManager.SignAsync(request);
        Assert.IsTrue(signingResult.IsSuccess);
        Assert.IsTrue(signingResult.Hmac?.Policy == "MyPolicy");
        Assert.IsTrue(signingResult.Hmac?.HeaderValues?.Count() == 0);
        Assert.IsNull(signingResult.Hmac?.Scheme);

        var verificationResult = await hmacManager.VerifyAsync(request);
        Assert.IsTrue(verificationResult.IsSuccess);
        Assert.IsTrue(verificationResult.Hmac?.Policy == "MyPolicy");
        Assert.IsTrue(verificationResult.Hmac?.HeaderValues?.Count() == 0);
        Assert.IsNull(verificationResult.Hmac?.Scheme);
    }
}