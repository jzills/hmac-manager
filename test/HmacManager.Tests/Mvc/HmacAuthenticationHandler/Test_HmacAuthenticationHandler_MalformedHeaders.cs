using HmacManager.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Unit.Tests.Common;

namespace Unit.Tests.Mvc;

[TestFixture]
public class Test_HmacAuthenticationHandler_MalformedHeaders : TestServiceCollection
{
    public HttpContext HttpContext;
    public AuthorizationFilterContext FilterContext;

    [SetUp]
    public void Init()
    {
        HttpContext = new DefaultHttpContext { RequestServices = ServiceProvider };
        FilterContext = new AuthorizationFilterContext(
            new ActionContext(
                HttpContext,
                new RouteData(),
                new ActionDescriptor()
            ), []);
    }

    [Test]
    public void Test_MissingPolicyHeader_FailsAuthentication_DoesNotThrow()
    {
        var uri = new Uri("https://localhost:1122/api/endpoint");
        FilterContext.HttpContext.Request.ConfigureFor(uri, HttpMethod.Get);
        FilterContext.HttpContext.Request.Headers[HmacAuthenticationDefaults.Headers.Authorization] = "Hmac x";

        AssertFailsWithoutThrowing();
    }

    [Test]
    public void Test_MalformedPolicyHeader_FailsAuthentication_DoesNotThrow()
    {
        var uri = new Uri("https://localhost:1122/api/endpoint");
        FilterContext.HttpContext.Request.ConfigureFor(uri, HttpMethod.Get);
        FilterContext.HttpContext.Request.Headers[HmacAuthenticationDefaults.Headers.Authorization] = "Hmac x";
        FilterContext.HttpContext.Request.Headers[HmacAuthenticationDefaults.Headers.Policy] = "   ";

        AssertFailsWithoutThrowing();
    }

    private void AssertFailsWithoutThrowing()
    {
        AuthenticateResult? authenticateResult = null;
        Assert.DoesNotThrowAsync(async () => authenticateResult = await FilterContext.HttpContext
            .AuthenticateAsync(HmacAuthenticationDefaults.AuthenticationScheme));

        Assert.Multiple(() =>
        {
            Assert.That(authenticateResult, Is.Not.Null);
            Assert.That(authenticateResult!.Succeeded, Is.False);
            Assert.That(authenticateResult.Failure, Is.Not.Null);
        });
    }
}
