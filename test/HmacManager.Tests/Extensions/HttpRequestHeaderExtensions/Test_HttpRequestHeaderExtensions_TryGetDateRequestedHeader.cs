// using HmacManager.Components;
// using HmacManager.Extensions;

// namespace Unit.Tests;

// [TestFixture]
// public class Test_HttpRequestHeaderExtensions_TryGetDateRequestedHeader : TestBase
// {
//     [Test]
//     public void Test()
//     {
//         var request = new HttpRequestMessage();
//         var hmac = new Hmac { Signature = "SomeSignature" };

//         request.Headers.AddRequiredHeaders(hmac, "SomePolicy");
//         var isSuccess = request.Headers.TryGetDateRequestedHeader(out var dateRequestedHeaderValue);

//         Assert.That(isSuccess, Is.True);
//         Assert.That(hmac.DateRequested.Equals(dateRequestedHeaderValue));
//     }
// }