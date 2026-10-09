using HmacManager.Headers;
using NUnit.Framework;

namespace Unit.Tests.Headers;

[TestFixture]
public class Test_HeaderValueCollectionValidator
{
    private HttpRequestMessage _request = null!;
    private HeaderValueCollectionValidator _validator = null!;

    [SetUp]
    public void Setup()
    {
        _request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
        _validator = new HeaderValueCollectionValidator(_request);
    }

    [TearDown]
    public void TearDown()
    {
        _request?.Dispose();
    }

    [Test]
    public void Validate_WithAllMatchingHeaders_ReturnsTrue()
    {
        _request.Headers.Add("X-Header-1", "value-1");
        _request.Headers.Add("X-Header-2", "value-2");
        var headerValues = new[]
        {
            new HeaderValue("X-Header-1", "value-1"),
            new HeaderValue("X-Header-2", "value-2")
        };
        
        var result = _validator.Validate(headerValues);
        
        Assert.IsTrue(result.IsValid);
    }

    [Test]
    public void Validate_WithMissingHeader_ReturnsFalse()
    {
        _request.Headers.Add("X-Header-1", "value-1");
        var headerValues = new[]
        {
            new HeaderValue("X-Header-1", "value-1"),
            new HeaderValue("X-Missing-Header", "missing-value")
        };
        
        var result = _validator.Validate(headerValues);
        
        Assert.IsFalse(result.IsValid);
    }

    [Test]
    public void Validate_WithMismatchedValue_ReturnsFalse()
    {
        _request.Headers.Add("X-Header-1", "actual-value");
        var headerValues = new[]
        {
            new HeaderValue("X-Header-1", "expected-value")
        };
        
        var result = _validator.Validate(headerValues);
        
        Assert.IsFalse(result.IsValid);
    }

    [Test]
    public void Validate_WithEmptyCollection_ReturnsTrue()
    {
        var headerValues = Array.Empty<HeaderValue>();
        
        var result = _validator.Validate(headerValues);
        
        Assert.IsTrue(result.IsValid);
    }
}
