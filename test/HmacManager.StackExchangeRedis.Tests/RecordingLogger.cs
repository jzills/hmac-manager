using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace HmacManager.StackExchangeRedis.Tests;

/// <summary>
/// A single entry captured by a <see cref="RecordingLogger{T}"/>.
/// </summary>
public sealed record RecordedLog(LogLevel Level, EventId EventId, string Message, Exception? Exception);

/// <summary>
/// An <see cref="ILogger{TCategoryName}"/> that keeps every message it is given. Thread-safe,
/// because connection events arrive on StackExchange.Redis's own threads.
/// </summary>
public sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly ConcurrentQueue<RecordedLog> _entries = new();

    public IReadOnlyCollection<RecordedLog> Entries => _entries;

    public IEnumerable<RecordedLog> WithEventId(int eventId) =>
        _entries.Where(entry => entry.EventId.Id == eventId);

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    ) => _entries.Enqueue(new RecordedLog(logLevel, eventId, formatter(state, exception), exception));
}
