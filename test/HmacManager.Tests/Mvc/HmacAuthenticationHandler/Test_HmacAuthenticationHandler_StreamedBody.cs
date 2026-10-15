using System.Text;
using HmacManager.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Unit.Tests.Common;

namespace Unit.Tests.Mvc;

// A body without a Content-Length (a chunked upload, HTTP/2 without the header, anything streamed
// through a proxy) arrives on a stream that cannot be sought, as Kestrel's request body always is.
// The handler must buffer it before anything reads it: the signature covers it, and the endpoint
// still needs it afterwards.
[TestFixture]
public class Test_HmacAuthenticationHandler_StreamedBody : TestServiceCollection
{
    [TestCase("{\"id\":1}")]
    [TestCase("")]
    public async Task Test_StreamedBody_Authenticates_AndStaysReadable(string body)
    {
        var uri = new Uri("https://localhost:1122/api/endpoint");
        var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = new StringContent(body) };
        var signingResult = await HmacManagerFactory
            .Create(PolicySchemeType.Policy_Memory.Policy)!
            .SignAsync(request);
        Assert.That(signingResult.IsSuccess);

        // A scope per request, as in an application: the authentication handler provider is
        // scoped, and from the root it would hand every test case the first case's handler.
        using var scope = ServiceProvider.CreateScope();
        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        httpContext.Request.ConfigureFor(uri, HttpMethod.Post, new NonSeekableStream(Encoding.UTF8.GetBytes(body)));
        foreach (var header in request.Headers)
        {
            httpContext.Request.Headers[header.Key] = header.Value.ToArray();
        }
        Assert.That(httpContext.Request.ContentLength, Is.Null);

        var authenticateResult = await httpContext.AuthenticateAsync(HmacAuthenticationDefaults.AuthenticationScheme);
        var bodyAfter = await new StreamReader(httpContext.Request.Body).ReadToEndAsync();

        Assert.Multiple(() =>
        {
            Assert.That(authenticateResult.Succeeded, Is.True);
            Assert.That(bodyAfter, Is.EqualTo(body));
        });
    }
}
