using HmacManager.Components;
using HmacManager.Mvc;

namespace Unit.Tests.Components;

[TestFixture]
public class Test_HmacHeaderParser_CaseInsensitive
{
    private static readonly Guid TestNonce = Guid.NewGuid();
    private static readonly DateTimeOffset TestDate = DateTimeOffset.UtcNow;

    // Envoy normalizes all custom header names to lowercase in HTTP/2.
    // These tests verify that HmacHeaderParser handles lowercase keys correctly.
    private static Dictionary<string, string> BuildLowercaseHeaders() => new()
    {
        ["authorization"]      = $"{HmacAuthenticationDefaults.AuthenticationScheme} signature123",
        ["hmac-policy"]        = "MyPolicy",
        ["hmac-nonce"]         = TestNonce.ToString(),
        ["hmac-daterequested"] = TestDate.ToUnixTimeMilliseconds().ToString()
    };

    [Test]
    public void Test_GetAuthorization_WithLowercaseKey_ReturnsSignature()
    {
        var parser = new HmacHeaderParser(BuildLowercaseHeaders());
        Assert.That(parser.GetAuthorization(), Is.EqualTo("signature123"));
    }

    [Test]
    public void Test_GetPolicy_WithLowercaseKey_ReturnsPolicy()
    {
        var parser = new HmacHeaderParser(BuildLowercaseHeaders());
        Assert.That(parser.GetPolicy(), Is.EqualTo("MyPolicy"));
    }

    [Test]
    public void Test_GetNonce_WithLowercaseKey_ReturnsNonce()
    {
        var parser = new HmacHeaderParser(BuildLowercaseHeaders());
        Assert.That(parser.GetNonce(), Is.EqualTo(TestNonce));
    }

    [Test]
    public void Test_GetDateRequested_WithLowercaseKey_ReturnsDate()
    {
        var parser = new HmacHeaderParser(BuildLowercaseHeaders());
        Assert.That(
            parser.GetDateRequested().ToUnixTimeMilliseconds(),
            Is.EqualTo(TestDate.ToUnixTimeMilliseconds()));
    }
}
