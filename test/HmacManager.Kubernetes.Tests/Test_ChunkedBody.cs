using System.Text;
using HmacManager.Components;
using HmacManager.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace HmacManager.Kubernetes.Tests;

/// <summary>
/// A body that reaches the service without a Content-Length (chunked, from any caller other than an
/// Envoy that buffered it first) must be hashed in full, not signed as absent.
/// </summary>
public class Test_ChunkedBody
{
    private WebApplicationFactory<Program> _factory = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // As a pod runs: appsettings.Development.json carries a placeholder policy whose
            // empty public key fails validation.
            builder.UseEnvironment("Production");

            builder.UseSetting("HmacManager:0:Name", "MyPolicy");
            builder.UseSetting("HmacManager:0:Keys:PublicKey", "eb8e9dae-08bd-4883-80fe-1d9a103b30b5");
            builder.UseSetting("HmacManager:0:Keys:PrivateKey", "Hii9mvaSlUm9RRLwsfuUcg==");
            builder.UseSetting("HmacManager:0:Nonce:CacheType", "Memory");
            builder.UseSetting("HmacManager:0:Nonce:MaxAgeInSeconds", "30");
        });
    }

    [TearDown]
    public void TearDown() => _factory.Dispose();

    [TestCase("{\"id\":1}")]
    [TestCase("")]
    public async Task Test_SignedChunkedPost_IsAllowed(string body)
    {
        // Signing with the service's own policy set: signing never touches the nonce cache.
        var hmacManager = _factory.Services.GetRequiredService<IHmacManagerFactory>().Create("MyPolicy")!;

        // Signing buffers the body, so without re-streaming it below the signer the client would
        // send a Content-Length.
        using var client = _factory.CreateDefaultClient(new HmacDelegatingHandler(hmacManager), new RechunkHandler());

        var response = await client.PostAsync("/api/orders", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.That((int)response.StatusCode, Is.EqualTo(200));
    }

    private sealed class RechunkHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var bytes = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            var content = new StreamContent(new NonSeekableStream(bytes));
            foreach (var (key, values) in request.Content.Headers)
            {
                if (key != "Content-Length") content.Headers.TryAddWithoutValidation(key, values);
            }
            request.Content = content;
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
