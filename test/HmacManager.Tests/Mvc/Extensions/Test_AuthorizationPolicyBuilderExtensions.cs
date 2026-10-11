using Microsoft.AspNetCore.Authorization;
using HmacManager.Mvc;
using NUnit.Framework;

namespace HmacManager.Unit.Tests.Mvc.Extensions;

[TestFixture]
public class Test_AuthorizationPolicyBuilderExtensions
{
    [Test]
    public void RequireHmacAuthentication_WithPolicyOnly_RequiresBothBuilder()
    {
        // Arrange
        var policyBuilder = new AuthorizationPolicyBuilder();

        // Act
        var result = policyBuilder.RequireHmacAuthentication("test-policy");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.SameAs(policyBuilder)); // Should return same builder for fluent API
    }

    [Test]
    public void RequireHmacAuthentication_WithPolicyAndScheme_ReturnsSameBuilder()
    {
        // Arrange
        var policyBuilder = new AuthorizationPolicyBuilder();

        // Act
        var result = policyBuilder.RequireHmacAuthentication("test-policy", "test-scheme");

        // Assert
        Assert.That(result, Is.SameAs(policyBuilder));
    }

    [Test]
    public void RequireHmacAuthentication_WithNullPolicy_ThrowsException()
    {
        // Arrange
        var policyBuilder = new AuthorizationPolicyBuilder();

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => policyBuilder.RequireHmacAuthentication(null!));
        Assert.That(ex, Is.Not.Null);
    }

    [Test]
    public void RequireHmacAuthentication_WithEmptyPolicy_ThrowsArgumentException()
    {
        // Arrange
        var policyBuilder = new AuthorizationPolicyBuilder();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => policyBuilder.RequireHmacAuthentication(string.Empty));
    }

    [Test]
    public void RequireHmacAuthentication_WithWhitespacePolicy_ThrowsArgumentException()
    {
        // Arrange
        var policyBuilder = new AuthorizationPolicyBuilder();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => policyBuilder.RequireHmacAuthentication("   "));
    }

    [Test]
    public void RequireHmacAuthentication_WithEmptyScheme_IgnoresScheme()
    {
        // Arrange
        var policyBuilder = new AuthorizationPolicyBuilder();

        // Act
        var result = policyBuilder.RequireHmacAuthentication("test-policy", string.Empty);

        // Assert
        Assert.That(result, Is.SameAs(policyBuilder)); // Scheme is skipped if empty
    }

    [Test]
    public void RequireHmacAuthentication_WithNullScheme_IgnoresScheme()
    {
        // Arrange
        var policyBuilder = new AuthorizationPolicyBuilder();

        // Act
        var result = policyBuilder.RequireHmacAuthentication("test-policy", null);

        // Assert
        Assert.That(result, Is.SameAs(policyBuilder));
    }

    [Test]
    public void RequireHmacPolicy_WithSinglePolicy_ReturnsSameBuilder()
    {
        // Arrange
        var policyBuilder = new AuthorizationPolicyBuilder();

        // Act
        var result = policyBuilder.RequireHmacPolicy("test-policy");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.SameAs(policyBuilder));
    }

    [Test]
    public void RequireHmacPolicy_WithMultiplePolicies_ReturnsSameBuilder()
    {
        // Arrange
        var policyBuilder = new AuthorizationPolicyBuilder();

        // Act
        var result = policyBuilder.RequireHmacPolicy("policy1", "policy2", "policy3");

        // Assert
        Assert.That(result, Is.SameAs(policyBuilder));
    }

    [Test]
    public void RequireHmacPolicy_WithNullPolicy_ThrowsException()
    {
        // Arrange
        var policyBuilder = new AuthorizationPolicyBuilder();

        // Act & Assert - Throws NullReferenceException when params contains null
        Assert.Throws<NullReferenceException>(() => policyBuilder.RequireHmacPolicy(null!));
    }

    [Test]
    public void RequireHmacPolicy_WithEmptyPolicy_ThrowsArgumentException()
    {
        // Arrange
        var policyBuilder = new AuthorizationPolicyBuilder();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => policyBuilder.RequireHmacPolicy(string.Empty));
    }

    [Test]
    public void RequireHmacPolicy_WithWhitespacePolicy_ThrowsArgumentException()
    {
        // Arrange
        var policyBuilder = new AuthorizationPolicyBuilder();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => policyBuilder.RequireHmacPolicy("   "));
    }

    [Test]
    public void RequireHmacPolicy_WithMixedValidAndInvalidPolicies_ThrowsArgumentException()
    {
        // Arrange
        var policyBuilder = new AuthorizationPolicyBuilder();

        // Act & Assert - Second policy is empty
        Assert.Throws<ArgumentException>(() => policyBuilder.RequireHmacPolicy("policy1", string.Empty));
    }

    [Test]
    public void RequireHmacScheme_WithSingleScheme_ReturnsSameBuilder()
    {
        // Arrange
        var policyBuilder = new AuthorizationPolicyBuilder();

        // Act
        var result = policyBuilder.RequireHmacScheme("test-scheme");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.SameAs(policyBuilder));
    }

    [Test]
    public void RequireHmacScheme_WithMultipleSchemes_ReturnsSameBuilder()
    {
        // Arrange
        var policyBuilder = new AuthorizationPolicyBuilder();

        // Act
        var result = policyBuilder.RequireHmacScheme("scheme1", "scheme2", "scheme3");

        // Assert
        Assert.That(result, Is.SameAs(policyBuilder));
    }

    [Test]
    public void RequireHmacScheme_WithNullScheme_ThrowsException()
    {
        // Arrange
        var policyBuilder = new AuthorizationPolicyBuilder();

        // Act & Assert - Throws NullReferenceException when params contains null
        Assert.Throws<NullReferenceException>(() => policyBuilder.RequireHmacScheme(null!));
    }

    [Test]
    public void RequireHmacScheme_WithEmptyScheme_ThrowsArgumentException()
    {
        // Arrange
        var policyBuilder = new AuthorizationPolicyBuilder();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => policyBuilder.RequireHmacScheme(string.Empty));
    }

    [Test]
    public void RequireHmacScheme_WithWhitespaceScheme_ThrowsArgumentException()
    {
        // Arrange
        var policyBuilder = new AuthorizationPolicyBuilder();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => policyBuilder.RequireHmacScheme("   "));
    }

    [Test]
    public void RequireHmacScheme_WithMixedValidAndInvalidSchemes_ThrowsArgumentException()
    {
        // Arrange
        var policyBuilder = new AuthorizationPolicyBuilder();

        // Act & Assert - Second scheme is empty
        Assert.Throws<ArgumentException>(() => policyBuilder.RequireHmacScheme("scheme1", string.Empty));
    }

    [Test]
    public void RequireHmacAuthentication_BuilderCanContinueFluentAPI()
    {
        // Arrange
        var policyBuilder = new AuthorizationPolicyBuilder();

        // Act - Chain requires together
        var result = policyBuilder
            .RequireHmacAuthentication("policy1", "scheme1")
            .RequireHmacPolicy("policy2");

        // Assert
        Assert.That(result, Is.SameAs(policyBuilder));
    }
}
