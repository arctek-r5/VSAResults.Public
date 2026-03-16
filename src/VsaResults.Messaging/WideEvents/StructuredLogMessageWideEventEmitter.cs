using Microsoft.Extensions.Logging;

namespace VsaResults.Messaging.WideEvents;

/// <summary>
/// Emits message wide events via structured logging (Serilog-compatible).
/// Mirrors the pattern used by StructuredLogWideEventSink for feature wide events.
/// </summary>
public sealed class StructuredLogMessageWideEventEmitter : IMessageWideEventEmitter
{
    private readonly ILogger _logger;

    public StructuredLogMessageWideEventEmitter(ILogger logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public void Emit(MessageWideEvent wideEvent)
    {
        var logLevel = wideEvent.Outcome is "success" ? LogLevel.Information : LogLevel.Warning;

        // Destructure the entire wide event as structured properties.
        // Serilog will flatten these into the log entry.
        _logger.Log(logLevel,
            "WideEvent: {EventType} {Outcome} in {TotalMs:F2}ms",
            "consumer",
            wideEvent.Outcome,
            wideEvent.TotalMs);

        // Log the full event as a scope so all properties are captured
        using (_logger.BeginScope(BuildScope(wideEvent)))
        {
            if (wideEvent.ExceptionType is not null)
            {
                _logger.Log(LogLevel.Warning,
                    "Consumer {ConsumerType} exception: {ExceptionType} — {ExceptionMessage}",
                    wideEvent.ConsumerType,
                    wideEvent.ExceptionType,
                    wideEvent.ExceptionMessage);
            }
            else if (wideEvent.ErrorCode is not null)
            {
                _logger.Log(LogLevel.Warning,
                    "Consumer {ConsumerType} error: {ErrorCode} — {ErrorMessage}",
                    wideEvent.ConsumerType,
                    wideEvent.ErrorCode,
                    wideEvent.ErrorMessage);
            }
        }
    }

    private static Dictionary<string, object?> BuildScope(MessageWideEvent e)
    {
        var scope = new Dictionary<string, object?>
        {
            ["event_type"] = "consumer",
            ["outcome"] = e.Outcome,
            ["total_ms"] = e.TotalMs,
            ["message_id"] = e.MessageId,
            ["correlation_id"] = e.CorrelationId,
            ["message_type"] = e.MessageType,
            ["consumer_type"] = e.ConsumerType,
            ["endpoint_name"] = e.EndpointName,
            ["trace_id"] = e.TraceId,
            ["span_id"] = e.SpanId,
            ["service_name"] = e.ServiceName,
            ["environment"] = e.Environment,
            ["host"] = e.Host,
        };

        if (e.ErrorCode is not null) scope["error_code"] = e.ErrorCode;
        if (e.ErrorType is not null) scope["error_type"] = e.ErrorType;
        if (e.ErrorMessage is not null) scope["error_message"] = e.ErrorMessage;
        if (e.ExceptionType is not null) scope["exception_type"] = e.ExceptionType;
        if (e.ExceptionMessage is not null) scope["exception_message"] = e.ExceptionMessage;
        if (e.FailedAtStage is not null) scope["failed_at_stage"] = e.FailedAtStage;
        if (e.RetryAttempt > 0) scope["retry_attempt"] = e.RetryAttempt;
        if (e.QueueTimeMs > 0) scope["queue_time_ms"] = e.QueueTimeMs;
        if (e.ConsumerMs > 0) scope["consumer_ms"] = e.ConsumerMs;

        foreach (var (key, value) in e.MessageContext)
        {
            scope[$"ctx_{key}"] = value;
        }

        return scope;
    }
}
