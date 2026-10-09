using HmacManager.Validation;
using NUnit.Framework;

namespace Unit.Tests.Validation;

[TestFixture]
public class Test_DateTimeOffsetValidator
{
    private DateTimeOffsetValidator _validator = null!;

    [SetUp]
    public void Setup()
    {
        _validator = new DateTimeOffsetValidator();
    }

    [Test]
    public void Validate_WithMinValue_ReturnsFalse()
    {
        var result = _validator.Validate(DateTimeOffset.MinValue);
        
        Assert.IsFalse(result.IsValid);
        Assert.IsNotNull(result.Error);
        Assert.That(result.Error!.Message, Contains.Substring("minimum value"));
    }

    [Test]
    public void Validate_WithMaxValue_ReturnsFalse()
    {
        var result = _validator.Validate(DateTimeOffset.MaxValue);
        
        Assert.IsFalse(result.IsValid);
        Assert.IsNotNull(result.Error);
        Assert.That(result.Error!.Message, Contains.Substring("maximum value"));
    }

    [Test]
    public void Validate_WithValidValue_ReturnsTrue()
    {
        var result = _validator.Validate(DateTimeOffset.Now);
        
        Assert.IsTrue(result.IsValid);
    }

    [Test]
    public void Validate_WithArbitraryValue_ReturnsTrue()
    {
        var result = _validator.Validate(new DateTimeOffset(2025, 6, 15, 10, 30, 0, TimeSpan.Zero));
        
        Assert.IsTrue(result.IsValid);
    }
}
