using JetBrains.Annotations;
using VsaResults.Features.Features;
using VsaResults.Messaging.Faults;
using VsaResults.Messaging.Messages;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Consumers;

/// <summary>
/// Marker interface for message consumers.
/// All consumers must implement this interface directly or through <see cref="IConsumer{TMessage}"/>.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public interface IConsumer
{
}

/// <summary>
/// Consumer for a specific message type.
/// Returns <see cref="VsaResult{TValue}"/> to enable functional error handling.
/// When errors are returned, a <see cref="Fault{TMessage}"/> is automatically published.
/// </summary>
/// <typeparam name="TMessage">The type of message to consume.</typeparam>
/// <remarks>
/// <para>
/// Unlike MassTransit which uses exceptions for error handling, VsaResults.Messaging
/// uses the VsaResult pattern. Return errors instead of throwing exceptions.
/// </para>
/// <para>
/// Example:
/// <code>
/// public class OrderCreatedConsumer : IConsumer&lt;OrderCreated&gt;
/// {
///     public async Task&lt;ErrorOr&lt;Unit&gt;&gt; ConsumeAsync(
///         ConsumeContext&lt;OrderCreated&gt; context,
///         CancellationToken ct = default)
///     {
///         if (context.Message.Amount &lt;= 0)
///             return MessagingErrors.ConsumerFailed(nameof(OrderCreatedConsumer), "Invalid amount");
///
///         // Process the order...
///         return Unit.Value;
///     }
/// }
/// </code>
/// </para>
/// </remarks>
public interface IConsumer<TMessage> : IConsumer
    where TMessage : class, IMessage
{
    /// <summary>
    /// Consumes the message and returns a result.
    /// </summary>
    /// <param name="context">The consume context containing the message and metadata.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see cref="Unit.Value"/> on success, or errors on failure.
    /// On error, a <see cref="Fault{TMessage}"/> is automatically published.
    /// </returns>
    Task<VsaResult<Unit>> ConsumeAsync(
        ConsumeContext<TMessage> context,
        CancellationToken ct = default);
}

/// <summary>
/// Consumer that returns a typed result.
/// Useful for request-response patterns or when the consumer produces a value.
/// </summary>
/// <typeparam name="TMessage">The type of message to consume.</typeparam>
/// <typeparam name="TResult">The type of result produced.</typeparam>
public interface IConsumer<TMessage, TResult> : IConsumer
    where TMessage : class, IMessage
{
    /// <summary>
    /// Consumes the message and returns a typed result.
    /// </summary>
    /// <param name="context">The consume context containing the message and metadata.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The result on success, or errors on failure.
    /// On error, a <see cref="Fault{TMessage}"/> is automatically published.
    /// </returns>
    Task<VsaResult<TResult>> ConsumeAsync(
        ConsumeContext<TMessage> context,
        CancellationToken ct = default);
}
