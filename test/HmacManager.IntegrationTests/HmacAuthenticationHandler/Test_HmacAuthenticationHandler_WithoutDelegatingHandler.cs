using HmacManager.Components;
using HmacManager.Mvc;
using HmacManager.Mvc.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Text;

[TestFixture]
public class Test_HmacAuthenticationHandler_WithoutDelegatingHandler
{
    TestServer Server;
    HttpClient Client;
    IHmacManager HmacManager;

    [SetUp]
    public void Setup()
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services => 
            {
                services.AddRouting();
                services.AddMemoryCache();
                services.AddAuthentication().AddHmac(options =>
                {
                    options.EnableConsolidatedHeaders();
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

                services.AddAuthorization(options => 
                    options.AddPolicy("TestPolicy", policy => 
                        policy.RequireHmacAuthentication("MyPolicy", "RequireAccountAndEmail")));
            })
            .Configure(app =>
            {
                app.UseHttpsRedirection();
                app.UseRouting();

                app.UseAuthentication();
                app.UseAuthorization();

                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/api", async (HttpContext context) =>
                    {
                        await context.Response.WriteAsync("Ok!");
                    }).RequireAuthorization("TestPolicy"); 

                    endpoints.MapPost("/api", async (HttpContext context, [FromBody] string body) =>
                    {
                        await context.Response.WriteAsync("Ok!");
                    }).RequireAuthorization("TestPolicy"); 
                });
            });

        Server = new TestServer(builder);
        Client = Server.CreateClient();
        HmacManager = Server.Services.GetRequiredService<IHmacManagerFactory>()
            .Create("MyPolicy", "RequireAccountAndEmail")!;
    }

    [Test]
    public async Task Test_Get()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{Client.BaseAddress}api");

        request.Headers.Add("X-Account", "myAccount");
        request.Headers.Add("X-Email", "myEmail");

        await HmacManager.SignAsync(request);

        var response = await Client.SendAsync(request);
        Assert.That(response.IsSuccessStatusCode);
    }

    [Test]
    public async Task Test_Post()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{Client.BaseAddress}api")
        {
            Content = new StringContent(JsonConvert.SerializeObject("TestContent"), Encoding.UTF8, "application/json")
        };

        request.Headers.Add("X-Account", "myAccount");
        request.Headers.Add("X-Email", "myEmail");

        await HmacManager.SignAsync(request);

        var response = await Client.SendAsync(request);
        Assert.That(response.IsSuccessStatusCode);
    }
}
