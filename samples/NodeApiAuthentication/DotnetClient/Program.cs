using Microsoft.Extensions.DependencyInjection;
using HmacManager.Mvc.Extensions;

// A .NET client signing requests to an API that is not .NET.
//
// Nothing here is aware that the verifier is Node. Both ends build the same signing
// content, so the caller's configuration is the same as it would be against an
// ASP.NET Core API — which is the whole claim this sample exists to demonstrate.

var apiUrl = Environment.GetEnvironmentVariable("API_URL") ?? "http://localhost:5200";

var services = new ServiceCollection();

// The same policy the Node API declares, written out independently. Note the shape is
// identical to every other sample: the policy does not know what will verify it.
services.AddHmacManager(options =>
{
    options.AddPolicy("MyPolicy", policy =>
    {
        policy.UsePublicKey(Guid.Parse("eb8e9dae-08bd-4883-80fe-1d9a103b30b5"));
        policy.UsePrivateKey("dGhpc0lzTXlTdXBlckNvb2xQcml2YXRlS2V5");
        policy.UseMemoryCache(30);
        policy.AddScheme("RequireAccountAndEmail", scheme =>
        {
            scheme.AddHeader("X-Account");
            scheme.AddHeader("X-Email");
        });
    });
});

// The handler signs every outgoing request on this client, after the BaseAddress has
// been applied — which matters, because the signing content covers the host and a
// relative URI has none to cover.
services
    .AddHttpClient("Hmac_MyPolicy_RequireAccountAndEmail", client =>
    {
        client.BaseAddress = new Uri(apiUrl);
    })
    .AddHmacHttpMessageHandler("MyPolicy", "RequireAccountAndEmail");

var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IHttpClientFactory>()
    .CreateClient("Hmac_MyPolicy_RequireAccountAndEmail");

async Task Call(string label, HttpRequestMessage request)
{
    // The scheme's headers have to be on the request before the handler signs it.
    request.Headers.Add("X-Account", "myAccountId");
    request.Headers.Add("X-Email", "someone@example.com");

    var response = await client.SendAsync(request);
    Console.WriteLine($"{label}: {(int)response.StatusCode}");
    Console.WriteLine(await response.Content.ReadAsStringAsync());
}

await Call("GET ", new HttpRequestMessage(HttpMethod.Get, "api/weatherforecast"));

await Call("POST", new HttpRequestMessage(HttpMethod.Post, "api/weatherforecast")
{
    Content = new StringContent(
        "{\"summary\":\"This is a test.\"}",
        System.Text.Encoding.UTF8,
        "application/json")
});
