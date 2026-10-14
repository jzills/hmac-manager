using System.Net.Http.Headers;

namespace Unit.Tests;

public class Test_Memory_ReplayAttack_InvalidSignature : TestServiceCollection
{
    [Test]
    public async Task Test_InvalidSignature_DoesNotConsumeTheNonce()
    {
        var hmacManager = HmacManagerFactory.Create("Policy_Memory");
        Assert.IsNotNull(hmacManager);

        var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://localhost:1122/api/endpoint"));
        var signingResult = await hmacManager!.SignAsync(request);
        Assert.IsTrue(signingResult.IsSuccess);

        var forged = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
        {
            forged.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        forged.Headers.Authorization = new AuthenticationHeaderValue(
            request.Headers.Authorization!.Scheme,
            $"{request.Headers.Authorization.Parameter}=");

        Assert.IsFalse((await hmacManager.VerifyAsync(forged)).IsSuccess);
        Assert.IsTrue((await hmacManager.VerifyAsync(request)).IsSuccess);
    }
}
