namespace Unit.Tests.Components;

public class Test_SigningContentBuilder_AbsoluteUri
{
    [Test]
    public async Task Test()
    {
        var requestAbsolute = new HttpRequestMessage(HttpMethod.Get, "https://sample.com/api?q=1");

        var signingContentAbsolute = new HmacManager.Components.SigningContentBuilder()
            .WithRequest(requestAbsolute)
            .Build();

        Assert.That(signingContentAbsolute, Contains.Substring(":/api?q=1:"));
    }
}