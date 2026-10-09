using HmacManager.Components;
using HmacManager.Exceptions;
using NUnit.Framework;

namespace Unit.Tests.Exceptions;

[TestFixture]
public class Test_HashAlgorithmNotSupportedException
{
    [Test]
    public void Constructor_WithContentHashAlgorithm_CreatesExceptionWithMessage()
    {
        var exception = new HashAlgorithmNotSupportedException(ContentHashAlgorithm.SHA256);
        
        Assert.That(exception.Message, Contains.Substring("ContentHashAlgorithm"));
        Assert.That(exception.Message, Contains.Substring("SHA256"));
        Assert.That(exception.Message, Contains.Substring("not supported"));
    }

    [Test]
    public void Constructor_WithSigningHashAlgorithm_CreatesExceptionWithMessage()
    {
        var exception = new HashAlgorithmNotSupportedException(SigningHashAlgorithm.HMACSHA512);
        
        Assert.That(exception.Message, Contains.Substring("SigningHashAlgorithm"));
        Assert.That(exception.Message, Contains.Substring("HMACSHA512"));
        Assert.That(exception.Message, Contains.Substring("not supported"));
    }

    [Test]
    [TestCaseSource(typeof(ContentHashAlgorithmTestCases), nameof(ContentHashAlgorithmTestCases.AllAlgorithms))]
    public void Constructor_WithAllContentHashAlgorithms_IncludesAlgorithmNameInMessage(ContentHashAlgorithm algorithm)
    {
        var exception = new HashAlgorithmNotSupportedException(algorithm);
        
        Assert.That(exception.Message, Contains.Substring("ContentHashAlgorithm"));
        Assert.That(exception.Message, Does.Contain(algorithm.ToString()));
    }

    [Test]
    [TestCaseSource(typeof(SigningHashAlgorithmTestCases), nameof(SigningHashAlgorithmTestCases.AllAlgorithms))]
    public void Constructor_WithAllSigningHashAlgorithms_IncludesAlgorithmNameInMessage(SigningHashAlgorithm algorithm)
    {
        var exception = new HashAlgorithmNotSupportedException(algorithm);
        
        Assert.That(exception.Message, Contains.Substring("SigningHashAlgorithm"));
        Assert.That(exception.Message, Does.Contain(algorithm.ToString()));
    }
}

public class ContentHashAlgorithmTestCases
{
    public static IEnumerable<ContentHashAlgorithm> AllAlgorithms =>
        typeof(ContentHashAlgorithm)
            .GetEnumValues()
            .Cast<ContentHashAlgorithm>();
}

public class SigningHashAlgorithmTestCases
{
    public static IEnumerable<SigningHashAlgorithm> AllAlgorithms =>
        typeof(SigningHashAlgorithm)
            .GetEnumValues()
            .Cast<SigningHashAlgorithm>();
}
