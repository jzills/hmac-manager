using HmacManager.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Unit.Tests.Common;

namespace Unit.Tests.Mvc;

/// <summary>
/// A request may name a policy that exists and a scheme that policy does not declare. The factory
/// has no manager to give for that pair, so the authentication context carries a null manager and
/// the verification call would dereference it.
///
/// The scheme name arrives on a request header, so an unauthenticated caller controls it. Failing
/// authentication is the correct outcome; throwing would turn a mistyped header into a 500 that any
/// caller could trigger at will.
/// </summary>
[TestFixture]
public class Test_HmacAuthenticationHandler_UndeclaredScheme : TestServiceCollection
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
    public async Task Test_UndeclaredScheme_FailsAuthentication_DoesNotThrow()
    {
        var uri = new Uri("https://localhost:1122/api/endpoint");
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Add("Scheme_Header_1", "Scheme_Header_Value_1");

        // Sign properly, so everything except the scheme name is valid and the request gets past
        // header parsing to the point where the manager is resolved.
        var hmacManager = HmacManagerFactory.Create(
            PolicySchemeType.Policy_Memory_Scheme_1.Policy,
            PolicySchemeType.Policy_Memory_Scheme_1.Scheme
        );

        var signingResult = await hmacManager!.SignAsync(request);
        FilterContext.HttpContext.Request.ConfigureFor(uri, HttpMethod.Get);
        FilterContext.HttpContext.Request.AddHmacHeaders(signingResult);
        FilterContext.HttpContext.Request.Headers.Append("Scheme_Header_1", "Scheme_Header_Value_1");

        // Then name a scheme the policy does not declare.
        FilterContext.HttpContext.Request.Headers[HmacAuthenticationDefaults.Headers.Scheme] =
            "ThisSchemeIsNotDeclared";

        AuthenticateResult? authenticateResult = null;
        Assert.DoesNotThrowAsync(
            async () => authenticateResult = await FilterContext.HttpContext
                .AuthenticateAsync(HmacAuthenticationDefaults.AuthenticationScheme),
            "An undeclared scheme name must not throw — the name is caller-supplied, so throwing " +
            "would let any caller turn a mistyped header into a server error.");

        Assert.Multiple(() =>
        {
            Assert.That(authenticateResult, Is.Not.Null);
            Assert.That(authenticateResult!.Succeeded, Is.False);
            Assert.That(authenticateResult.Failure, Is.Not.Null);
        });
    }
}
