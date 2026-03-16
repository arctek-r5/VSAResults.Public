namespace VsaResults.Features.WideEvents.Unified.Interceptors;

/// <summary>
/// Interceptor that strips fields based on verbosity level.
/// </summary>
public sealed class VerbosityInterceptor : WideEventInterceptorBase
{
    private readonly WideEventOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="VerbosityInterceptor"/> class.
    /// </summary>
    /// <param name="options">The wide event options.</param>
    public VerbosityInterceptor(WideEventOptions options)
    {
        _options = options;
    }

    /// <inheritdoc />
    public override int Order => 1000; // Run last before emission

    /// <inheritdoc />
    public override ValueTask<WideEvent?> OnBeforeEmitAsync(WideEvent wideEvent, CancellationToken ct)
    {
        switch (_options.Verbosity)
        {
            case WideEventVerbosity.Minimal:
                StripToMinimal(wideEvent);
                break;

            case WideEventVerbosity.Standard:
                StripToStandard(wideEvent);
                break;

            case WideEventVerbosity.Verbose:
                // Keep everything
                break;
        }

        return ValueTask.FromResult<WideEvent?>(wideEvent);
    }

    private static void StripToMinimal(WideEvent wideEvent)
    {
        // Keep only: EventId, Timestamp, EventType, Outcome, TotalMs, TraceId, SpanId, Error basics
        wideEvent.ParentSpanId = null;
        wideEvent.ConversationId = null;
        wideEvent.InitiatorId = null;
        wideEvent.CausationId = null;
        wideEvent.DeploymentId = null;
        wideEvent.Region = null;
        wideEvent.Host = null;
        wideEvent.Context.Clear();
        wideEvent.ChildSpans.Clear();

        if (wideEvent.Feature != null)
        {
            wideEvent.Feature.ValidatorType = null;
            wideEvent.Feature.RequirementsType = null;
            wideEvent.Feature.MutatorType = null;
            wideEvent.Feature.QueryType = null;
            wideEvent.Feature.SideEffectsType = null;
            wideEvent.Feature.LoadedEntities.Clear();
        }

        if (wideEvent.Message != null)
        {
            wideEvent.Message.SourceAddress = null;
            wideEvent.Message.DestinationAddress = null;
            wideEvent.Message.ResponseAddress = null;
            wideEvent.Message.FaultAddress = null;
            wideEvent.Message.MessageTypes = null;
            wideEvent.Message.FilterTypes = null;
            wideEvent.Message.InputAddress = null;
        }

        if (wideEvent.Error != null)
        {
            wideEvent.Error.FailedInNamespace = null;
            wideEvent.Error.FailedInClass = null;
            wideEvent.Error.FailedInMethod = null;
            wideEvent.Error.ExceptionStackTrace = null;
            wideEvent.Error.AllDescriptions = null;
        }
    }

    private static void StripToStandard(WideEvent wideEvent)
    {
        // Keep most fields, but strip verbose-only items
        if (wideEvent.Error != null)
        {
            wideEvent.Error.ExceptionStackTrace = null;
        }

        // Strip child span context (large)
        foreach (var childSpan in wideEvent.ChildSpans)
        {
            childSpan.Context = null;
        }
    }
}
