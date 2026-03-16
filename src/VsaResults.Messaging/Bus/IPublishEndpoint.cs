using VsaResults.Features.Features;
using VsaResults.Messaging.Messages;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Bus;

/// <summary>
/// Interface for publishing events.
/// Events are delivered to all subscribed consumers.
/// </summary>
public interface IPublishEndpoint
{
    /// <summary>
    /// Publishes an event to all subscribers.
    /// </summary>
    /// <typeparam name="TMessage">The event type.</typeparam>
    /// <param name="message">The event to publish.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Unit on success, or errors on failure.</returns>
    Task<VsaResult<Unit>> PublishAsync<TMessage>(
        TMessage message,
        CancellationToken ct = default)
        where TMessage : class, IEvent;

    /// <summary>
    /// Publishes an event with custom header configuration.
    /// </summary>
    /// <typeparam name="TMessage">The event type.</typeparam>
    /// <param name="message">The event to publish.</param>
    /// <param name="configureHeaders">Header configuration callback.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Unit on success, or errors on failure.</returns>
    Task<VsaResult<Unit>> PublishAsync<TMessage>(
        TMessage message,
        Action<MessageHeaders> configureHeaders,
        CancellationToken ct = default)
        where TMessage : class, IEvent;

    /// <summary>
    /// Publishes an event with a specific correlation ID.
    /// </summary>
    /// <typeparam name="TMessage">The event type.</typeparam>
    /// <param name="message">The event to publish.</param>
    /// <param name="correlationId">The correlation ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Unit on success, or errors on failure.</returns>
    Task<VsaResult<Unit>> PublishAsync<TMessage>(
        TMessage message,
        CorrelationId correlationId,
        CancellationToken ct = default)
        where TMessage : class, IEvent;
}
