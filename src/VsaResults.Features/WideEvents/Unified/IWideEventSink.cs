namespace VsaResults.Features.WideEvents.Unified;

/// <summary>
/// Abstraction for writing wide events to a destination.
/// Sinks are the final stage of the emission pipeline.
/// </summary>
/// <remarks>
/// Sinks should be stateless and thread-safe.
/// Multiple sinks can be registered to fan out events.
/// </remarks>
public interface IWideEventSink
{
    /// <summary>
    /// Writes a wide event to the sink's destination.
    /// </summary>
    /// <param name="wideEvent">The wide event to write.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask WriteAsync(WideEvent wideEvent, CancellationToken ct = default);
}
