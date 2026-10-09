using HmacManager.Extensions;

namespace Unit.Tests;

[TestFixture]
public class Test_HttpRequestMessageExtensions_HasContent : TestBase
{
    [Test]
    public void Test_NullContent_ReturnsFalse()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://some/api");

        Assert.That(request.HasContent(), Is.False);
    }

    [Test]
    public void Test_EmptyStringContent_ReturnsFalse()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://some/api")
        {
            Content = new StringContent(string.Empty)
        };

        Assert.That(request.HasContent(), Is.False);
    }

    [Test]
    public void Test_NonEmptyStringContent_ReturnsTrue()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://some/api")
        {
            Content = new StringContent("{\"id\":1}")
        };

        Assert.That(request.HasContent(), Is.True);
    }

    [Test]
    public void Test_StreamContentWithUnknownLength_ReturnsTrue()
    {
        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        stream.Seek(0, SeekOrigin.Begin);

        var content = new StreamContent(stream);
        content.Headers.ContentLength = null;

        var request = new HttpRequestMessage(HttpMethod.Post, "https://some/api")
        {
            Content = content
        };

        Assert.That(request.HasContent(), Is.True);
    }
}
