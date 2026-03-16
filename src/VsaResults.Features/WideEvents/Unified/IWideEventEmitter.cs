namespace VsaResults.Features.WideEvents.Unified;

/// <summary>
/// Abstraction for emitting wide events.
/// Handles scope integration, interceptor pipeline, and routing to sinks.
/// </summary>
public interface IWideEventEmitter
{
    /// <summary>
    /// Emits a wide event.
    /// If a <see cref="WideEventScope"/> is active, the event may be captured
    /// as a child span depending on the aggregation mode.
    /// </summary>
    /// <param name="wideEvent">The wide event to emit.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask EmitAsync(WideEvent wideEvent, CancellationToken ct = default);

    /// <summary>
    /// Emits a wide event synchronously.
    /// If a <see cref="WideEventScope"/> is active, the event may be captured
    /// as a child span depending on the aggregation mode.
    /// </summary>
    /// <param name="wideEvent">The wide event to emit.</param>
    void Emit(WideEvent wideEvent);
}

/// <summary>
/// A null implementation that discards all events.
/// Used when WideEvents are not configured.
/// </summary>
public sealed class NullWideEventEmitter : IWideEventEmitter
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static readonly NullWideEventEmitter Instance = new();

    private NullWideEventEmitter()
    {
    }

    /// <inheritdoc />
    public ValueTask EmitAsync(WideEvent wideEvent, CancellationToken ct = default)
    {
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void Emit(WideEvent wideEvent)
    {
        // Intentionally empty - discards events
    }
}
