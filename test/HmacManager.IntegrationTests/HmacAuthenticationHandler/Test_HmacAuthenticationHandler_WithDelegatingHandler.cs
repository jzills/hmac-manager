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
public class Test_HmacAuthenticationHandler_WithDelegatingHandler
{
    TestServer Server;
    HttpClient Client;

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

                    // Echoes the body the endpoint received, and whether it arrived with a length.
                    endpoints.MapPost("/echo", async (HttpContext context) =>
                    {
                        using var reader = new StreamReader(context.Request.Body);
                        var length = context.Request.ContentLength?.ToString() ?? "none";
                        await context.Response.WriteAsync($"{length}|{await reader.ReadToEndAsync()}");
                    }).RequireAuthorization("TestPolicy");
                });
            });

        Server = new TestServer(builder);

        var hmacManager = Server.Services
            .GetRequiredService<IHmacManagerFactory>()
            .Create("MyPolicy", "RequireAccountAndEmail")!;

        var handler = new HmacDelegatingHandler(hmacManager) { InnerHandler = Server.CreateHandler() };
        Client = new HttpClient(handler) { BaseAddress = Server.BaseAddress };
    }

    [Test]
    public async Task Test_Get()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api");

        request.Headers.Add("X-Account", "myAccount");
        request.Headers.Add("X-Email", "myEmail");

        var response = await Client.SendAsync(request);
        Assert.That(response.IsSuccessStatusCode);
    }

    [Test]
    public async Task Test_Post()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api")
        {
            Content = new StringContent(JsonConvert.SerializeObject("TestContent"), Encoding.UTF8, "application/json")
        };

        request.Headers.Add("X-Account", "myAccount");
        request.Headers.Add("X-Email", "myEmail");

        var response = await Client.SendAsync(request);
        Assert.That(response.IsSuccessStatusCode);
    }

    // A chunked upload has no Content-Length, so the server cannot know the body's length
    // without reading it. The handler must buffer it before reading, or the signature is
    // computed over an empty body and the endpoint receives nothing.
    [TestCase("{\"id\":1}")]
    [TestCase("")]
    public async Task Test_Post_ChunkedBody(string body)
    {
        // Signing buffers the body, after which HttpClient would send a Content-Length. Re-stream
        // it below the signer, as a proxy that re-chunks would, so the server sees no length.
        var hmacManager = Server.Services
            .GetRequiredService<IHmacManagerFactory>()
            .Create("MyPolicy", "RequireAccountAndEmail")!;
        var rechunk = new RechunkHandler { InnerHandler = Server.CreateHandler() };
        using var client = new HttpClient(new HmacDelegatingHandler(hmacManager) { InnerHandler = rechunk })
        {
            BaseAddress = Server.BaseAddress
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/echo")
        {
            Content = new StreamContent(new NonSeekableStream(Encoding.UTF8.GetBytes(body)))
        };
        request.Headers.Add("X-Account", "myAccount");
        request.Headers.Add("X-Email", "myEmail");

        var response = await client.SendAsync(request);

        Assert.Multiple(async () =>
        {
            Assert.That(response.IsSuccessStatusCode, Is.True);
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo($"none|{body}"));
        });
    }

    private sealed class RechunkHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var bytes = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            request.Content = new StreamContent(new NonSeekableStream(bytes));
            request.Headers.TransferEncodingChunked = true;
            return await base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class NonSeekableStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream _inner = new(bytes);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
