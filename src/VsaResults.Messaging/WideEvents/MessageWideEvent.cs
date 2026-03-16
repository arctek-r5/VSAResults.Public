namespace VsaResults.Messaging.WideEvents;

/// <summary>
/// Wide Event for message processing - a single comprehensive log event
/// emitted per message consumption containing all context needed for debugging.
///
/// Based on the "Canonical Log Lines" / "Wide Events" pattern:
/// https://loggingsucks.com/
///
/// Key principles:
/// - One event per message consumption (not scattered log lines)
/// - High cardinality fields (message_id, correlation_id, consumer_name)
/// - High dimensionality (many fields for rich querying)
/// - Build throughout, emit once at the end.
/// </summary>
public sealed class MessageWideEvent
{
    // Request Context

    /// <summary>Gets or sets the distributed trace ID.</summary>
    public string? TraceId { get; set; }

    /// <summary>Gets or sets the current span ID.</summary>
    public string? SpanId { get; set; }

    /// <summary>Gets or sets the parent span ID.</summary>
    public string? ParentSpanId { get; set; }

    /// <summary>Gets or sets the event timestamp.</summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    // Message Context

    /// <summary>Gets or sets the message ID.</summary>
    public required string MessageId { get; set; }

    /// <summary>Gets or sets the correlation ID for tracking related messages.</summary>
    public required string CorrelationId { get; set; }

    /// <summary>Gets or sets the conversation ID for request-response patterns.</summary>
    public string? ConversationId { get; set; }

    /// <summary>Gets or sets the initiator ID (original message that started the flow).</summary>
    public string? InitiatorId { get; set; }

    /// <summary>Gets or sets the message type name.</summary>
    public required string MessageType { get; set; }

    /// <summary>Gets or sets the full message types hierarchy (for polymorphic messages).</summary>
    public string[]? MessageTypes { get; set; }

    /// <summary>Gets or sets the source address where the message was sent from.</summary>
    public string? SourceAddress { get; set; }

    /// <summary>Gets or sets the destination address.</summary>
    public string? DestinationAddress { get; set; }

    /// <summary>Gets or sets the response address for request-response.</summary>
    public string? ResponseAddress { get; set; }

    /// <summary>Gets or sets the fault address for error routing.</summary>
    public string? FaultAddress { get; set; }

    // Consumer Context

    /// <summary>Gets or sets the consumer type name.</summary>
    public required string ConsumerType { get; set; }

    /// <summary>Gets or sets the endpoint name where the message was received.</summary>
    public required string EndpointName { get; set; }

    /// <summary>Gets or sets the input queue address.</summary>
    public string? InputAddress { get; set; }

    // Service Context

    /// <summary>Gets or sets the name of the service.</summary>
    public string? ServiceName { get; set; }

    /// <summary>Gets or sets the version of the service.</summary>
    public string? ServiceVersion { get; set; }

    /// <summary>Gets or sets the environment name (production, staging, etc.).</summary>
    public string? Environment { get; set; }

    /// <summary>Gets or sets the deployment identifier.</summary>
    public string? DeploymentId { get; set; }

    /// <summary>Gets or sets the region/datacenter.</summary>
    public string? Region { get; set; }

    /// <summary>Gets or sets the host machine name.</summary>
    public string? Host { get; set; }

    // Pipeline Stage Metadata

    /// <summary>Gets or sets the number of retry attempts made.</summary>
    public int RetryAttempt { get; set; }

    /// <summary>Gets or sets the maximum retry count configured.</summary>
    public int? MaxRetryCount { get; set; }

    /// <summary>Gets or sets a value indicating whether message redelivered (from broker).</summary>
    public bool Redelivered { get; set; }

    /// <summary>Gets or sets the filter types applied during processing.</summary>
    public string[]? FilterTypes { get; set; }

    // Timing Breakdown (milliseconds)

    /// <summary>Gets or sets the time spent deserializing the message.</summary>
    public double? DeserializationMs { get; set; }

    /// <summary>Gets or sets the time spent in pre-consume filters.</summary>
    public double? PreConsumeFiltersMs { get; set; }

    /// <summary>Gets or sets the time spent in consumer execution.</summary>
    public double? ConsumerMs { get; set; }

    /// <summary>Gets or sets the time spent in post-consume filters.</summary>
    public double? PostConsumeFiltersMs { get; set; }

    /// <summary>Gets or sets the total processing time.</summary>
    public double TotalMs { get; set; }

    /// <summary>Gets or sets the time the message spent in the queue (if available).</summary>
    public double? QueueTimeMs { get; set; }

    // Outcome

    /// <summary>
    /// Gets or sets the processing outcome: success, consumer_error, deserialization_error,
    /// retry_exhausted, circuit_breaker_open, timeout, or exception.
    /// </summary>
    public required string Outcome { get; set; }

    /// <summary>Gets a value indicating whether the processing was successful.</summary>
    public bool IsSuccess => Outcome == "success";

    // Error Context (populated on failure)

    /// <summary>Gets or sets the first error code.</summary>
    public string? ErrorCode { get; set; }

    /// <summary>Gets or sets the first error type.</summary>
    public string? ErrorType { get; set; }

    /// <summary>Gets or sets the first error message.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Gets or sets all error descriptions joined.</summary>
    public string? ErrorDescription { get; set; }

    /// <summary>Gets or sets the total number of errors.</summary>
    public int? ErrorCount { get; set; }

    /// <summary>Gets or sets which pipeline stage failed.</summary>
    public string? FailedAtStage { get; set; }

    // Exception Context (populated on unhandled exception)

    /// <summary>Gets or sets the exception type name.</summary>
    public string? ExceptionType { get; set; }

    /// <summary>Gets or sets the exception message.</summary>
    public string? ExceptionMessage { get; set; }

    /// <summary>Gets or sets the exception stack trace.</summary>
    public string? ExceptionStackTrace { get; set; }

    // Fault Context (populated when fault is published)

    /// <summary>Gets or sets a value indicating whether a fault message was published.</summary>
    public bool FaultPublished { get; set; }

    /// <summary>Gets or sets the fault message ID if published.</summary>
    public string? FaultMessageId { get; set; }

    // Message Context (non-sensitive fields from headers and context)

    /// <summary>Gets or sets the business context accumulated during processing.</summary>
    public Dictionary<string, object?> MessageContext { get; set; } = [];

    /// <summary>
    /// Creates a new wide event builder for message processing.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="correlationId">The correlation ID.</param>
    /// <param name="messageType">The message type name.</param>
    /// <param name="consumerType">The consumer type name.</param>
    /// <param name="endpointName">The endpoint name.</param>
    /// <returns>A builder for constructing the wide event.</returns>
    public static MessageWideEventBuilder Start(
        string messageId,
        string correlationId,
        string messageType,
        string consumerType,
        string endpointName)
        => new(messageId, correlationId, messageType, consumerType, endpointName);
}
