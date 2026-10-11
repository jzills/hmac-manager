using HmacManager.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using System.Text;
using System.Text.Json;
using Unit.Tests.Common;

namespace Unit.Tests.Mvc;

[TestFixture]
public class Test_HmacAuthenticationHandler_HandleAuthenticateAsync_Post : TestServiceCollection
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
    public async Task Test()
    {
        var hmacAuthorizationFilter = new HmacAuthenticateAttribute 
        { 
            Policy = PolicySchemeType.Policy_Memory_Scheme_1.Policy,
            Scheme = PolicySchemeType.Policy_Memory_Scheme_1.Scheme
        };

        FilterContext.Filters.Add(hmacAuthorizationFilter);

        var uri = new Uri("https://localhost:1122/api/endpoint");
        var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {

            }), Encoding.UTF8, "application/json")
        };

        request.Headers.Add("Scheme_Header_1", "Scheme_Header_Value_1");

        var hmacManager = HmacManagerFactory.Create(
            PolicySchemeType.Policy_Memory_Scheme_1.Policy,
            PolicySchemeType.Policy_Memory_Scheme_1.Scheme
        );

        var signingResult = await hmacManager!.SignAsync(request);

        var body = new MemoryStream();
        await request.Content.CopyToAsync(body);
        body.Position = 0;
        FilterContext.HttpContext.Request.ConfigureFor(uri, HttpMethod.Post, body);
        FilterContext.HttpContext.Request.AddHmacHeaders(signingResult);
        FilterContext.HttpContext.Request.Headers.Append("Scheme_Header_1", "Scheme_Header_Value_1");

        string? policy = null;
        string? scheme = null;
        var authenticateResult = await FilterContext.HttpContext.AuthenticateAsync(HmacAuthenticationDefaults.AuthenticationScheme);
        var hasPolicyProperty = authenticateResult.Properties?.Items.TryGetValue(HmacAuthenticationDefaults.Properties.PolicyProperty, out policy) ?? false;
        var hasSchemeProperty = authenticateResult.Properties?.Items.TryGetValue(HmacAuthenticationDefaults.Properties.SchemeProperty, out scheme) ?? false;
        Assert.That(authenticateResult.Succeeded);
        Assert.That(hasPolicyProperty);
        Assert.That(hasSchemeProperty);
        Assert.That(policy, Is.EqualTo(PolicySchemeType.Policy_Memory_Scheme_1.Policy));
        Assert.That(scheme, Is.EqualTo(PolicySchemeType.Policy_Memory_Scheme_1.Scheme));
    }
}