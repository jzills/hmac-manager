using Microsoft.Extensions.Logging;

namespace Unit.Tests;

/// <summary>
/// A single entry captured by a <see cref="RecordingLogger{T}"/>.
/// </summary>
public sealed record RecordedLog(LogLevel Level, EventId EventId, string Message, Exception? Exception);

/// <summary>
/// An <see cref="ILogger{TCategoryName}"/> that keeps every message it is given.
/// </summary>
/// <remarks>
/// <see cref="IsEnabled"/> is always true, including for <see cref="LogLevel.Trace"/>, so assertions
/// about what the library must never write see everything it could possibly write.
/// </remarks>
public sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly List<RecordedLog> _entries = [];

    public IReadOnlyList<RecordedLog> Entries => _entries;

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
    ) => _entries.Add(new RecordedLog(logLevel, eventId, formatter(state, exception), exception));
}

/// <summary>
/// An <see cref="ILoggerFactory"/> that hands out one supplied <see cref="ILogger"/> for every
/// category, so a component that builds its own loggers from an injected factory (like
/// <see cref="HmacManager.Components.HmacManagerFactory"/>) can still be observed in a test.
/// </summary>
public sealed class RecordingLoggerFactory(ILogger logger) : ILoggerFactory
{
    public ILogger CreateLogger(string categoryName) => logger;

    public void AddProvider(ILoggerProvider provider) { }

    public void Dispose() { }
}
