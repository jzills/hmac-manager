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
public class Test_HmacAuthenticationHandler_UnknownPolicy : TestServiceCollection
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
    public async Task Test_UnknownPolicy_FailsAuthentication_DoesNotThrow()
    {
        var uri = new Uri("https://localhost:1122/api/endpoint");
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Add("Scheme_Header_1", "Scheme_Header_Value_1");

        var hmacManager = HmacManagerFactory.Create(
            PolicySchemeType.Policy_Memory_Scheme_1.Policy,
            PolicySchemeType.Policy_Memory_Scheme_1.Scheme
        );

        var signingResult = await hmacManager!.SignAsync(request);
        FilterContext.HttpContext.Request.ConfigureFor(uri, HttpMethod.Get);
        FilterContext.HttpContext.Request.AddHmacHeaders(signingResult);
        FilterContext.HttpContext.Request.Headers.Append("Scheme_Header_1", "Scheme_Header_Value_1");
        FilterContext.HttpContext.Request.Headers[HmacAuthenticationDefaults.Headers.Policy] = "ThisPolicyIsNotRegistered";

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
