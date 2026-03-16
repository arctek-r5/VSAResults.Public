using VsaResults.Features.Features;
using VsaResults.Messaging.Bus;
using VsaResults.Messaging.Messages;
using VsaResults.Messaging.Transports;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Consumers;

/// <summary>
/// Consumer for batch processing of messages.
/// Useful for high-throughput scenarios where processing messages in batches
/// is more efficient than one at a time.
/// </summary>
/// <typeparam name="TMessage">The message type.</typeparam>
/// <remarks>
/// <para>
/// Batch consumers receive multiple messages at once, enabling optimizations like:
/// </para>
/// <list type="bullet">
/// <item><description>Bulk database operations</description></item>
/// <item><description>Reduced per-message overhead</description></item>
/// <item><description>Better throughput for high-volume scenarios</description></item>
/// </list>
/// <para>
/// Example:
/// <code>
/// public class OrderBatchConsumer : IBatchConsumer&lt;OrderCreated&gt;
/// {
///     public async Task&lt;ErrorOr&lt;Unit&gt;&gt; ConsumeAsync(
///         BatchConsumeContext&lt;OrderCreated&gt; context,
///         CancellationToken ct = default)
///     {
///         var orders = context.Messages.Select(m => m.Message).ToList();
///         await _orderService.ProcessBatchAsync(orders, ct);
///         return Unit.Value;
///     }
/// }
/// </code>
/// </para>
/// </remarks>
public interface IBatchConsumer<TMessage> : IConsumer
    where TMessage : class, IMessage
{
    /// <summary>
    /// Consumes a batch of messages.
    /// </summary>
    /// <param name="context">The batch consume context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Unit on success, or errors on failure.</returns>
    Task<VsaResult<Unit>> ConsumeAsync(
        BatchConsumeContext<TMessage> context,
        CancellationToken ct = default);
}

/// <summary>
/// Context for batch message consumption.
/// </summary>
/// <typeparam name="TMessage">The message type.</typeparam>
public sealed class BatchConsumeContext<TMessage>
    where TMessage : class, IMessage
{
    /// <summary>Gets the batch of message contexts.</summary>
    public required IReadOnlyList<ConsumeContext<TMessage>> Messages { get; init; }

    /// <summary>Gets the number of messages in the batch.</summary>
    public int Count => Messages.Count;

    /// <summary>Gets whether the batch is empty.</summary>
    public bool IsEmpty => Count == 0;

    /// <summary>Gets the first message context.</summary>
    public ConsumeContext<TMessage> First => Messages[0];

    /// <summary>Gets the correlation ID from the first message.</summary>
    public CorrelationId CorrelationId => Messages.Count > 0 ? Messages[0].CorrelationId : CorrelationId.Empty;

    /// <summary>Gets the publish endpoint for publishing follow-up events.</summary>
    public required IPublishEndpoint PublishEndpoint { get; init; }

    /// <summary>Gets the send endpoint provider for sending commands.</summary>
    public required ISendEndpointProvider SendEndpointProvider { get; init; }

    /// <summary>
    /// Gets the context to be included in the wide event log.
    /// </summary>
    // ReSharper disable once CollectionNeverQueried.Global — read by pipeline infrastructure for wide event emission
    public Dictionary<string, object?> WideEventContext { get; } = [];

    /// <summary>
    /// Adds context to be included in the wide event log.
    /// </summary>
    /// <param name="key">The context key.</param>
    /// <param name="value">The context value.</param>
    /// <returns>This context for fluent chaining.</returns>
    public BatchConsumeContext<TMessage> AddContext(string key, object? value)
    {
        WideEventContext[key] = value;
        return this;
    }

    /// <summary>
    /// Projects the messages to their payloads.
    /// </summary>
    /// <returns>The list of message payloads.</returns>
    public IReadOnlyList<TMessage> GetMessages() =>
        Messages.Select(m => m.Message).ToList();

    /// <summary>
    /// Projects the messages using a selector function.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="selector">The projection function.</param>
    /// <returns>The projected results.</returns>
    public IReadOnlyList<TResult> Select<TResult>(Func<ConsumeContext<TMessage>, TResult> selector) =>
        Messages.Select(selector).ToList();

    /// <summary>
    /// Publishes a follow-up event for the batch.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="event">The event to publish.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Unit on success, or errors on failure.</returns>
    public Task<VsaResult<Unit>> PublishAsync<TEvent>(
        TEvent @event,
        CancellationToken ct = default)
        where TEvent : class, IEvent
        => PublishEndpoint.PublishAsync(@event, ct);

    /// <summary>
    /// Sends a command to a specific endpoint.
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

        return await endpointResult.Value.SendAsync(command, ct);
    }
}

/// <summary>
/// Configuration for batch consumption.
/// </summary>
public sealed class BatchConsumerOptions
{
    /// <summary>Gets or sets the maximum batch size.</summary>
    public int MaxBatchSize { get; set; } = 100;

    /// <summary>Gets or sets the maximum time to wait for a full batch.</summary>
    public TimeSpan MaxBatchTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets whether to process partial batches on timeout.</summary>
    public bool ProcessPartialBatches { get; set; } = true;
}
