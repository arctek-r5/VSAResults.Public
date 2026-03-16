using VsaResults.Features.Features;
using VsaResults.Messaging.Messages;
using VsaResults.Messaging.Sagas;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.StateMachine;

/// <summary>
/// Fluent builder for constructing state machines.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
public sealed class StateMachineBuilder<TState>
    where TState : class, ISagaState, new()
{
    private readonly List<State> _states = [State.Initial, State.Final];
    private readonly Dictionary<Type, List<IEventHandler<TState>>> _handlers = [];
    private readonly Dictionary<Type, object> _correlationExtractors = [];
    private State _initialState = State.Initial;
    private Action<TState>? _onCompleteCallback;

    /// <summary>
    /// Defines a new state in the state machine.
    /// </summary>
    /// <param name="name">The state name.</param>
    /// <returns>The created state.</returns>
    public State DefineState(string name)
    {
        var state = State.Create(name);
        if (!_states.Any(s => s.Name == name))
        {
            _states.Add(state);
        }

        return state;
    }

    /// <summary>
    /// Sets the initial state for new saga instances.
    /// </summary>
    /// <param name="state">The initial state.</param>
    /// <returns>This builder for method chaining.</returns>
    public StateMachineBuilder<TState> InitiallyIn(State state)
    {
        _initialState = state;
        return this;
    }

    /// <summary>
    /// Configures correlation ID extraction for a message type.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="extractor">Function to extract correlation ID from the message.</param>
    /// <returns>This builder for method chaining.</returns>
    public StateMachineBuilder<TState> CorrelateBy<TMessage>(Func<TMessage, Guid> extractor)
        where TMessage : class, IMessage
    {
        _correlationExtractors[typeof(TMessage)] = extractor;
        return this;
    }

    /// <summary>
    /// Configures an event that can initiate a new saga instance.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="handler">The handler for the initiating event.</param>
    /// <returns>This builder for method chaining.</returns>
    public StateMachineBuilder<TState> Initially<TMessage>(
        Func<SagaContext<TState>, TMessage, CancellationToken, Task<VsaResult<Unit>>> handler)
        where TMessage : class, IMessage
    {
        var eventHandler = new EventHandler<TState, TMessage>(handler, canInitiate: true);
        eventHandler.AddActiveState(State.Initial);

        AddHandler(eventHandler);
        return this;
    }

    /// <summary>
    /// Configures an event that can initiate a new saga instance with async initializer.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="handler">The handler for the initiating event.</param>
    /// <returns>This builder for method chaining.</returns>
    public StateMachineBuilder<TState> Initially<TMessage>(
        Func<SagaContext<TState>, TMessage, Task<VsaResult<Unit>>> handler)
        where TMessage : class, IMessage
    {
        return Initially<TMessage>((ctx, msg, _) => handler(ctx, msg));
    }

    /// <summary>
    /// Begins configuring handlers for a specific state.
    /// </summary>
    /// <param name="state">The state to configure.</param>
    /// <returns>A state configurator for fluent chaining.</returns>
    public StateConfigurator<TState> During(State state)
    {
        return new StateConfigurator<TState>(this, state);
    }

    /// <summary>
    /// Begins configuring handlers for multiple states.
    /// </summary>
    /// <param name="states">The states to configure.</param>
    /// <returns>A state configurator for fluent chaining.</returns>
    public StateConfigurator<TState> During(params State[] states)
    {
        return new StateConfigurator<TState>(this, states);
    }

    /// <summary>
    /// Configures an event handler that applies to any state.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="handler">The handler for the event.</param>
    /// <returns>This builder for method chaining.</returns>
    public StateMachineBuilder<TState> DuringAny<TMessage>(
        Func<SagaContext<TState>, TMessage, CancellationToken, Task<VsaResult<Unit>>> handler)
        where TMessage : class, IMessage
    {
        var eventHandler = new EventHandler<TState, TMessage>(handler);
        AddHandler(eventHandler);
        return this;
    }

    /// <summary>
    /// Configures a completion handler that fires when entering the Final state.
    /// </summary>
    /// <param name="onComplete">Action to execute on completion.</param>
    /// <returns>This builder for method chaining.</returns>
    public StateMachineBuilder<TState> WhenComplete(Action<TState> onComplete)
    {
        _onCompleteCallback = onComplete;
        return this;
    }

    /// <summary>
    /// Builds the state machine definition.
    /// </summary>
    /// <returns>The built state machine.</returns>
    public IStateMachine<TState> Build()
    {
        return new BuiltStateMachine<TState>(
            [.. _states],
            _handlers.ToDictionary(
                kvp => kvp.Key,
                kvp => (IReadOnlyList<IEventHandler<TState>>)[.. kvp.Value]),
            _correlationExtractors,
            _initialState,
            _onCompleteCallback);
    }

    internal void AddHandler(IEventHandler<TState> handler)
    {
        if (!_handlers.TryGetValue(handler.MessageType, out var handlers))
        {
            handlers = [];
            _handlers[handler.MessageType] = handlers;
        }

        handlers.Add(handler);
    }
}

