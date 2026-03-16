namespace VsaResults.Features.WideEvents.Unified.Sinks;

/// <summary>
/// A sink that writes to multiple underlying sinks.
/// </summary>
public sealed class CompositeWideEventSink : IWideEventSink
{
    private readonly IWideEventSink[] _sinks;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeWideEventSink"/> class.
    /// </summary>
    /// <param name="sinks">The sinks to write to.</param>
    public CompositeWideEventSink(params IWideEventSink[] sinks)
    {
        _sinks = sinks;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeWideEventSink"/> class.
    /// </summary>
    /// <param name="sinks">The sinks to write to.</param>
    public CompositeWideEventSink(IEnumerable<IWideEventSink> sinks)
    {
        _sinks = [.. sinks];
    }

    /// <inheritdoc />
    public async ValueTask WriteAsync(WideEvent wideEvent, CancellationToken ct = default)
    {
        foreach (var sink in _sinks)
        {
            await sink.WriteAsync(wideEvent, ct).ConfigureAwait(false);
        }
    }
}
