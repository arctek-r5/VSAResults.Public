using VsaResults.Errors;
using VsaResults.Messaging.Consumers;
using VsaResults.Messaging.Messages;

namespace VsaResults.Messaging.Faults;

/// <summary>
/// Represents a message processing failure.
/// Automatically published when a consumer returns errors.
/// </summary>
/// <typeparam name="TMessage">The type of the original message that faulted.</typeparam>
/// <remarks>
/// <para>
/// Faults are published as events, allowing other services to react to failures.
/// Subscribe to <c>Fault&lt;TMessage&gt;</c> to handle failures of specific message types.
/// </para>
/// <para>
/// Unlike MassTransit which creates faults from exceptions, VsaResults.Messaging
/// creates faults from <see cref="Error"/> instances returned by consumers.
/// </para>
/// </remarks>
public sealed record Fault<TMessage> : IEvent
    where TMessage : class, IMessage
{
    /// <summary>Gets the original message that caused the fault.</summary>
    public required TMessage Message { get; init; }

    /// <summary>Gets the fault details.</summary>
    public required FaultContext FaultContext { get; init; }

    /// <summary>Gets the original message ID.</summary>
    public required MessageId MessageId { get; init; }

    /// <summary>Gets the correlation ID from the original message.</summary>
    public required CorrelationId CorrelationId { get; init; }

    /// <summary>Gets the conversation ID from the original message.</summary>
    public ConversationId? ConversationId { get; init; }

    /// <summary>Gets the timestamp when the fault occurred.</summary>
    public DateTimeOffset FaultedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Gets the host information where the fault occurred.</summary>
    public HostInfo? Host { get; init; }

    /// <summary>Gets the retry attempt number when the fault occurred.</summary>
    public int RetryAttempt { get; init; }

    /// <summary>Gets the consumer type that faulted.</summary>
    public string? ConsumerType { get; init; }

    /// <summary>Gets the endpoint where the fault occurred.</summary>
    public string? Endpoint { get; init; }

    /// <summary>
    /// Creates a fault from a consume context and errors.
    /// </summary>
    /// <param name="context">The consume context.</param>
    /// <param name="errors">The errors that caused the fault.</param>
    /// <param name="consumerType">The consumer type name.</param>
    /// <returns>A new fault instance.</returns>
    public static Fault<TMessage> Create(
        ConsumeContext<TMessage> context,
        IReadOnlyList<Error> errors,
        string? consumerType = null)
    {
        return new Fault<TMessage>
        {
            Message = context.Message,
            FaultContext = FaultContext.FromErrors(errors, consumerType),
            MessageId = context.MessageId,
            CorrelationId = context.CorrelationId,
            ConversationId = context.ConversationId,
            RetryAttempt = context.RetryAttempt,
            ConsumerType = consumerType,
            Endpoint = context.ReceiveEndpointName,
            Host = HostInfo.Current
        };
    }

    /// <summary>
    /// Creates a fault from a consume context and an exception.
    /// </summary>
    /// <param name="context">The consume context.</param>
    /// <param name="exception">The exception that caused the fault.</param>
    /// <param name="consumerType">The consumer type name.</param>
    /// <returns>A new fault instance.</returns>
    public static Fault<TMessage> Create(
        ConsumeContext<TMessage> context,
        Exception exception,
        string? consumerType = null)
    {
        return new Fault<TMessage>
        {
            Message = context.Message,
            FaultContext = FaultContext.FromException(exception, consumerType),
            MessageId = context.MessageId,
            CorrelationId = context.CorrelationId,
            ConversationId = context.ConversationId,
            RetryAttempt = context.RetryAttempt,
            ConsumerType = consumerType,
            Endpoint = context.ReceiveEndpointName,
            Host = HostInfo.Current
        };
    }
}
