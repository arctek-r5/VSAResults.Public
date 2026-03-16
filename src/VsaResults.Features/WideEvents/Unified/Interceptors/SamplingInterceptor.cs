namespace VsaResults.Features.WideEvents.Unified.Interceptors;

/// <summary>
/// Interceptor that applies sampling to wide events.
/// Successful events are sampled at the configured rate; failures are always emitted.
/// </summary>
public sealed class SamplingInterceptor : WideEventInterceptorBase
{
    private readonly WideEventOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="SamplingInterceptor"/> class.
    /// </summary>
    /// <param name="options">The wide event options.</param>
    public SamplingInterceptor(WideEventOptions options)
    {
        _options = options;
    }

    /// <inheritdoc />
    public override int Order => -1000; // Run early to filter before expensive operations

    /// <inheritdoc />
    public override ValueTask<WideEvent?> OnBeforeEmitAsync(WideEvent wideEvent, CancellationToken ct)
    {
        if (!_options.EnableSampling)
        {
            return ValueTask.FromResult<WideEvent?>(wideEvent);
        }

        // Always emit events with outcomes in the always-capture list
        if (_options.AlwaysCaptureOutcomes.Contains(wideEvent.Outcome))
        {
            return ValueTask.FromResult<WideEvent?>(wideEvent);
        }

        // Apply sampling rate
        if (_options.SamplingRate >= 1.0)
        {
            return ValueTask.FromResult<WideEvent?>(wideEvent);
        }

        if (_options.SamplingRate <= 0.0)
        {
            return ValueTask.FromResult<WideEvent?>(null);
        }

        // Use deterministic sampling based on event ID for consistency
        var hash = wideEvent.EventId.GetHashCode();
        var threshold = (uint)(_options.SamplingRate * uint.MaxValue);
        var sample = (uint)hash;

        if (sample <= threshold)
        {
            return ValueTask.FromResult<WideEvent?>(wideEvent);
        }

        return ValueTask.FromResult<WideEvent?>(null);
    }
}
