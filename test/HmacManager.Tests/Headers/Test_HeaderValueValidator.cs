using HmacManager.Headers;
using HmacManager.Policies;
using NUnit.Framework;

namespace Unit.Tests.Headers;

[TestFixture]
public class Test_HeaderValueValidator
{
    private HttpRequestMessage _request = null!;
    private HeaderValueValidator _validator = null!;

    [SetUp]
    public void Setup()
    {
        _request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
        _validator = new HeaderValueValidator(_request);
    }

    [TearDown]
    public void TearDown()
    {
        _request?.Dispose();
    }

    [Test]
    public void Validate_WithMissingHeader_ReturnsFalse()
    {
        var headerValue = new HeaderValue("X-Missing-Header", "value");
        
        var result = _validator.Validate(headerValue);
        
        Assert.IsFalse(result.IsValid);
        Assert.That(result.Error!.Message, Contains.Substring("not present"));
    }

    [Test]
    public void Validate_WithMatchingHeader_ReturnsTrue()
    {
        _request.Headers.Add("X-Custom-Header", "expected-value");
        var headerValue = new HeaderValue("X-Custom-Header", "expected-value");
        
        var result = _validator.Validate(headerValue);
        
        Assert.IsTrue(result.IsValid);
    }

    [Test]
    public void Validate_WithMismatchedValue_ReturnsFalse()
    {
        _request.Headers.Add("X-Custom-Header", "actual-value");
        var headerValue = new HeaderValue("X-Custom-Header", "expected-value");
        
        var result = _validator.Validate(headerValue);
        
        Assert.IsFalse(result.IsValid);
        Assert.That(result.Error!.Message, Contains.Substring("does not match"));
    }
}
