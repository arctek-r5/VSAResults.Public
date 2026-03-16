using VsaResults.Features.Features;
using VsaResults.Messaging.Consumers;
using VsaResults.Messaging.Messages;
using VsaResults.Messaging.StateMachine;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Sagas;

/// <summary>
/// Consumer adapter that bridges the messaging pipeline to the saga dispatcher.
/// One instance is registered per (TState, TMessage) pair.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
/// <typeparam name="TMessage">The message type.</typeparam>
internal sealed class SagaMessageConsumer<TState, TMessage> : IConsumer<TMessage>
    where TState : class, ISagaState, new()
    where TMessage : class, IMessage
{
    private readonly SagaDispatcher<TState> _dispatcher;
    private readonly IStateMachine<TState> _stateMachine;

    public SagaMessageConsumer(
        SagaDispatcher<TState> dispatcher,
        IStateMachine<TState> stateMachine)
    {
        _dispatcher = dispatcher;
        _stateMachine = stateMachine;
    }

    public async Task<VsaResult<Unit>> ConsumeAsync(
        ConsumeContext<TMessage> context,
        CancellationToken ct = default)
    {
        var correlationId = ExtractCorrelationId(context);
        var result = await _dispatcher.DispatchAsync(context.Message, context.Envelope, correlationId, ct);

        if (result.IsError)
        {
            // Log the error so it's visible — don't let saga dispatch errors vanish silently.
            // This is a saga-level error (state not found, concurrency conflict, etc.),
            // NOT a business error — the saga handler already handled business failures
            // by transitioning to Faulted state.
            context.AddContext("saga_dispatch_error", string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}")));
        }

        return result;
    }

    private Guid ExtractCorrelationId(ConsumeContext<TMessage> context)
    {
        // Try the state machine's configured correlation extractor first
        var extractor = _stateMachine.GetCorrelationIdExtractor<TMessage>();
        if (extractor is not null)
        {
            return extractor(context.Message);
        }

        // Fall back to the envelope's correlation ID
        return context.CorrelationId;
    }
}
