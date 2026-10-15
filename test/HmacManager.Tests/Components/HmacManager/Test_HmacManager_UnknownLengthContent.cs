using System.Text;
using Unit.Tests.Common;

namespace Unit.Tests.Components;

// A StreamContent over a non-seekable stream has no known length until it is read. The verifier
// measures the body it actually received, so the signer must decide whether to hash content by
// what the body turns out to hold, not by whether its length was known up front.
[TestFixture]
public class Test_HmacManager_UnknownLengthContent : TestServiceCollection
{
    [Test]
    public async Task Test_EmptyNonSeekableBody_VerifiesWithoutContent()
    {
        var manager = HmacManagerFactory.Create(PolicySchemeType.Policy_Memory.Policy)!;
        var request = new HttpRequestMessage(HttpMethod.Post, "https://localhost/api/endpoint")
        {
            Content = new StreamContent(new NonSeekableStream([]))
        };
        Assert.That(request.Content.Headers.ContentLength, Is.Null);

        var signingResult = await manager.SignAsync(request);
        Assert.That(signingResult.IsSuccess);

        // What the server builds for a body of zero bytes: a request with no content at all.
        var received = CopyHeaders(request, content: null);
        var verificationResult = await manager.VerifyAsync(received);

        Assert.That(verificationResult.IsSuccess, Is.True);
    }

    [Test]
    public async Task Test_NonEmptyNonSeekableBody_VerifiesAndStaysReadable()
    {
        const string body = "{\"id\":1}";
        var manager = HmacManagerFactory.Create(PolicySchemeType.Policy_Memory.Policy)!;
        var request = new HttpRequestMessage(HttpMethod.Post, "https://localhost/api/endpoint")
        {
            Content = new StreamContent(new NonSeekableStream(Encoding.UTF8.GetBytes(body)))
        };

        var signingResult = await manager.SignAsync(request);
        Assert.That(signingResult.IsSuccess);

        // HmacDelegatingHandler sends the request after signing it, so the body must survive.
        Assert.That(await request.Content.ReadAsStringAsync(), Is.EqualTo(body));

        var received = CopyHeaders(request, new StringContent(body));
        var verificationResult = await manager.VerifyAsync(received);

        Assert.That(verificationResult.IsSuccess, Is.True);
    }

    private static HttpRequestMessage CopyHeaders(HttpRequestMessage signed, HttpContent? content)
    {
        var received = new HttpRequestMessage(signed.Method, signed.RequestUri) { Content = content };
        foreach (var header in signed.Headers)
        {
            received.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return received;
    }
}
