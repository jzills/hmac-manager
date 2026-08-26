using System.Text;
using HmacManager.Mvc.Extensions;

var builder = WebApplication.CreateBuilder(args);

var apiUrl = builder.Configuration["ApiUrl"] ?? "http://localhost:5120";

builder.Services
    .AddHttpClient("WeatherApi", client => client.BaseAddress = new Uri(apiUrl))
    .AddHmacHttpMessageHandler("MyPolicy", "RequireAccountAndEmail");

// This client only holds MyPolicy's key. It has no way to sign as AdminPolicy,
// which is the point of the 403 below.
builder.Services.AddHmacManager(options =>
{
    options.AddPolicy("MyPolicy", policy =>
    {
        policy.UsePublicKey(Guid.Parse("eb8e9dae-08bd-4883-80fe-1d9a103b30b5"));
        policy.UsePrivateKey(Convert.ToBase64String(Encoding.UTF8.GetBytes("thisIsMySuperCoolPrivateKey")));
        policy.UseMemoryCache(30);
        policy.AddScheme("RequireAccountAndEmail", scheme =>
        {
            scheme.AddHeader("X-Account");
            scheme.AddHeader("X-Email");
        });
    });
});

var app = builder.Build();

app.MapGet("/", async (IHttpClientFactory clientFactory) =>
{
    var client = clientFactory.CreateClient("WeatherApi");

    async Task<object> Call(HttpMethod method)
    {
        using var request = new HttpRequestMessage(method, "api/weatherforecast");
        request.Headers.Add("X-Account", "myAccountId");
        request.Headers.Add("X-Email", "someone@example.com");

        var response = await client.SendAsync(request);
        return new { Status = (int)response.StatusCode, Body = await response.Content.ReadAsStringAsync() };
    }

    return Results.Json(new
    {
        // Requires MyPolicy — which is what this client signs with. 200.
        Get = await Call(HttpMethod.Get),

        // Requires AdminPolicy. Authenticated, but not authorized. 403.
        Delete = await Call(HttpMethod.Delete)
    });
});

app.Run();
