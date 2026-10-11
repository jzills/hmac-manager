// using HmacManager.Extensions;
// using HmacManager.Headers;

// namespace Unit.Tests;

// [TestFixture]
// public class Test_HttpRequestHeaderExtensions_TryParseHmac : TestServiceCollection
// {
//     [Test]
//     public async Task Test()
//     {
//         var scheme = new Scheme(PolicySchemeType.Policy_Memory_Scheme_1.Scheme);
//         scheme.AddHeader("Scheme_Header_1");

//         var request = new HttpRequestMessage(HttpMethod.Get, "/api/something");
//         request.Headers.Add("Scheme_Header_1", "Value");

//         var signingResult = await HmacManagerFactory
//             .Create(PolicySchemeType.Policy_Memory_Scheme_1.Policy, PolicySchemeType.Policy_Memory_Scheme_1.Scheme)!
//             .SignAsync(request);

//         var hasHeaderValues = request.Headers.TryParseHmac(scheme, 30, out var hmac);
//         Assert.IsTrue(hasHeaderValues);
//         Assert.That(signingResult.Hmac!.HeaderValues.Count, Is.EqualTo(hmac.HeaderValues.Count));

//         foreach (var (signingHeaderValue, headerValue) in 
//             signingResult.Hmac!.HeaderValues.Zip(hmac.HeaderValues))
//         {
//             Assert.That(signingHeaderValue.Name, Is.EqualTo(headerValue.Name));
//             Assert.That(signingHeaderValue.ClaimType, Is.EqualTo(headerValue.ClaimType));
//             Assert.That(signingHeaderValue.Value, Is.EqualTo(headerValue.Value));
//         }
//     }
// }