using HmacManager.Components;
using HmacManager.Exceptions;
using HmacManager.Mvc;

namespace Unit.Tests.Components;

[TestFixture]
public class Test_HmacOptionsHeaderParser_InvalidBase64
{
    [Test]
    public void Test_InvalidBase64_ThrowsBadHeaderFormatException()
    {
        var headers = new Dictionary<string, string>
        {
            [HmacAuthenticationDefaults.Headers.Options] = "not base64!"
        };

        Assert.Throws<BadHeaderFormatException>(() => new HmacOptionsHeaderParser(headers));
    }
}
