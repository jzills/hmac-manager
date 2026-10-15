using HmacManager.Mvc.Extensions.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using NUnit.Framework;
using Unit.Tests.Common;

namespace Unit.Tests.Mvc.Extensions.Internal;

[TestFixture]
public class Test_HttpRequestExtensions
{
    [Test]
    public void MayHaveContent_WithNullBody_ReturnsFalse()
    {
        var context = new DefaultHttpContext();
        context.Request.Body = null!;

        Assert.That(context.Request.MayHaveContent(), Is.False);
    }

    [Test]
    public void MayHaveContent_WithDefaultEmptyBody_ReturnsFalse()
    {
        var context = new DefaultHttpContext();

        Assert.That(context.Request.Body, Is.SameAs(Stream.Null));
        Assert.That(context.Request.MayHaveContent(), Is.False);
    }

    [Test]
    public void MayHaveContent_WithContentLengthZero_ReturnsFalse()
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream();
        context.Request.ContentLength = 0;

        Assert.That(context.Request.MayHaveContent(), Is.False);
    }

    [Test]
    public void MayHaveContent_WithContentLengthGreaterThanZero_ReturnsTrue()
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(new byte[] { 1, 2, 3 });
        context.Request.ContentLength = 3;

        Assert.That(context.Request.MayHaveContent(), Is.True);
    }

    // Unknown length: reported as possible content whatever the stream holds, because finding
    // out would mean reading it. Computing the signing content measures it after buffering.
    [TestCase(new byte[] { 1, 2, 3 })]
    [TestCase(new byte[0])]
    public void MayHaveContent_WithUnknownLength_ReturnsTrue_WithoutReadingTheBody(byte[] bytes)
    {
        var context = new DefaultHttpContext();
        var body = new CountingStream(new NonSeekableStream(bytes));
        context.Request.Body = body;

        Assert.Multiple(() =>
        {
            Assert.That(context.Request.MayHaveContent(), Is.True);
            Assert.That(body.Reads, Is.Zero);
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void MayHaveContent_WithUnknownLength_FollowsBodyDetection(bool canHaveBody)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new NonSeekableStream([]);
        context.Features.Set<IHttpRequestBodyDetectionFeature>(new BodyDetection(canHaveBody));

        Assert.That(context.Request.MayHaveContent(), Is.EqualTo(canHaveBody));
    }

    private sealed class BodyDetection(bool canHaveBody) : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => canHaveBody;
    }

    private sealed class CountingStream(Stream inner) : Stream
    {
        public int Reads { get; private set; }

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Reads++;
            return inner.Read(buffer, offset, count);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Reads++;
            return inner.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Reads++;
            return inner.ReadAsync(buffer, cancellationToken);
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
