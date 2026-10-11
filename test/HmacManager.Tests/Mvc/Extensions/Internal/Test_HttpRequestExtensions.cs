using HmacManager.Mvc.Extensions.Internal;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace Unit.Tests.Mvc.Extensions.Internal;

[TestFixture]
public class Test_HttpRequestExtensions
{
    [Test]
    public void HasContent_WithNullBody_ReturnsFalse()
    {
        var context = new DefaultHttpContext();
        context.Request.Body = null!;
        
        var result = context.Request.HasContent();
        
        Assert.IsFalse(result);
    }

    [Test]
    public void HasContent_WithEmptyMemoryStream_ReturnsFalse()
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream();
        context.Request.ContentLength = 0;
        
        var result = context.Request.HasContent();
        
        Assert.IsFalse(result);
    }

    [Test]
    public void HasContent_WithContentLengthGreaterThanZero_ReturnsTrue()
    {
        var context = new DefaultHttpContext();
        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        context.Request.Body = stream;
        context.Request.ContentLength = 3;
        
        var result = context.Request.HasContent();
        
        Assert.IsTrue(result);
    }

    [Test]
    public void HasContent_WithSeekableStreamWithContent_ReturnsTrue()
    {
        var context = new DefaultHttpContext();
        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        stream.Seek(0, SeekOrigin.Begin);
        context.Request.Body = stream;
        // Don't set ContentLength, so it tests the CanSeek path
        
        var result = context.Request.HasContent();
        
        Assert.IsTrue(result);
    }

    [Test]
    public void HasContent_WithSeekableEmptyStream_ReturnsFalse()
    {
        var context = new DefaultHttpContext();
        var stream = new MemoryStream();
        context.Request.Body = stream;
        
        var result = context.Request.HasContent();
        
        Assert.IsFalse(result);
    }

    [Test]
    public void HasContent_WithNonSeekableStreamWithContent_ReturnsTrue()
    {
        var context = new DefaultHttpContext();
        var memStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var nonSeekableStream = new NonSeekableStreamWrapper(memStream);
        context.Request.Body = nonSeekableStream;
        
        var result = context.Request.HasContent();
        
        Assert.IsTrue(result);
    }

    [Test]
    public void HasContent_WithNonSeekableEmptyStream_ReturnsFalse()
    {
        var context = new DefaultHttpContext();
        var memStream = new MemoryStream();
        var nonSeekableStream = new NonSeekableStreamWrapper(memStream);
        context.Request.Body = nonSeekableStream;
        
        var result = context.Request.HasContent();
        
        Assert.IsFalse(result);
    }

    /// <summary>
    /// A wrapper stream that disables seeking to test the non-seekable code path.
    /// </summary>
    private class NonSeekableStreamWrapper : Stream
    {
        private readonly Stream _innerStream;

        public NonSeekableStreamWrapper(Stream innerStream)
        {
            _innerStream = innerStream;
        }

        public override bool CanRead => _innerStream.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => _innerStream.CanWrite;
        public override long Length => _innerStream.Length;
        public override long Position
        {
            get => _innerStream.Position;
            set => _innerStream.Position = value;
        }

        public override void Flush() => _innerStream.Flush();
        public override int Read(byte[] buffer, int offset, int count) =>
            _innerStream.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();
        public override void SetLength(long value) => _innerStream.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) =>
            _innerStream.Write(buffer, offset, count);
    }
}
