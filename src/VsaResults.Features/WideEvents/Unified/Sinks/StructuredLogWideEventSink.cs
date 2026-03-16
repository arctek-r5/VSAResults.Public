using Microsoft.Extensions.Logging;

namespace VsaResults.Features.WideEvents.Unified.Sinks;

/// <summary>
/// Sink that writes wide events using structured logging with all properties.
/// Suitable for use with Serilog's destructuring feature.
/// </summary>
public sealed class StructuredLogWideEventSink : IWideEventSink
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StructuredLogWideEventSink"/> class.
    /// </summary>
    /// <param name="logger">The logger to write to.</param>
    public StructuredLogWideEventSink(ILogger logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(WideEvent wideEvent, CancellationToken ct = default)
    {
        var logLevel = wideEvent.IsSuccess ? LogLevel.Information : LogLevel.Warning;

        // Log with the entire event as a structured object
        // Serilog will destructure this into individual properties
        using (_logger.BeginScope(BuildScope(wideEvent)))
        {
            _logger.Log(
                logLevel,
                "WideEvent: {EventType} {Outcome} in {TotalMs:F2}ms",
                wideEvent.EventType,
                wideEvent.Outcome,
                wideEvent.TotalMs);
        }

        return ValueTask.CompletedTask;
    }

    private static Dictionary<string, object?> BuildScope(WideEvent wideEvent)
    {
        var scope = new Dictionary<string, object?>
        {
            ["event_id"] = wideEvent.EventId,
            ["event_type"] = wideEvent.EventType,
            ["outcome"] = wideEvent.Outcome,
            ["total_ms"] = wideEvent.TotalMs,
            ["timestamp"] = wideEvent.Timestamp,
            ["trace_id"] = wideEvent.TraceId,
            ["span_id"] = wideEvent.SpanId,
            ["parent_span_id"] = wideEvent.ParentSpanId,
            ["correlation_id"] = wideEvent.CorrelationId,
            ["conversation_id"] = wideEvent.ConversationId,
            ["initiator_id"] = wideEvent.InitiatorId,
            ["causation_id"] = wideEvent.CausationId,
            ["service_name"] = wideEvent.ServiceName,
            ["service_version"] = wideEvent.ServiceVersion,
            ["environment"] = wideEvent.Environment,
            ["deployment_id"] = wideEvent.DeploymentId,
            ["region"] = wideEvent.Region,
            ["host"] = wideEvent.Host,
        };

        // Add feature segment
        if (wideEvent.Feature != null)
        {
            scope["feature_name"] = wideEvent.Feature.FeatureName;
            scope["feature_type"] = wideEvent.Feature.FeatureType;
            scope["request_type"] = wideEvent.Feature.RequestType;
            scope["result_type"] = wideEvent.Feature.ResultType;
            scope["validator_type"] = wideEvent.Feature.ValidatorType;
            scope["requirements_type"] = wideEvent.Feature.RequirementsType;
            scope["mutator_type"] = wideEvent.Feature.MutatorType;
            scope["query_type"] = wideEvent.Feature.QueryType;
            scope["side_effects_type"] = wideEvent.Feature.SideEffectsType;
            scope["has_custom_validator"] = wideEvent.Feature.HasCustomValidator;
            scope["has_custom_requirements"] = wideEvent.Feature.HasCustomRequirements;
            scope["has_custom_side_effects"] = wideEvent.Feature.HasCustomSideEffects;
            scope["validation_ms"] = wideEvent.Feature.ValidationMs;
            scope["requirements_ms"] = wideEvent.Feature.RequirementsMs;
            scope["execution_ms"] = wideEvent.Feature.ExecutionMs;
            scope["side_effects_ms"] = wideEvent.Feature.SideEffectsMs;

            if (!wideEvent.Feature.LoadedEntities.IsEmpty)
            {
                scope["loaded_entities"] = wideEvent.Feature.LoadedEntities;
            }
        }

        // Add message segment
        if (wideEvent.Message != null)
        {
            scope["message_id"] = wideEvent.Message.MessageId;
            scope["message_type"] = wideEvent.Message.MessageType;
            scope["message_types"] = wideEvent.Message.MessageTypes;
            scope["consumer_type"] = wideEvent.Message.ConsumerType;
            scope["endpoint_name"] = wideEvent.Message.EndpointName;
            scope["input_address"] = wideEvent.Message.InputAddress;
            scope["source_address"] = wideEvent.Message.SourceAddress;
            scope["destination_address"] = wideEvent.Message.DestinationAddress;
            scope["response_address"] = wideEvent.Message.ResponseAddress;
            scope["fault_address"] = wideEvent.Message.FaultAddress;
            scope["retry_attempt"] = wideEvent.Message.RetryAttempt;
            scope["max_retry_count"] = wideEvent.Message.MaxRetryCount;
            scope["redelivered"] = wideEvent.Message.Redelivered;
            scope["filter_types"] = wideEvent.Message.FilterTypes;
            scope["deserialization_ms"] = wideEvent.Message.DeserializationMs;
            scope["pre_consume_filters_ms"] = wideEvent.Message.PreConsumeFiltersMs;
            scope["consumer_ms"] = wideEvent.Message.ConsumerMs;
            scope["post_consume_filters_ms"] = wideEvent.Message.PostConsumeFiltersMs;
            scope["queue_time_ms"] = wideEvent.Message.QueueTimeMs;
            scope["fault_published"] = wideEvent.Message.FaultPublished;
            scope["fault_message_id"] = wideEvent.Message.FaultMessageId;
        }

        // Add error segment
        if (wideEvent.Error != null)
        {
            scope["error_code"] = wideEvent.Error.Code;
            scope["error_type"] = wideEvent.Error.Type;
            scope["error_message"] = wideEvent.Error.Message;
            scope["error_all_descriptions"] = wideEvent.Error.AllDescriptions;
            scope["error_count"] = wideEvent.Error.Count;
            scope["failed_at_stage"] = wideEvent.Error.FailedAtStage;
            scope["failed_in_namespace"] = wideEvent.Error.FailedInNamespace;
            scope["failed_in_class"] = wideEvent.Error.FailedInClass;
            scope["failed_in_method"] = wideEvent.Error.FailedInMethod;
            scope["exception_type"] = wideEvent.Error.ExceptionType;
            scope["exception_message"] = wideEvent.Error.ExceptionMessage;
            scope["exception_stack_trace"] = wideEvent.Error.ExceptionStackTrace;
        }

        // Add context
        foreach (var (key, value) in wideEvent.Context)
        {
            scope[$"ctx_{key}"] = value;
        }

        // Add child spans summary
        if (wideEvent.ChildSpans.Count > 0)
        {
            scope["child_span_count"] = wideEvent.ChildSpans.Count;
            scope["child_spans"] = wideEvent.ChildSpans.Select(cs => new
            {
                span_id = cs.SpanId,
                event_type = cs.EventType,
                name = cs.Name,
                outcome = cs.Outcome,
                duration_ms = cs.DurationMs,
            }).ToList();
        }

        return scope;
    }
}
