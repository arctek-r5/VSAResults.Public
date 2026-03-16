using VsaResults.Features.Features;
using VsaResults.Messaging.Bus;
using VsaResults.Messaging.Messages;
using VsaResults.Messaging.Transports;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Consumers;

/// <summary>
/// Context for message consumption, modeled after <see cref="FeatureContext{TRequest}"/>.
/// Carries the message, metadata, and provides methods for follow-up messaging.
/// </summary>
/// <typeparam name="TMessage">The message type.</typeparam>
/// <remarks>
/// <para>
/// ConsumeContext follows the same patterns as FeatureContext:
/// </para>
/// <list type="bullet">
/// <item><description><c>Payload</c> for storing arbitrary data during processing</description></item>
/// <item><description><c>WideEventContext</c> for observability data</description></item>
/// <item><description>Fluent <c>AddContext</c> method for chaining</description></item>
/// </list>
/// </remarks>
public sealed class ConsumeContext<TMessage>
    where TMessage : class, IMessage
{
    /// <summary>Gets the message being consumed.</summary>
    public required TMessage Message { get; init; }

    /// <summary>Gets the message envelope containing metadata.</summary>
    public required MessageEnvelope Envelope { get; init; }

    /// <summary>Gets the unique message identifier.</summary>
    public MessageId MessageId => Envelope.MessageId;

    /// <summary>Gets the correlation identifier.</summary>
    public CorrelationId CorrelationId => Envelope.CorrelationId;

    /// <summary>Gets the conversation identifier.</summary>
    public ConversationId? ConversationId => Envelope.ConversationId;

    /// <summary>Gets the message headers.</summary>
    public MessageHeaders Headers => Envelope.Headers;

    /// <summary>Gets the timestamp when the message was sent.</summary>
    public DateTimeOffset SentTime => Envelope.SentTime;

    /// <summary>Gets the source address.</summary>
    public EndpointAddress? SourceAddress => Envelope.SourceAddress;

    /// <summary>Gets the destination address.</summary>
    public EndpointAddress? DestinationAddress => Envelope.DestinationAddress;

    /// <summary>
    /// Gets the payload storage for arbitrary data during processing.
    /// Similar to <see cref="FeatureContext{TRequest}.Entities"/>.
    /// </summary>
    public Dictionary<string, object> Payload { get; } = [];

    /// <summary>
    /// Gets the context to be included in the wide event log.
    /// Similar to <see cref="FeatureContext{TRequest}.WideEventContext"/>.
    /// </summary>
    public Dictionary<string, object?> WideEventContext { get; } = [];

    /// <summary>Gets the publish endpoint for publishing follow-up events.</summary>
    public required IPublishEndpoint PublishEndpoint { get; init; }

    /// <summary>Gets the send endpoint provider for sending commands.</summary>
    public required ISendEndpointProvider SendEndpointProvider { get; init; }

    /// <summary>Gets the receive endpoint name where this message was received.</summary>
    public string? ReceiveEndpointName { get; init; }

    /// <summary>Gets the current retry attempt number (0 = first attempt).</summary>
    public int RetryAttempt { get; init; }

    /// <summary>
    /// Gets a payload value by key.
    /// </summary>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <param name="key">The payload key.</param>
    /// <returns>The value cast to the specified type.</returns>
    public T GetPayload<T>(string key) => (T)Payload[key];

    /// <summary>
    /// Tries to get a payload value by key.
    /// </summary>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <param name="key">The payload key.</param>
    /// <param name="value">The value if found.</param>
    /// <returns>True if the value was found and is of the correct type.</returns>
    public bool TryGetPayload<T>(string key, out T? value)
    {
        if (Payload.TryGetValue(key, out var obj) && obj is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Sets a payload value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="key">The payload key.</param>
    /// <param name="value">The value to store.</param>
    public void SetPayload<T>(string key, T value)
        where T : notnull
        => Payload[key] = value;

    /// <summary>
    /// Adds context to be included in the wide event log.
    /// Use snake_case keys for consistency with structured logging.
    /// </summary>
    /// <param name="key">The context key (e.g., "order_id", "customer_name").</param>
    /// <param name="value">The context value.</param>
    /// <returns>This context for fluent chaining.</returns>
    public ConsumeContext<TMessage> AddContext(string key, object? value)
    {
        WideEventContext[key] = value;
        return this;
    }

    /// <summary>
    /// Adds multiple context entries to be included in the wide event log.
    /// </summary>
    /// <param name="pairs">Key-value pairs to add.</param>
    /// <returns>This context for fluent chaining.</returns>
    public ConsumeContext<TMessage> AddContext(params (string Key, object? Value)[] pairs)
    {
        foreach (var (key, value) in pairs)
        {
            WideEventContext[key] = value;
        }

        return this;
    }

    /// <summary>
    /// Publishes a follow-up event. Correlation is automatically preserved.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="event">The event to publish.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Unit on success, or errors on failure.</returns>
    public Task<VsaResult<Unit>> PublishAsync<TEvent>(
        TEvent @event,
        CancellationToken ct = default)
        where TEvent : class, IEvent
        => PublishEndpoint.PublishAsync(@event, headers =>
        {
            // Preserve tracing context
            headers.TraceId = Headers.TraceId;
            headers.SpanId = Headers.SpanId;
            headers.TenantId = Headers.TenantId;
            headers.InitiatorId = MessageId.ToString();
        }, ct);

    /// <summary>
    /// Publishes a follow-up event with custom headers.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="event">The event to publish.</param>
    /// <param name="configureHeaders">Headers configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Unit on success, or errors on failure.</returns>
    public Task<VsaResult<Unit>> PublishAsync<TEvent>(
        TEvent @event,
        Action<MessageHeaders> configureHeaders,
        CancellationToken ct = default)
        where TEvent : class, IEvent
        => PublishEndpoint.PublishAsync(@event, headers =>
        {
            // Preserve tracing context first
            headers.TraceId = Headers.TraceId;
            headers.SpanId = Headers.SpanId;
            headers.TenantId = Headers.TenantId;
            headers.InitiatorId = MessageId.ToString();

            // Then allow customization
            configureHeaders(headers);
        }, ct);

    /// <summary>
    /// Sends a command to a specific endpoint. Correlation is automatically preserved.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <param name="address">The destination address.</param>
    /// <param name="command">The command to send.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Unit on success, or errors on failure.</returns>
    public async Task<VsaResult<Unit>> SendAsync<TCommand>(
        EndpointAddress address,
        TCommand command,
        CancellationToken ct = default)
        where TCommand : class, ICommand
    {
        var endpointResult = await SendEndpointProvider.GetSendEndpointAsync(address, ct);
        if (endpointResult.IsError)
        {
            return endpointResult.Errors.ToResult<Unit>();
        }

        return await endpointResult.Value.SendAsync(command, headers =>
        {
            // Preserve tracing context
            headers.TraceId = Headers.TraceId;
            headers.SpanId = Headers.SpanId;
            headers.TenantId = Headers.TenantId;
            headers.InitiatorId = MessageId.ToString();
        }, ct);
    }

    /// <summary>
    /// Schedules a message for future delivery.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="event">The event to schedule.</param>
    /// <param name="scheduledTime">When to deliver the message.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Unit on success, or errors on failure.</returns>
    public Task<VsaResult<Unit>> SchedulePublishAsync<TEvent>(
        TEvent @event,
        DateTimeOffset scheduledTime,
        CancellationToken ct = default)
        where TEvent : class, IEvent
        => PublishEndpoint.PublishAsync(@event, headers =>
        {
            headers.TraceId = Headers.TraceId;
            headers.SpanId = Headers.SpanId;
            headers.TenantId = Headers.TenantId;
            headers.InitiatorId = MessageId.ToString();
            headers.ScheduledTime = scheduledTime;
        }, ct);
}
