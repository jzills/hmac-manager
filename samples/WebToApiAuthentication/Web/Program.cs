using System.Text;
using HmacManager.Mvc.Extensions;

var builder = WebApplication.CreateBuilder(args);

var apiUrl = builder.Configuration["ApiUrl"] ?? "http://localhost:5100";

// Attaching the handler is the whole client side: every request this named
// client sends is signed on its way out. Nothing in the calling code below
// mentions HMAC.
builder.Services
    .AddHttpClient("WeatherApi", client => client.BaseAddress = new Uri(apiUrl))
    .AddHmacHttpMessageHandler("MyPolicy", "RequireAccountAndEmail");

// The signing half of the same policy the Api verifies with.
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

    using var get = new HttpRequestMessage(HttpMethod.Get, "api/weatherforecast");

    // The scheme's headers have to be on the request before it is signed —
    // their values are part of the signature.
    get.Headers.Add("X-Account", "myAccountId");
    get.Headers.Add("X-Email", "someone@example.com");

    var forecasts = await client.SendAsync(get);

    using var post = new HttpRequestMessage(HttpMethod.Post, "api/weatherforecast")
    {
        Content = JsonContent.Create(new { Summary = "Signed, body and all." })
    };

    post.Headers.Add("X-Account", "myAccountId");
    post.Headers.Add("X-Email", "someone@example.com");

    var created = await client.SendAsync(post);

    return Results.Json(new
    {
        Get = new { Status = (int)forecasts.StatusCode, Body = await forecasts.Content.ReadAsStringAsync() },
        Post = new { Status = (int)created.StatusCode, Body = await created.Content.ReadAsStringAsync() }
    });
});

app.Run();
