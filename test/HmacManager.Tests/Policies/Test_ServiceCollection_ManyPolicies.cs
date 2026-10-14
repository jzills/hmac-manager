using Microsoft.Extensions.DependencyInjection;
using HmacManager.Components;
using HmacManager.Mvc.Extensions;

namespace Unit.Tests;

public class Test_ServiceCollection_ManyPolicies : TestBase
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
                options.AddPolicy("MyPolicy_1", policy =>
                {
                    policy.UsePublicKey(PublicKey);
                    policy.UsePrivateKey(PrivateKey);
                    policy.UseContentHashAlgorithm(ContentHashAlgorithm.SHA1);
                    policy.UseSigningHashAlgorithm(SigningHashAlgorithm.HMACSHA1);
                    policy.UseDistributedCache(100);
                });

                options.AddPolicy("MyPolicy_2", policy =>
                {
                    policy.UsePublicKey(PublicKey);
                    policy.UsePrivateKey(PrivateKey);
                    policy.UseContentHashAlgorithm(ContentHashAlgorithm.SHA256);
                    policy.UseSigningHashAlgorithm(SigningHashAlgorithm.HMACSHA256);
                    policy.UseDistributedCache(100);
                });

                options.AddPolicy("MyPolicy_3", policy =>
                {
                    policy.UsePublicKey(PublicKey);
                    policy.UsePrivateKey(PrivateKey);
                    policy.UseContentHashAlgorithm(ContentHashAlgorithm.SHA512);
                    policy.UseSigningHashAlgorithm(SigningHashAlgorithm.HMACSHA512);
                    policy.UseDistributedCache(100);
                });
            });

        var serviceProvider = services.BuildServiceProvider();
        var hmacManagerFactory = serviceProvider.GetRequiredService<IHmacManagerFactory>();
        Assert.IsNotNull(hmacManagerFactory, "An implementation of \"IHmacManagerFactory\" is required but has not been registered.");

        var hmacManager_policy1 = hmacManagerFactory.Create("MyPolicy_1");
        Assert.IsNotNull(hmacManager_policy1, "An implementation of \"IHmacManager\" is required but has not been registered.");

        var hmacManager_policy2 = hmacManagerFactory.Create("MyPolicy_2");
        Assert.IsNotNull(hmacManager_policy2, "An implementation of \"IHmacManager\" is required but has not been registered.");

        var hmacManager_policy3 = hmacManagerFactory.Create("MyPolicy_3");
        Assert.IsNotNull(hmacManager_policy3, "An implementation of \"IHmacManager\" is required but has not been registered.");

        // var signingResult_policy1 = await hmacManager_policy1.SignAsync(request);
        // var signingResult_policy2 = await hmacManager_policy2.SignAsync(request);
        // var signingResult_policy3 = await hmacManager_policy3.SignAsync(request);

        // Assert.IsTrue(signingResult_policy1.IsSuccess);
        // Assert.IsTrue(signingResult_policy2.IsSuccess);
        // Assert.IsTrue(signingResult_policy3.IsSuccess);

        // Assert.IsTrue(signingResult_policy1!.Hmac!.Signature != signingResult_policy2!.Hmac!.Signature);
        // Assert.IsTrue(signingResult_policy2!.Hmac!.Signature != signingResult_policy3!.Hmac!.Signature);
    }
}