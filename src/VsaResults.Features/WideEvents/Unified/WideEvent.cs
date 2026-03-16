using System.Collections.Concurrent;
using VsaResults.Features.WideEvents.Unified.Segments;

namespace VsaResults.Features.WideEvents.Unified;

/// <summary>
/// Unified Wide Event - a single comprehensive log event that can represent
/// feature executions, message processing, or any composable combination.
///
/// Based on the "Canonical Log Lines" / "Wide Events" pattern:
/// https://loggingsucks.com/
///
/// Key principles:
/// - One event per service call (not scattered log lines)
/// - Composable segments (Feature?, Message?, Error?)
/// - Aggregatable (child spans for nested operations)
/// - High cardinality fields (trace_id, correlation_id, feature_name).
/// - High dimensionality (many fields for rich querying).
/// </summary>
public sealed class WideEvent
{
    // Core Identity

    /// <summary>Gets or sets the unique identifier for this event.</summary>
    public string EventId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets or sets the event timestamp.</summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the type of event: "feature", "message", "combined", or custom.</summary>
    public required string EventType { get; set; }

    /// <summary>
    /// Gets or sets the outcome: success, validation_failure, requirements_failure,
    /// execution_failure, side_effects_failure, consumer_error, deserialization_error,
    /// retry_exhausted, circuit_breaker_open, timeout, or exception.
    /// </summary>
    public required string Outcome { get; set; }

    /// <summary>Gets a value indicating whether the operation was successful.</summary>
    public bool IsSuccess => Outcome == "success";

    /// <summary>Gets or sets the total execution time in milliseconds.</summary>
    public double TotalMs { get; set; }

    // Distributed Tracing

    /// <summary>Gets or sets the distributed trace ID (W3C Trace Context).</summary>
    public string? TraceId { get; set; }

    /// <summary>Gets or sets the current span ID.</summary>
    public string? SpanId { get; set; }

    /// <summary>Gets or sets the parent span ID.</summary>
    public string? ParentSpanId { get; set; }

    // Correlation (for message-driven flows)

    /// <summary>Gets or sets the correlation ID for tracking related operations.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Gets or sets the conversation ID for request-response patterns.</summary>
    public string? ConversationId { get; set; }

    /// <summary>Gets or sets the initiator ID (original message that started the flow).</summary>
    public string? InitiatorId { get; set; }

    /// <summary>
    /// Gets or sets the causation ID - links this event to its direct cause.
    /// Used when <see cref="WideEventAggregationMode.LinkOnly"/> is active.
    /// </summary>
    public string? CausationId { get; set; }

    // Service Context

    /// <summary>Gets or sets the name of the service.</summary>
    public string? ServiceName { get; set; }

    /// <summary>Gets or sets the version of the service.</summary>
    public string? ServiceVersion { get; set; }

    /// <summary>Gets or sets the git commit hash for deployment correlation.</summary>
    public string? CommitHash { get; set; }

    /// <summary>Gets or sets the environment name (production, staging, etc.).</summary>
    public string? Environment { get; set; }

    /// <summary>Gets or sets the deployment identifier.</summary>
    public string? DeploymentId { get; set; }

    /// <summary>Gets or sets the region/datacenter.</summary>
    public string? Region { get; set; }

    /// <summary>Gets or sets the host machine name.</summary>
    public string? Host { get; set; }

    // Composable Segments

    /// <summary>
    /// Gets or sets the error segment. Populated when the operation fails.
    /// </summary>
    public WideEventErrorSegment? Error { get; set; }

    /// <summary>
    /// Gets or sets the feature segment. Populated for VSA feature executions.
    /// </summary>
    public WideEventFeatureSegment? Feature { get; set; }

    /// <summary>
    /// Gets or sets the message segment. Populated for message processing.
    /// </summary>
    public WideEventMessageSegment? Message { get; set; }

    // Child Spans (for aggregated events)

    /// <summary>
    /// Gets or sets child spans captured within this event's scope.
    /// Populated when <see cref="WideEventAggregationMode.AggregateToParent"/> is active.
    /// </summary>
    public List<WideEventChildSpan> ChildSpans { get; set; } = [];

    // Custom Context

    /// <summary>
    /// Gets or sets the business context accumulated during execution.
    /// Contains non-sensitive data from requests, messages, and pipeline stages.
    /// </summary>
    public ConcurrentDictionary<string, object?> Context { get; set; } = new();

    /// <summary>
    /// Creates a new wide event builder for a feature execution.
    /// </summary>
    /// <param name="featureName">The name of the feature.</param>
    /// <param name="featureType">The type of feature ("Mutation" or "Query").</param>
    /// <returns>A builder for constructing the wide event.</returns>
    public static WideEventBuilder StartFeature(string featureName, string featureType)
        => new WideEventBuilder("feature").WithFeature(featureName, featureType);

    /// <summary>
    /// Creates a new wide event builder for message processing.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="messageType">The message type name.</param>
    /// <param name="consumerType">The consumer type name.</param>
    /// <param name="endpointName">The endpoint name.</param>
    /// <returns>A builder for constructing the wide event.</returns>
    public static WideEventBuilder StartMessage(
        string messageId,
        string messageType,
        string consumerType,
        string endpointName)
        => new WideEventBuilder("message").WithMessage(messageId, messageType, consumerType, endpointName);

    /// <summary>
    /// Creates a new wide event builder for a combined message + feature execution.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="messageType">The message type name.</param>
    /// <param name="consumerType">The consumer type name.</param>
    /// <param name="endpointName">The endpoint name.</param>
    /// <returns>A builder for constructing the wide event.</returns>
    public static WideEventBuilder StartCombined(
        string messageId,
        string messageType,
        string consumerType,
        string endpointName)
        => new WideEventBuilder("combined").WithMessage(messageId, messageType, consumerType, endpointName);

    /// <summary>
    /// Creates a new wide event builder for a custom event type.
    /// </summary>
    /// <param name="eventType">The custom event type name.</param>
    /// <returns>A builder for constructing the wide event.</returns>
    public static WideEventBuilder Start(string eventType)
        => new(eventType);

    /// <summary>
    /// Creates a deep copy of this wide event, including all segments and child spans.
    /// Uses record <c>with {}</c> expressions on segments (which are records) for shallow copies,
    /// and explicit <c>DeepClone()</c> for segments containing mutable collections.
    /// Adding a new property to a segment record automatically includes it in copies —
    /// only mutable collection properties need explicit handling in <c>DeepClone()</c>.
    /// </summary>
    internal WideEvent Clone()
    {
        return new WideEvent
        {
            EventId = EventId,
            Timestamp = Timestamp,
            EventType = EventType,
            Outcome = Outcome,
            TotalMs = TotalMs,
            TraceId = TraceId,
            SpanId = SpanId,
            ParentSpanId = ParentSpanId,
            CorrelationId = CorrelationId,
            ConversationId = ConversationId,
            InitiatorId = InitiatorId,
            CausationId = CausationId,
            ServiceName = ServiceName,
            ServiceVersion = ServiceVersion,
            CommitHash = CommitHash,
            Environment = Environment,
            DeploymentId = DeploymentId,
            Region = Region,
            Host = Host,
            Error = Error is not null ? Error with { } : null,
            Feature = Feature?.DeepClone(),
            Message = Message?.DeepClone(),
            ChildSpans = [.. ChildSpans.Select(child => child.DeepClone())],
            Context = new ConcurrentDictionary<string, object?>(Context),
        };
    }
}
