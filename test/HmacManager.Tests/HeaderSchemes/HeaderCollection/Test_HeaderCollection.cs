using HmacManager.Headers;

namespace Unit.Tests.Schemes;

[TestFixture]
public class Test_HeaderCollection
{
    [Test]
    public void Test()
    {
        var headers = new HeaderCollection();
        Assert.DoesNotThrow(() => headers.Add(new Header("Header_1", string.Empty)));
        Assert.DoesNotThrow(() => headers.Add(new Header("Header_1", "          ")));
        Assert.DoesNotThrow(() => headers.Add(new Header("Header_1", default!)));
        Assert.DoesNotThrow(() => headers.Add(new Header("Header_1", null!)));
    }
}