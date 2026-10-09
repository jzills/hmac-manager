// using HmacManager.Components;
// using HmacManager.Extensions;
// using HmacManager.Mvc;

// namespace Unit.Tests;

// [TestFixture]
// public class Test_HttpRequestHeaderExtensions_AddRequiredHeaders_DateRequested : TestBase
// {
//     [Test]
//     public void Test()
//     {
//         var request = new HttpRequestMessage();
//         var hmac = new Hmac { Signature = "SomeSignature" };

//         request.Headers.AddRequiredHeaders(hmac, "SomePolicy");
//         var dateRequestedHeaderValue = request.Headers.GetValues(HmacAuthenticationDefaults.Headers.DateRequested).First();
        
//         Assert.That(hmac.DateRequested.Equals(DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(dateRequestedHeaderValue))));
//     }
// }