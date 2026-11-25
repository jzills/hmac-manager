using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using HmacManager.Diagnostics;

namespace Unit.Tests;

/// <summary>
/// A single measurement captured by a <see cref="MetricsRecorder"/>.
/// </summary>
public sealed record RecordedMeasurement(
    string Instrument, double Value, IReadOnlyDictionary<string, object?> Tags)
{
    public object? Tag(string name) => Tags.TryGetValue(name, out var value) ? value : null;
}

/// <summary>
/// Listens to HmacManager's <see cref="Meter"/> for as long as it is alive and keeps every
/// measurement made on it.
/// </summary>
/// <remarks>
/// The meter is process-wide, so a recorder sees every measurement made while it listens, from any
/// test. The tests here do not run in parallel; where one still needs to tell its own measurements
/// apart, it filters on a policy name no other test uses.
/// </remarks>
public sealed class MetricsRecorder : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly ConcurrentQueue<RecordedMeasurement> _measurements = new();

    public MetricsRecorder()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == HmacManagerDiagnostics.MeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            Record(instrument, value, tags));
        _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            Record(instrument, value, tags));

        _listener.Start();
    }

    public IReadOnlyList<RecordedMeasurement> Measurements => _measurements.ToList();

    public IEnumerable<RecordedMeasurement> For(string instrument) =>
        _measurements.Where(measurement => measurement.Instrument == instrument);

    private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var copy = new Dictionary<string, object?>();
        foreach (var tag in tags)
        {
            copy[tag.Key] = tag.Value;
        }

        _measurements.Enqueue(new RecordedMeasurement(instrument.Name, value, copy));
    }

    public void Dispose() => _listener.Dispose();
}
