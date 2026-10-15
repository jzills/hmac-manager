using System.Text;
using HmacManager.Components;
using HmacManager.Mvc;

namespace Unit.Tests.Components;

[TestFixture]
public class Test_HmacOptionsHeaderParser_CaseInsensitive
{
    // HTTP/2 lowercases header names, and HttpRequestHeaders enumerates them as received.
    [Test]
    public void Test_CreateParser_FromHttpRequestHeaders_WithLowercaseOptionsKey_ReturnsPolicy()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/x");
        request.Headers.TryAddWithoutValidation(
            HmacAuthenticationDefaults.Headers.Options.ToLowerInvariant(),
            Convert.ToBase64String(Encoding.UTF8.GetBytes(
                $"{HmacAuthenticationDefaults.Headers.Policy}=MyPolicy")));

        var parser = new HmacOptionsHeaderParser().CreateParser(request.Headers);

        Assert.That(parser.GetPolicy(), Is.EqualTo("MyPolicy"));
    }
}
