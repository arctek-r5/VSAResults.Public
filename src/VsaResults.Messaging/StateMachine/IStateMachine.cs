using VsaResults.Features.Features;
using VsaResults.Messaging.ErrorOr;
using VsaResults.Messaging.Messages;
using VsaResults.Messaging.Sagas;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.StateMachine;

/// <summary>
/// Marker interface for state machines.
/// </summary>
public interface IStateMachine
{
    /// <summary>
    /// Gets the saga state type managed by this state machine.
    /// </summary>
    Type StateType { get; }

    /// <summary>
    /// Gets all defined states in this state machine.
    /// </summary>
    IReadOnlyList<State> States { get; }

    /// <summary>
    /// Gets all events (message types) handled by this state machine.
    /// </summary>
    IReadOnlyList<Type> Events { get; }
}

/// <summary>
/// Interface for a state machine that manages saga state transitions.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
public interface IStateMachine<TState> : IStateMachine
    where TState : class, ISagaState, new()
{
    /// <summary>
    /// Gets or sets the initial state for new saga instances.
    /// </summary>
    State InitialState { get; }

    /// <summary>
    /// Gets all event handlers registered with this state machine.
    /// </summary>
    IReadOnlyDictionary<Type, IReadOnlyList<IEventHandler<TState>>> EventHandlers { get; }

    /// <summary>
    /// Gets the callback invoked when the saga transitions to the Final state.
    /// </summary>
    Action<TState>? OnCompleteCallback { get; }

    /// <summary>
    /// Gets the correlation ID extractor for a message type.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <returns>A function that extracts the correlation ID from a message, or null if not configured.</returns>
    Func<TMessage, Guid>? GetCorrelationIdExtractor<TMessage>()
        where TMessage : class, IMessage;
}

/// <summary>
/// Interface for event handlers within a state machine.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
public interface IEventHandler<TState>
    where TState : class, ISagaState, new()
{
    /// <summary>
    /// Gets the message type this handler processes.
    /// </summary>
    Type MessageType { get; }

    /// <summary>
    /// Gets the states this handler is active in, or empty for all states.
    /// </summary>
    IReadOnlyList<State> ActiveInStates { get; }

    /// <summary>
    /// Gets a value indicating whether this handler can initiate a new saga.
    /// </summary>
    bool CanInitiate { get; }

    /// <summary>
    /// Handles a message within the saga context.
    /// </summary>
    /// <param name="context">The saga context.</param>
    /// <param name="message">The message to handle.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Unit on success, or errors on failure.</returns>
    Task<VsaResult<Unit>> HandleAsync(SagaContext<TState> context, object message, CancellationToken ct);
}

/// <summary>
/// Typed event handler for a specific message type.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
/// <typeparam name="TMessage">The message type.</typeparam>
public sealed class EventHandler<TState, TMessage> : IEventHandler<TState>
    where TState : class, ISagaState, new()
    where TMessage : class, IMessage
{
    private readonly Func<SagaContext<TState>, TMessage, CancellationToken, Task<VsaResult<Unit>>> _handler;
    private readonly List<State> _activeInStates = [];

    internal EventHandler(
        Func<SagaContext<TState>, TMessage, CancellationToken, Task<VsaResult<Unit>>> handler,
        bool canInitiate = false)
    {
        _handler = handler;
        CanInitiate = canInitiate;
    }

    /// <inheritdoc />
    public Type MessageType => typeof(TMessage);

    /// <inheritdoc />
    public IReadOnlyList<State> ActiveInStates => _activeInStates;

    /// <inheritdoc />
    public bool CanInitiate { get; }

    /// <inheritdoc />
    public async Task<VsaResult<Unit>> HandleAsync(SagaContext<TState> context, object message, CancellationToken ct)
    {
        if (message is TMessage typedMessage)
        {
            return await _handler(context, typedMessage, ct);
        }

        return MessagingErrors.InvalidMessageType(message.GetType().Name, typeof(TMessage).Name);
    }

    internal void AddActiveState(State state) => _activeInStates.Add(state);
}
