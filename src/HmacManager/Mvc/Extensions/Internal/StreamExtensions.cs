namespace HmacManager.Mvc.Extensions.Internal;

/// <summary>
/// Provides extension methods for the <see cref="Stream"/> class.
/// </summary>
internal static class StreamExtensions
{
    /// <summary>
    /// Rewinds the stream to the beginning if the stream supports seeking.
    /// </summary>
    /// <param name="stream">The stream to rewind.</param>
    /// <remarks>
    /// This method attempts to reset the position of the stream to the beginning
    /// using <see cref="Stream.Seek"/> if the stream is seekable (i.e., <see cref="Stream.CanSeek"/> is true).
    /// If the stream is not seekable, no action is taken.
    /// </remarks>
    internal static void Rewind(this Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }
    }
}