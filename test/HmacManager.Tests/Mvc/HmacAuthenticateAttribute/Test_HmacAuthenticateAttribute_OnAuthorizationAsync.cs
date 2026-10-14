using HmacManager.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using NUnit.Framework.Constraints;

namespace Unit.Tests.Mvc;

[TestFixture]
public class Test_HmacAuthenticateAttribute_OnAuthorizationAsync : TestServiceCollection
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
    [TestCaseSource(nameof(GetPolicySchemes))]
    public void Test_IsMatch(
        string initPolicy, 
        string? initScheme,
        string? verifyPolicy,
        string? verifyScheme,
        IResolveConstraint expected
    )
    {
        var hmacAuthenticateAttribute = new HmacAuthenticateAttribute 
        { 
            Policy = initPolicy, 
            Scheme = initScheme 
        };

        var isMatch = hmacAuthenticateAttribute.IsMatch(verifyPolicy, verifyScheme);
        Assert.That(isMatch, expected);
    }

    public static IEnumerable<TestCaseData> GetPolicySchemes() =>
    [
        new TestCaseData(
            PolicySchemeType.Policy_Memory_Scheme_1.Policy, 
            PolicySchemeType.Policy_Memory_Scheme_1.Scheme,
            PolicySchemeType.Policy_Memory_Scheme_1.Policy, 
            PolicySchemeType.Policy_Memory_Scheme_1.Scheme,
            Is.True
        ),
        new TestCaseData(
            PolicySchemeType.Policy_Memory_Scheme_1.Policy, 
            null,
            PolicySchemeType.Policy_Memory_Scheme_1.Policy, 
            null,
            Is.True
        ),
        new TestCaseData(
            PolicySchemeType.Policy_Memory_Scheme_1.Policy, 
            PolicySchemeType.Policy_Memory_Scheme_1.Scheme,
            string.Empty,
            string.Empty,
            Is.False
        ),
        new TestCaseData(
            PolicySchemeType.Policy_Memory_Scheme_1.Policy, 
            PolicySchemeType.Policy_Memory_Scheme_1.Scheme,
            null,
            null,
            Is.False
        ),
        new TestCaseData(
            null,
            null,
            null,
            null,
            Is.False
        ),
        new TestCaseData(
            PolicySchemeType.Policy_Memory_Scheme_1.Policy, 
            PolicySchemeType.Policy_Memory_Scheme_1.Scheme,
            null,
            PolicySchemeType.Policy_Memory_Scheme_1.Scheme,
            Is.False
        ),
        new TestCaseData(
            PolicySchemeType.Policy_Memory_Scheme_1.Policy, 
            PolicySchemeType.Policy_Memory_Scheme_1.Scheme,
            PolicySchemeType.Policy_Memory_Scheme_1.Policy,
            null,
            Is.False
        ),
        new TestCaseData(
            PolicySchemeType.Policy_Memory_Scheme_1.Policy, 
            PolicySchemeType.Policy_Memory_Scheme_1.Scheme,
            $" {PolicySchemeType.Policy_Memory_Scheme_1.Policy}",
            PolicySchemeType.Policy_Memory_Scheme_1.Scheme,
            Is.False
        ),
        new TestCaseData(
            PolicySchemeType.Policy_Memory_Scheme_1.Policy, 
            PolicySchemeType.Policy_Memory_Scheme_1.Scheme,
            PolicySchemeType.Policy_Memory_Scheme_1.Policy,
            $" {PolicySchemeType.Policy_Memory_Scheme_1.Scheme}",
            Is.False
        ),
        new TestCaseData(
            PolicySchemeType.Policy_Memory_Scheme_1.Policy, 
            PolicySchemeType.Policy_Memory_Scheme_1.Scheme,
            $"{PolicySchemeType.Policy_Memory_Scheme_1.Policy} ",
            PolicySchemeType.Policy_Memory_Scheme_1.Scheme,
            Is.False
        ),
        new TestCaseData(
            PolicySchemeType.Policy_Memory_Scheme_1.Policy, 
            PolicySchemeType.Policy_Memory_Scheme_1.Scheme,
            PolicySchemeType.Policy_Memory_Scheme_1.Policy,
            $"{PolicySchemeType.Policy_Memory_Scheme_1.Scheme} ",
            Is.False
        ),
        new TestCaseData(
            PolicySchemeType.Policy_Memory_Scheme_1.Policy, 
            PolicySchemeType.Policy_Memory_Scheme_1.Scheme,
            PolicySchemeType.Policy_Distributed_Scheme_1.Policy,
            PolicySchemeType.Policy_Memory_Scheme_1.Scheme,
            Is.False
        ),
    ];
}