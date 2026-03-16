using VsaResults.Features.Features;
using VsaResults.Messaging.Bus;
using VsaResults.Messaging.Messages;
using VsaResults.Messaging.Transports;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Sagas;

/// <summary>
/// Context for saga execution, providing access to the saga state and messaging capabilities.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
public sealed class SagaContext<TState>
    where TState : class, ISagaState, new()
{
    private readonly IBus _bus;
    private readonly MessageEnvelope _envelope;
    private readonly List<object> _pendingPublishes = [];
    private readonly List<(EndpointAddress Address, object Message)> _pendingSends = [];

    internal SagaContext(
        TState state,
        IBus bus,
        MessageEnvelope envelope,
        bool isNew)
    {
        State = state;
        IsNew = isNew;
        _bus = bus;
        _envelope = envelope;
    }

    /// <summary>
    /// Gets the saga state instance.
    /// </summary>
    public TState State { get; }

    /// <summary>
    /// Gets a value indicating whether this is a new saga instance.
    /// </summary>
    public bool IsNew { get; }

    /// <summary>
    /// Gets the correlation ID for this saga instance.
    /// </summary>
    public Guid CorrelationId => State.CorrelationId;

    /// <summary>
    /// Gets the message envelope that triggered this saga execution.
    /// </summary>
    public MessageEnvelope Envelope => _envelope;

    /// <summary>
    /// Gets the message headers from the triggering message.
    /// </summary>
    public MessageHeaders Headers => _envelope.Headers;

    /// <summary>
    /// Transitions the saga to a new state.
    /// </summary>
    /// <param name="stateName">The name of the new state.</param>
    public void TransitionTo(string stateName)
    {
        State.CurrentState = stateName;
        State.ModifiedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Marks the saga as complete, transitioning to the Final state.
    /// </summary>
    public void SetComplete()
    {
        TransitionTo("Final");
    }

    /// <summary>
    /// Marks the saga as faulted, capturing the current state so retry
    /// handlers know which step to re-enter.
    /// </summary>
    public void SetFaulted()
    {
        State.FaultedAtState = State.CurrentState;
        TransitionTo("Faulted");
    }

    /// <summary>
    /// Queues an event to be published when the saga completes successfully.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="event">The event to publish.</param>
    public void Publish<TEvent>(TEvent @event)
        where TEvent : class, IEvent
    {
        _pendingPublishes.Add(@event);
    }

    /// <summary>
    /// Queues a command to be sent when the saga completes successfully.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <param name="address">The destination endpoint address.</param>
    /// <param name="command">The command to send.</param>
    public void Send<TCommand>(EndpointAddress address, TCommand command)
        where TCommand : class, ICommand
    {
        _pendingSends.Add((address, command));
    }

    /// <summary>
    /// Executes all pending publishes and sends.
    /// Called internally after successful saga execution.
    /// </summary>
    internal async Task<VsaResult<Unit>> FlushAsync(CancellationToken ct)
    {
        // Publish all pending events
        foreach (var @event in _pendingPublishes)
        {
            // IBus.PublishAsync is generic — find it by name then make it concrete
            var publishMethod = _bus.GetType()
                .GetMethods()
                .FirstOrDefault(m =>
                    m.Name == nameof(IBus.PublishAsync) &&
                    m.IsGenericMethod &&
                    m.GetParameters().Length == 2 &&
                    m.GetParameters()[1].ParameterType == typeof(CancellationToken));

            if (publishMethod is null)
            {
                throw new InvalidOperationException(
                    $"Could not find generic {nameof(IBus.PublishAsync)} method on bus type {_bus.GetType().FullName}.");
            }

            var genericMethod = publishMethod.MakeGenericMethod(@event.GetType());
            var task = (Task<VsaResult<Unit>>)genericMethod.Invoke(_bus, [@event, ct])!;
            var result = await task;

            if (result.IsError)
            {
                return result.Errors.ToResult<Unit>();
            }
        }

        // Send all pending commands
        foreach (var (address, command) in _pendingSends)
        {
            var endpointResult = await _bus.GetSendEndpointAsync(address, ct);
            if (endpointResult.IsError)
            {
                return endpointResult.Errors.ToResult<Unit>();
            }

            // ISendEndpoint.SendAsync is generic — find it by name then make it concrete
            var endpointType = endpointResult.Value.GetType();
            var sendMethod = endpointType
                .GetMethods()
                .FirstOrDefault(m =>
                    m.Name == nameof(ISendEndpoint.SendAsync) &&
                    m.IsGenericMethod &&
                    m.GetParameters().Length == 2 &&
                    m.GetParameters()[1].ParameterType == typeof(CancellationToken));

            if (sendMethod is null)
            {
                throw new InvalidOperationException(
                    $"Could not find generic {nameof(ISendEndpoint.SendAsync)} method on send endpoint type {endpointType.FullName}.");
            }

            var genericMethod = sendMethod.MakeGenericMethod(command.GetType());
            var task = (Task<VsaResult<Unit>>)genericMethod.Invoke(endpointResult.Value, [command, ct])!;
            var result = await task;

            if (result.IsError)
            {
                return result.Errors.ToResult<Unit>();
            }
        }

        return Unit.Value;
    }
}
