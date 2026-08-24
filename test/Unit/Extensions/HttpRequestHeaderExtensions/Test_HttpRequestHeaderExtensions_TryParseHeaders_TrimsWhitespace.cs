using HmacManager.Extensions;
using HmacManager.Schemes;

namespace Unit.Tests;

[TestFixture]
public class Test_HttpRequestHeaderExtensions_TryParseHeaders_TrimsWhitespace : TestBase
{
    [Test]
    public void Test()
    {
        var builder = new SchemeBuilder("MyScheme_1");
        builder.AddHeader("X-Account-Id");
        var scheme = builder.Build();

        var request = new HttpRequestMessage(HttpMethod.Get, "api/endpoint");
        request.Headers.TryAddWithoutValidation("X-Account-Id", "  padded  ");

        var hasHeaderValues = request.Headers.TryParseHeaders(scheme, out var headerValues);

        Assert.IsTrue(hasHeaderValues);
        Assert.That(headerValues.Single().Value, Is.EqualTo("padded"));
    }
}