/// <summary>
/// Configurator for handlers within specific states.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
public sealed class StateConfigurator<TState>
    where TState : class, ISagaState, new()
{
    private readonly StateMachineBuilder<TState> _builder;
    private readonly State[] _states;

    internal StateConfigurator(StateMachineBuilder<TState> builder, State state)
    {
        _builder = builder;
        _states = [state];
    }

    internal StateConfigurator(StateMachineBuilder<TState> builder, State[] states)
    {
        _builder = builder;
        _states = states;
    }

    /// <summary>
    /// Configures an event handler for these states.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="handler">The handler for the event.</param>
    /// <returns>This configurator for method chaining.</returns>
    public StateConfigurator<TState> When<TMessage>(
        Func<SagaContext<TState>, TMessage, CancellationToken, Task<VsaResult<Unit>>> handler)
        where TMessage : class, IMessage
    {
        var eventHandler = new EventHandler<TState, TMessage>(handler);
        foreach (var state in _states)
        {
            eventHandler.AddActiveState(state);
        }

        _builder.AddHandler(eventHandler);
        return this;
    }

    /// <summary>
    /// Configures an event handler for these states (without cancellation token).
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="handler">The handler for the event.</param>
    /// <returns>This configurator for method chaining.</returns>
    public StateConfigurator<TState> When<TMessage>(
        Func<SagaContext<TState>, TMessage, Task<VsaResult<Unit>>> handler)
        where TMessage : class, IMessage
    {
        return When<TMessage>((ctx, msg, _) => handler(ctx, msg));
    }

    /// <summary>
    /// Configures an event handler that transitions to a new state.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="targetState">The state to transition to.</param>
    /// <returns>This configurator for method chaining.</returns>
    public StateConfigurator<TState> TransitionTo<TMessage>(State targetState)
        where TMessage : class, IMessage
    {
        return When<TMessage>((ctx, _, _) =>
        {
            ctx.TransitionTo(targetState.Name);
            return Task.FromResult<VsaResult<Unit>>(Unit.Value);
        });
    }

    /// <summary>
    /// Configures an event handler that completes the saga.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <returns>This configurator for method chaining.</returns>
    public StateConfigurator<TState> Finalize<TMessage>()
        where TMessage : class, IMessage
    {
        return When<TMessage>((ctx, _, _) =>
        {
            ctx.SetComplete();
            return Task.FromResult<VsaResult<Unit>>(Unit.Value);
        });
    }

    /// <summary>
    /// Chains to configure another state.
    /// </summary>
    /// <param name="state">The state to configure.</param>
    /// <returns>A new state configurator.</returns>
    public StateConfigurator<TState> During(State state)
    {
        return _builder.During(state);
    }
}

/// <summary>
/// Built state machine implementation.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
internal sealed class BuiltStateMachine<TState> : IStateMachine<TState>
    where TState : class, ISagaState, new()
{
    private readonly Dictionary<Type, object> _correlationExtractors;

    public BuiltStateMachine(
        List<State> states,
        Dictionary<Type, IReadOnlyList<IEventHandler<TState>>> handlers,
        Dictionary<Type, object> correlationExtractors,
        State initialState,
        Action<TState>? onCompleteCallback)
    {
        States = states;
        EventHandlers = handlers;
        _correlationExtractors = correlationExtractors;
        InitialState = initialState;
        OnCompleteCallback = onCompleteCallback;
        Events = handlers.Keys.ToList();
    }

    public Type StateType => typeof(TState);
    public IReadOnlyList<State> States { get; }
    public IReadOnlyList<Type> Events { get; }
    public State InitialState { get; }
    public Action<TState>? OnCompleteCallback { get; }
    public IReadOnlyDictionary<Type, IReadOnlyList<IEventHandler<TState>>> EventHandlers { get; }

    public Func<TMessage, Guid>? GetCorrelationIdExtractor<TMessage>()
        where TMessage : class, IMessage
    {
        if (_correlationExtractors.TryGetValue(typeof(TMessage), out var extractor))
        {
            return (Func<TMessage, Guid>)extractor;
        }

        return null;
    }
}
