using Microsoft.Extensions.DependencyInjection;
using HmacManager.Common.Extensions;
using HmacManager.Components;
using HmacManager.Policies;
using HmacManager.Policies.Extensions;

namespace Unit.Tests.Policies;

[TestFixture]
public class Test_HmacPolicyCollection_DynamicPolicies_Remove_HmacManagerFactory : TestServiceCollection
{
    [Test]
    public void Test_RemovePolicy_VerifyThrough_HmacManagerFactory()
    {
        var policies = ServiceProvider.GetRequiredService<IHmacPolicyCollection>();
        Assert.IsTrue(policies.TryGetValue(PolicySchemeType.Policy_Memory.Policy, out _));

        policies.Remove(PolicySchemeType.Policy_Memory.Policy);

        var hmacManagerFactory = ServiceProvider.GetRequiredService<IHmacManagerFactory>();
        var hmacManager = hmacManagerFactory.Create(PolicySchemeType.Policy_Memory.Policy);
        Assert.IsNull(hmacManager);
    }
}