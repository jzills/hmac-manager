using Microsoft.Extensions.DependencyInjection;
using HmacManager.Common.Extensions;
using HmacManager.Policies;
using HmacManager.Policies.Extensions;

namespace Unit.Tests.Policies;

[TestFixture]
public class Test_HmacPolicyCollection_DynamicPolicies_Remove : TestServiceCollection
{
    [Test]
    public void Test_RemovePolicy_VerifyThrough_HmacPolicyCollection()
    {
        var policies = ServiceProvider.GetRequiredService<IHmacPolicyCollection>();
        Assert.IsTrue(policies.TryGetValue(PolicySchemeType.Policy_Memory.Policy, out _));

        policies.Remove(PolicySchemeType.Policy_Memory.Policy);

        policies = ServiceProvider.GetRequiredService<IHmacPolicyCollection>();
        Assert.IsFalse(policies.TryGetValue(PolicySchemeType.Policy_Memory.Policy, out _));
    }
}