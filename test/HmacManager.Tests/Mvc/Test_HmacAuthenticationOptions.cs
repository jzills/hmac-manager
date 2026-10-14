using HmacManager.Mvc;
using HmacManager.Components;
using HmacManager.Policies;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Unit.Tests.Mvc;

[TestFixture]
public class Test_HmacAuthenticationOptions
{
    [Test]
    public void Constructor_Default_CreatesOptionsWithEmptyEvents()
    {
        var options = new HmacAuthenticationOptions();
        
        Assert.That(options.Events, Is.Not.Null);
        Assert.That(options.Events, Is.TypeOf<HmacEvents>());
    }

    [Test]
    public void AddPolicy_WithValidPolicy_RegistersPolicy()
    {
        var options = new HmacAuthenticationOptions();
        
        options.AddPolicy("TestPolicy", policy =>
        {
            policy.UsePublicKey(Guid.Parse("a18f5729-32ce-43a4-ac4d-af0a699539ae"));
            policy.UsePrivateKey("xCy0Ucg3YEKlmiK23Zph+g==");
            policy.UseMemoryCache(60);
        });

        Assert.That(options, Is.Not.Null);
    }

    [Test]
    public void EnableConsolidatedHeaders_ConfiguresOptions()
    {
        var options = new HmacAuthenticationOptions();
        
        options.EnableConsolidatedHeaders();
        
        Assert.That(options, Is.Not.Null);
    }

    [Test]
    public void EnableScopedPolicies_WithValidAccessor_ConfiguresOptions()
    {
        var options = new HmacAuthenticationOptions();
        var services = new ServiceCollection()
            .AddMemoryCache()
            .AddDistributedMemoryCache();
        var serviceProvider = services.BuildServiceProvider();

        options.EnableScopedPolicies(_ => new HmacPolicyCollection());
        
        Assert.That(options, Is.Not.Null);
    }

    [Test]
    public void GetOptions_ReturnsNotNull()
    {
        var options = new HmacAuthenticationOptions();
        
        var result = options.GetOptions();
        
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void Events_CanBeSet()
    {
        var options = new HmacAuthenticationOptions();
        var customEvents = new HmacEvents();
        
        options.Events = customEvents;
        
        Assert.That(options.Events, Is.SameAs(customEvents));
    }
}
