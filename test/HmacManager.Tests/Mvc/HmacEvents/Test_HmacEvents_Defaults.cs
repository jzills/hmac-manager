using System.Security.Claims;
using HmacManager.Components;
using HmacManager.Mvc;
using HmacManager.Policies;
using Microsoft.AspNetCore.Http;

namespace Unit.Tests.Mvc;

[TestFixture]
public class Test_HmacEvents_Defaults
{
    [Test]
    public void Test_HmacEvents_Default_Init()
    {
        var events = new HmacEvents();
        Assert.That(events.OnAuthenticationFailureAsync, Is.EqualTo(HmacEventsDefaults.OnAuthenticationFailureAsync));
        Assert.That(events.OnAuthenticationSuccessAsync, Is.EqualTo(HmacEventsDefaults.OnAuthenticationSuccessAsync));
        Assert.That(events.OnValidateKeysAsync, Is.EqualTo(HmacEventsDefaults.OnValidateKeysAsync));
    }

    [Test]
    public async Task Test_HmacEvents_OnAuthenticationFailure_Init()
    {
        var events = new HmacEvents
        {
            OnAuthenticationFailureAsync = (_, _) => Task.FromResult(new Exception("Test"))
        };

        var error = await events.OnAuthenticationFailureAsync(new DefaultHttpContext(), new HmacResult(true, new Hmac { Policy = "" }));
        Assert.That(error.Message, Is.EqualTo("Test"));
        Assert.That(events.OnAuthenticationSuccessAsync, Is.EqualTo(HmacEventsDefaults.OnAuthenticationSuccessAsync));
        Assert.That(events.OnValidateKeysAsync, Is.EqualTo(HmacEventsDefaults.OnValidateKeysAsync));
    }

    [Test]
    public async Task Test_HmacEvents_OnAuthenticationSuccess_Init()
    {
        var events = new HmacEvents
        {
            OnAuthenticationSuccessAsync = (_, _) => Task.FromResult<Claim[]>([new Claim("Test", "Value")])
        };

        var claims = await events.OnAuthenticationSuccessAsync(new DefaultHttpContext(), new HmacResult(true, new Hmac { Policy = "" }));
        Assert.DoesNotThrow(() => claims.First(claim => claim.Type == "Test" && claim.Value == "Value"));
        Assert.That(events.OnAuthenticationFailureAsync, Is.EqualTo(HmacEventsDefaults.OnAuthenticationFailureAsync));
        Assert.That(events.OnValidateKeysAsync, Is.EqualTo(HmacEventsDefaults.OnValidateKeysAsync));
    }

    [Test]
    public async Task Test_HmacEvents_OnValidateKeys_Init()
    {
        var events = new HmacEvents 
        { 
            OnValidateKeysAsync = (_, _) => Task.FromResult(true) 
        };

        var isValid = await events.OnValidateKeysAsync(new DefaultHttpContext(), new KeyCredentials());
        Assert.That(isValid, Is.True);
        Assert.That(events.OnAuthenticationFailureAsync, Is.EqualTo(HmacEventsDefaults.OnAuthenticationFailureAsync));
        Assert.That(events.OnAuthenticationSuccessAsync, Is.EqualTo(HmacEventsDefaults.OnAuthenticationSuccessAsync));
    }
}