using HmacManager.Schemes;
using HmacManager.Headers;
using NUnit.Framework;

namespace Unit.Tests.Schemes;

[TestFixture]
public class Test_SchemeValidator
{
    private SchemeValidator _validator = null!;

    [SetUp]
    public void Setup()
    {
        _validator = new SchemeValidator();
    }

    [Test]
    public void Validate_WithValidName_ButNoHeaders_ReturnsFalse()
    {
        var scheme = new Scheme("ValidScheme");
        
        var result = _validator.Validate(scheme);
        
        Assert.IsFalse(result.IsValid);
        Assert.That(result.Error!.Message, Contains.Substring("Headers"));
    }

    [Test]
    public void Validate_WithEmptyHeaders_ReturnsFalse()
    {
        var scheme = new Scheme("TestScheme");
        
        var result = _validator.Validate(scheme);
        
        Assert.IsFalse(result.IsValid);
        Assert.That(result.Error!.Message, Contains.Substring("Headers"));
    }

    [Test]
    public void Validate_WithValidName_AndValidHeaders_ReturnsTrue()
    {
        var scheme = new Scheme("TestScheme");
        scheme.Headers.Add(new Header("X-Header", "HeaderClaimType"));
        
        var result = _validator.Validate(scheme);
        
        Assert.IsTrue(result.IsValid);
    }

    [Test]
    public void Validate_WithValidNameAndMultipleHeaders_ReturnsTrue()
    {
        var scheme = new Scheme("TestScheme");
        scheme.Headers.Add(new Header("X-Header-1", "ClaimType1"));
        scheme.Headers.Add(new Header("X-Header-2", "ClaimType2"));
        
        var result = _validator.Validate(scheme);
        
        Assert.IsTrue(result.IsValid);
    }
}
