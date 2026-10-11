using HmacManager.Extensions;
using HmacManager.Schemes;

namespace Unit.Tests;

[TestFixture]
public class Test_HttpRequestHeaderExtensions_TryParseHeaders : TestBase
{
    [Test]
    public void Test()
    {
        var builder = new SchemeBuilder("MyScheme_1");
        builder.AddHeader("X-Account-Id");
        builder.AddHeader("X-Email");
        var scheme = builder.Build();

        var request = new HttpRequestMessage(HttpMethod.Get, "api/endpoint");
        request.Headers.Add("X-Account-Id", Guid.NewGuid().ToString());

        var hasHeaderValues = request.Headers.TryParseHeaders(scheme, out var _);
        Assert.IsFalse(hasHeaderValues);
    }
}