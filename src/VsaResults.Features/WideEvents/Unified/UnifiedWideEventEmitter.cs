using VsaResults.Features.WideEvents.Unified.Interceptors;

namespace VsaResults.Features.WideEvents.Unified;

/// <summary>
/// Default implementation of <see cref="IWideEventEmitter"/>.
/// Handles scope integration, interceptor pipeline, and routing to sinks.
/// </summary>
public sealed class UnifiedWideEventEmitter : IWideEventEmitter
{
    private readonly IWideEventInterceptor[] _interceptors;
    private readonly IWideEventSink _sink;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnifiedWideEventEmitter"/> class.
    /// </summary>
    /// <param name="sink">The sink to write events to.</param>
    /// <param name="interceptors">The interceptors to apply.</param>
    public UnifiedWideEventEmitter(IWideEventSink sink, IEnumerable<IWideEventInterceptor>? interceptors = null)
    {
        _sink = sink;
        _interceptors = interceptors?.OrderBy(i => i.Order).ToArray() ?? [];
    }

    /// <summary>
    /// Creates an emitter with default interceptors from options.
    /// </summary>
    /// <param name="sink">The sink to write events to.</param>
    /// <param name="options">The wide event options.</param>
    /// <returns>A configured emitter.</returns>
    public static UnifiedWideEventEmitter Create(IWideEventSink sink, WideEventOptions options)
    {
        var interceptors = new IWideEventInterceptor[]
        {
            new SamplingInterceptor(options),
            new RedactionInterceptor(options),
            new ContextLimitInterceptor(options),
            new VerbosityInterceptor(options),
        };

        return new UnifiedWideEventEmitter(sink, interceptors);
    }

    /// <inheritdoc />
    public async ValueTask EmitAsync(WideEvent wideEvent, CancellationToken ct = default)
    {
        // Check if there's an active scope that wants to capture this event
        if (WideEventScope.TryReportToCurrentScope(wideEvent))
        {
            // Event was captured as a child span - don't emit separately
            return;
        }

        // Run through interceptor pipeline
        var eventToEmit = wideEvent;
        foreach (var interceptor in _interceptors)
        {
            eventToEmit = await interceptor.OnBeforeEmitAsync(eventToEmit, ct).ConfigureAwait(false);
            if (eventToEmit == null)
            {
                // Event was filtered out
                return;
            }
        }

        // Write to sink
        await _sink.WriteAsync(eventToEmit, ct).ConfigureAwait(false);

        // Run after-emit hooks
        foreach (var interceptor in _interceptors)
        {
            await interceptor.OnAfterEmitAsync(eventToEmit, ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// The synchronous path requires all interceptors and the sink to complete synchronously.
    /// If any interceptor or the sink returns an incomplete task, an <see cref="InvalidOperationException"/>
    /// is thrown rather than blocking via <c>.GetAwaiter().GetResult()</c>, which risks deadlocks
    /// in ASP.NET Core and other SynchronizationContext-aware environments.
    /// </remarks>
    public void Emit(WideEvent wideEvent)
    {
        // Check if there's an active scope that wants to capture this event
        if (WideEventScope.TryReportToCurrentScope(wideEvent))
        {
            // Event was captured as a child span - don't emit separately
            return;
        }

        // Run through interceptor pipeline — must complete synchronously
        var eventToEmit = wideEvent;
        foreach (var interceptor in _interceptors)
        {
            var task = interceptor.OnBeforeEmitAsync(eventToEmit, CancellationToken.None);
            if (!task.IsCompleted)
            {
                throw new InvalidOperationException(
                    $"Wide event interceptor '{interceptor.GetType().Name}' returned an incomplete task from OnBeforeEmitAsync. " +
                    $"Interceptors used with the synchronous Emit() path must complete synchronously. " +
                    $"Use EmitAsync() instead, or ensure the interceptor completes without awaiting.");
            }

            eventToEmit = task.Result;

            if (eventToEmit == null)
            {
                // Event was filtered out
                return;
            }
        }

        // Write to sink — must complete synchronously
        var writeTask = _sink.WriteAsync(eventToEmit, CancellationToken.None);
        if (!writeTask.IsCompleted)
        {
            throw new InvalidOperationException(
                $"Wide event sink '{_sink.GetType().Name}' returned an incomplete task from WriteAsync. " +
                $"Sinks used with the synchronous Emit() path must complete synchronously. " +
                $"Use EmitAsync() instead.");
        }

        // Run after-emit hooks — must complete synchronously
        foreach (var interceptor in _interceptors)
        {
            var task = interceptor.OnAfterEmitAsync(eventToEmit, CancellationToken.None);
            if (!task.IsCompleted)
            {
                throw new InvalidOperationException(
                    $"Wide event interceptor '{interceptor.GetType().Name}' returned an incomplete task from OnAfterEmitAsync. " +
                    $"Interceptors used with the synchronous Emit() path must complete synchronously. " +
                    $"Use EmitAsync() instead.");
            }
        }
    }
}
