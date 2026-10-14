using HmacManager.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using Unit.Tests.Common;

namespace Unit.Tests.Mvc;

[TestFixture]
public class Test_HmacAuthorizationHandler_OnAuthorizationAsync : TestServiceCollection
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
    public async Task Test_OnAuthorizeAsync_With_ExistingPolicy()
    {
        var hmacAuthorizationFilter = new HmacAuthenticateAttribute 
        { 
            Policy = PolicySchemeType.Policy_Memory_Scheme_1.Policy,
            Scheme = PolicySchemeType.Policy_Memory_Scheme_1.Scheme
        };

        FilterContext.Filters.Add(hmacAuthorizationFilter);

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

        await hmacAuthorizationFilter.OnAuthorizationAsync(FilterContext);

        var result = FilterContext.Result as StatusCodeResult;
        Assert.That(result, Is.Null);
    }
    
    [Test]
    public async Task Test_OnAuthorizeAsync_With_NonExistentPolicy()
    {
        var hmacAuthorizationFilter = new HmacAuthenticateAttribute { Policy = "Some_Policy_That_Doesn't_Exist" };
        FilterContext.Filters.Add(hmacAuthorizationFilter);

        await hmacAuthorizationFilter.OnAuthorizationAsync(FilterContext);

        var result = FilterContext.Result as UnauthorizedResult;
        Assert.That(result, Is.Not.Null);
        Assert.That(result.StatusCode, Is.EqualTo(401));
    }
}