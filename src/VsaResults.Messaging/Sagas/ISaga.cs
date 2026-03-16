using VsaResults.Features.Features;
using VsaResults.Messaging.Messages;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Sagas;

/// <summary>
/// Marker interface for sagas.
/// </summary>
public interface ISaga
{
}

/// <summary>
/// Interface for saga handlers that process messages and update saga state.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
/// <typeparam name="TMessage">The message type this handler processes.</typeparam>
public interface ISaga<TState, in TMessage> : ISaga
    where TState : class, ISagaState, new()
    where TMessage : class, IMessage
{
    /// <summary>
    /// Extracts the correlation ID from a message to find the correct saga instance.
    /// </summary>
    /// <param name="message">The message to extract the correlation ID from.</param>
    /// <returns>The correlation ID, or null if not found.</returns>
    Guid? CorrelateBy(TMessage message);

    /// <summary>
    /// Handles a message within the saga context.
    /// </summary>
    /// <param name="context">The saga context with state and messaging capabilities.</param>
    /// <param name="message">The message to handle.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Unit on success, or errors on failure.</returns>
    Task<VsaResult<Unit>> HandleAsync(
        SagaContext<TState> context,
        TMessage message,
        CancellationToken ct = default);
}

/// <summary>
/// Interface for sagas that can be initiated by a message.
/// When the correlation ID is not found, a new saga instance is created.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
/// <typeparam name="TMessage">The initiating message type.</typeparam>
public interface IInitiatedBy<TState, in TMessage> : ISaga<TState, TMessage>
    where TState : class, ISagaState, new()
    where TMessage : class, IMessage
{
    /// <summary>
    /// Initializes a new saga state from the initiating message.
    /// </summary>
    /// <param name="message">The initiating message.</param>
    /// <returns>The initialized saga state.</returns>
    TState Initialize(TMessage message);
}

/// <summary>
/// Interface for sagas that handle events (publish/subscribe).
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
/// <typeparam name="TEvent">The event type.</typeparam>
public interface IOrchestrates<TState, in TEvent> : ISaga<TState, TEvent>
    where TState : class, ISagaState, new()
    where TEvent : class, IEvent
{
}

/// <summary>
/// Interface for sagas that observe events without modifying state.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
/// <typeparam name="TEvent">The event type.</typeparam>
public interface IObserves<TState, in TEvent> : ISaga<TState, TEvent>
    where TState : class, ISagaState, new()
    where TEvent : class, IEvent
{
}
