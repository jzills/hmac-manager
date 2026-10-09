using System.Text.Json;
using HmacManager.Policies;
using Unit.Tests.Common;

namespace Unit.Tests;

public class Test_HmacPolicy_JsonConverter : TestBase
{
    [Test]
    [TestCaseSource(typeof(HmacPolicyJsonData), nameof(HmacPolicyJsonData.ReadFromJsonFile))]
    public void Test(HmacPolicy policy) => Assert.DoesNotThrow(() => JsonSerializer.Serialize(policy));
}