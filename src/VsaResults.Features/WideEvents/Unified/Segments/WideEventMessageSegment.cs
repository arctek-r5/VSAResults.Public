namespace VsaResults.Features.WideEvents.Unified.Segments;

/// <summary>
/// Message processing segment for wide events.
/// Contains information specific to message consumption.
/// Implemented as a record to enable safe, shallow cloning via <c>with {}</c>.
/// </summary>
public sealed record WideEventMessageSegment
{
    /// <summary>Gets or sets the message ID.</summary>
    public required string MessageId { get; set; }

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

    // Pipeline Stage Metadata

    /// <summary>Gets or sets the number of retry attempts made.</summary>
    public int RetryAttempt { get; set; }

    /// <summary>Gets or sets the maximum retry count configured.</summary>
    public int? MaxRetryCount { get; set; }

    /// <summary>Gets or sets a value indicating whether message was redelivered (from broker).</summary>
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

    /// <summary>Gets or sets the time the message spent in the queue (if available).</summary>
    public double? QueueTimeMs { get; set; }

    // Fault Context

    /// <summary>Gets or sets a value indicating whether a fault message was published.</summary>
    public bool FaultPublished { get; set; }

    /// <summary>Gets or sets the fault message ID if published.</summary>
    public string? FaultMessageId { get; set; }

    /// <summary>
    /// Creates a deep copy of this segment, including copies of array fields.
    /// </summary>
    /// <returns>A new segment with copied array data.</returns>
    public WideEventMessageSegment DeepClone() => this with
    {
        MessageTypes = MessageTypes?.ToArray(),
        FilterTypes = FilterTypes?.ToArray(),
    };
}
