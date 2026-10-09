using HmacManager.Policies;
using HmacManager.Validation;
using NUnit.Framework;

namespace HmacManager.Unit.Tests.Validation.Validators;

[TestFixture]
internal class Test_NonceValidator
{
    [Test]
    public void Validate_WithValidNonce_ReturnsValid()
    {
        // Arrange
        var validator = new NonceValidator();
        var nonce = Guid.NewGuid();

        // Act
        var result = validator.Validate(nonce);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Error, Is.Null);
    }

    [Test]
    public void Validate_WithEmptyGuid_ReturnsInvalid()
    {
        // Arrange
        var validator = new NonceValidator();
        var nonce = Guid.Empty;

        // Act
        var result = validator.Validate(nonce);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Error, Is.TypeOf<FormatException>());
    }

    [Test]
    public void Validate_WithMultipleValidNonces_AllReturnValid()
    {
        // Arrange
        var validator = new NonceValidator();

        // Act & Assert
        for (int i = 0; i < 5; i++)
        {
            var nonce = Guid.NewGuid();
            var result = validator.Validate(nonce);
            Assert.That(result.IsValid, Is.True, $"Nonce {i} should be valid");
        }
    }

    [Test]
    public void Validate_ExceptionMessageContainsNonceName()
    {
        // Arrange
        var validator = new NonceValidator();

        // Act
        var result = validator.Validate(Guid.Empty);

        // Assert
        Assert.That(result.Error?.Message, Contains.Substring("nonce"));
    }
}
