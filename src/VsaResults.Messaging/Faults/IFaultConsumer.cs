using VsaResults.Messaging.Consumers;
using VsaResults.Messaging.Messages;

namespace VsaResults.Messaging.Faults;

/// <summary>
/// Consumer for fault messages.
/// Implement this to handle failures of specific message types.
/// </summary>
/// <typeparam name="TMessage">The type of the original message that faulted.</typeparam>
/// <remarks>
/// <para>
/// Fault consumers are automatically subscribed to <c>Fault&lt;TMessage&gt;</c> events.
/// Use them to:
/// </para>
/// <list type="bullet">
/// <item><description>Send alerts for critical failures</description></item>
/// <item><description>Update dashboards with failure information</description></item>
/// <item><description>Store failed messages for later reprocessing</description></item>
/// <item><description>Trigger compensating transactions</description></item>
/// </list>
/// <para>
/// Example:
/// <code>
/// public class OrderFaultConsumer : IFaultConsumer&lt;OrderCreated&gt;
/// {
///     public async Task&lt;ErrorOr&lt;Unit&gt;&gt; ConsumeAsync(
///         ConsumeContext&lt;Fault&lt;OrderCreated&gt;&gt; context,
///         CancellationToken ct = default)
///     {
///         var fault = context.Message;
///         _logger.LogError(
///             "Order {OrderId} processing failed: {Error}",
///             fault.Message.OrderId,
///             fault.FaultContext.ErrorMessage);
///
///         await _alertService.SendAlertAsync(fault, ct);
///         return Unit.Value;
///     }
/// }
/// </code>
/// </para>
/// </remarks>
public interface IFaultConsumer<TMessage> : IConsumer<Fault<TMessage>>
    where TMessage : class, IMessage
{
}
