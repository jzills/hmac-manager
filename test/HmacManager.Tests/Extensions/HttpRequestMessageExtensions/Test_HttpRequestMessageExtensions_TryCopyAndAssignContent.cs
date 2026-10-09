using System.Text;
using System.Text.Json;

namespace Unit.Tests;

[TestFixture]
public class Test_HttpRequestMessageExtensions_TryCopyAndAssignContent : TestBase
{
    [Test]
    public async Task Test()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://some/api")
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                Id = 1,
                Name = "Joshua"
            }), Encoding.UTF8, "application/json")
        };

        var result1 = await request.Content.ReadAsStreamAsync();
        var result2 = await request.Content.ReadAsStreamAsync();

        Assert.That(result1, Is.EqualTo(result2));
    }
}