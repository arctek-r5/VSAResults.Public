using Microsoft.Extensions.Logging;
using VsaResults.Features.Features;
using VsaResults.Messaging.Bus;
using VsaResults.Messaging.ErrorOr;
using VsaResults.Messaging.Messages;
using VsaResults.Messaging.StateMachine;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Sagas;

/// <summary>
/// Routes incoming messages to saga handlers, manages state lifecycle, and persists state.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
internal sealed class SagaDispatcher<TState>
    where TState : class, ISagaState, new()
{
    private readonly IStateMachine<TState> _stateMachine;
    private readonly ISagaRepository<TState> _repository;
    private readonly IBus _bus;
    private readonly ILogger<SagaDispatcher<TState>> _logger;

    private const int MaxConcurrencyRetries = 3;

    public SagaDispatcher(
        IStateMachine<TState> stateMachine,
        ISagaRepository<TState> repository,
        IBus bus,
        ILogger<SagaDispatcher<TState>> logger)
    {
        _stateMachine = stateMachine;
        _repository = repository;
        _bus = bus;
        _logger = logger;
    }

    /// <summary>
    /// Dispatches a message to the appropriate saga handler.
    /// </summary>
    /// <param name="message">The message to dispatch.</param>
    /// <param name="envelope">The message envelope containing metadata.</param>
    /// <param name="correlationId">The correlation ID identifying the saga instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Unit on success, or errors on failure.</returns>
    public async Task<VsaResult<Unit>> DispatchAsync(
        object message,
        MessageEnvelope envelope,
        Guid correlationId,
        CancellationToken ct)
    {
        var messageType = message.GetType();

        if (!_stateMachine.EventHandlers.TryGetValue(messageType, out var handlers))
        {
            return MessagingErrors.InvalidMessageType(messageType.Name, "registered saga event");
        }

        for (var attempt = 0; attempt < MaxConcurrencyRetries; attempt++)
        {
            try
            {
                return await DispatchCoreAsync(message, envelope, correlationId, handlers, ct);
            }
            catch (SagaConcurrencyException ex)
            {
                if (attempt == MaxConcurrencyRetries - 1)
                {
                    _logger.LogWarning(
                        ex,
                        "Saga concurrency conflict for {SagaType} {CorrelationId} after {Attempts} retries",
                        typeof(TState).Name, correlationId, MaxConcurrencyRetries);

                    return MessagingErrors.SagaConcurrencyConflict(CorrelationId.From(correlationId));
                }

                _logger.LogDebug(
                    "Saga concurrency conflict for {SagaType} {CorrelationId}, retrying (attempt {Attempt})",
                    typeof(TState).Name, correlationId, attempt + 1);
            }
        }

        return MessagingErrors.SagaConcurrencyConflict(CorrelationId.From(correlationId));
    }

    private async Task<VsaResult<Unit>> DispatchCoreAsync(
        object message,
        MessageEnvelope envelope,
        Guid correlationId,
        IReadOnlyList<IEventHandler<TState>> handlers,
        CancellationToken ct)
    {
        // Load existing state
        var loadResult = await _repository.GetAsync(correlationId, ct);
        TState? existingState = null;
        var isNew = false;

        if (loadResult.IsError)
        {
            // SagaNotFound is expected for new sagas — any other error is unexpected
            if (loadResult.FirstError.Code == MessagingErrors.Codes.SagaNotFound)
            {
                isNew = true;
            }
            else
            {
                return loadResult.Errors.ToResult<Unit>();
            }
        }
        else
        {
            existingState = loadResult.Value;
        }

        // Find applicable handlers
        var applicableHandlers = FindApplicableHandlers(handlers, existingState?.CurrentState, isNew);
        if (applicableHandlers.Count == 0)
        {
            if (isNew)
            {
                return MessagingErrors.SagaNotFound(CorrelationId.From(correlationId));
            }

            // No handlers match the current state — silently skip (idempotency)
            _logger.LogDebug(
                "No handlers match current state {CurrentState} for {SagaType} {CorrelationId}",
                existingState!.CurrentState, typeof(TState).Name, correlationId);

            return Unit.Value;
        }

        // Create new state if initiating
        TState state;
        if (isNew)
        {
            state = new TState
            {
                CorrelationId = correlationId,
                CurrentState = _stateMachine.InitialState.Name,
                CreatedAt = DateTimeOffset.UtcNow,
                ModifiedAt = DateTimeOffset.UtcNow
            };
        }
        else
        {
            state = existingState!;
        }

        var previousState = state.CurrentState;

        // Create context and execute handlers
        var context = new SagaContext<TState>(state, _bus, envelope, isNew);

        foreach (var handler in applicableHandlers)
        {
            var result = await handler.HandleAsync(context, message, ct);
            if (result.IsError)
            {
                _logger.LogWarning(
                    "Saga handler failed for {SagaType} {CorrelationId} in state {State}: {Error}",
                    typeof(TState).Name, correlationId, previousState,
                    result.FirstError.Description);

                return result;
            }
        }

        // Persist state
        var saveResult = await _repository.SaveAsync(state, ct);
        if (saveResult.IsError)
        {
            return saveResult;
        }

        // Flush pending outbound messages after successful persistence
        var flushResult = await context.FlushAsync(ct);
        if (flushResult.IsError)
        {
            _logger.LogWarning(
                "Failed to flush pending messages for {SagaType} {CorrelationId}: {Error}",
                typeof(TState).Name, correlationId, flushResult.FirstError.Description);
        }

        // Invoke completion callback only after the final state has been durably persisted.
        if (state.CurrentState == State.Final.Name && _stateMachine.OnCompleteCallback is not null)
        {
            _stateMachine.OnCompleteCallback(state);
        }

        _logger.LogDebug(
            "Saga {SagaType} {CorrelationId} transitioned from {PreviousState} to {CurrentState}",
            typeof(TState).Name, correlationId, previousState, state.CurrentState);

        return Unit.Value;
    }

    private static List<IEventHandler<TState>> FindApplicableHandlers(
        IReadOnlyList<IEventHandler<TState>> handlers,
        string? currentState,
        bool isNew)
    {
        var applicable = new List<IEventHandler<TState>>();

        foreach (var handler in handlers)
        {
            if (isNew && handler.CanInitiate)
            {
                applicable.Add(handler);
                continue;
            }

            if (isNew)
            {
                continue;
            }

            // Handler with no state filter applies to all states
            if (handler.ActiveInStates.Count == 0)
            {
                applicable.Add(handler);
                continue;
            }

            // Handler applies if current state matches any active state
            if (handler.ActiveInStates.Any(s => s.Name == currentState))
            {
                applicable.Add(handler);
            }
        }

        return applicable;
    }
}
