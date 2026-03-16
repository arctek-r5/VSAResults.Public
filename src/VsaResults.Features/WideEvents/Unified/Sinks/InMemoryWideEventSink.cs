namespace VsaResults.Features.WideEvents.Unified.Sinks;

/// <summary>
/// A sink that writes events to an in-memory list.
/// Useful for testing and debugging.
/// </summary>
public sealed class InMemoryWideEventSink : IWideEventSink
{
    private readonly List<WideEvent> _events = [];
    private readonly Lock _lock = new();

    /// <summary>
    /// Gets the events that have been written to this sink.
    /// </summary>
    public IReadOnlyList<WideEvent> Events
    {
        get
        {
            lock (_lock)
            {
                return _events.ToList();
            }
        }
    }

    /// <summary>
    /// Gets the count of events written to this sink.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _events.Count;
            }
        }
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(WideEvent wideEvent, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _events.Add(wideEvent);
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Clears all captured events.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _events.Clear();
        }
    }
}
