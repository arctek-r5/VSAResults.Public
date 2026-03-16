namespace VsaResults.Features.WideEvents.Unified;

/// <summary>
/// Interceptor for the wide event emission pipeline.
/// Allows enrichment, filtering, and transformation of events before emission.
/// </summary>
/// <remarks>
/// Interceptors are executed in order of <see cref="Order"/>.
/// Lower values execute first.
/// </remarks>
public interface IWideEventInterceptor
{
    /// <summary>
    /// Gets the execution order of this interceptor.
    /// Lower values execute first.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Called before a wide event is emitted.
    /// Can modify the event, filter it out (return null), or let it pass through.
    /// </summary>
    /// <param name="wideEvent">The wide event being emitted.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The (possibly modified) event to emit, or null to filter out the event.
    /// </returns>
    ValueTask<WideEvent?> OnBeforeEmitAsync(WideEvent wideEvent, CancellationToken ct);

    /// <summary>
    /// Called after a wide event has been emitted to all sinks.
    /// Cannot modify or filter the event at this stage.
    /// </summary>
    /// <param name="wideEvent">The wide event that was emitted.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask OnAfterEmitAsync(WideEvent wideEvent, CancellationToken ct);
}

/// <summary>
/// Base class for interceptors with default implementations.
/// </summary>
public abstract class WideEventInterceptorBase : IWideEventInterceptor
{
    /// <inheritdoc />
    public virtual int Order => 0;

    /// <inheritdoc />
    public virtual ValueTask<WideEvent?> OnBeforeEmitAsync(WideEvent wideEvent, CancellationToken ct)
    {
        return ValueTask.FromResult<WideEvent?>(wideEvent);
    }

    /// <inheritdoc />
    public virtual ValueTask OnAfterEmitAsync(WideEvent wideEvent, CancellationToken ct)
    {
        return ValueTask.CompletedTask;
    }
}
