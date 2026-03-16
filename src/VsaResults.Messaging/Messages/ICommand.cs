namespace VsaResults.Messaging.Messages;

/// <summary>
/// Marker interface for command messages.
/// Commands have point-to-point semantics: they are sent to a specific endpoint
/// and should be processed by exactly one consumer.
/// </summary>
/// <remarks>
/// Use commands when you need to instruct another service to perform an action.
/// Commands imply a sender knows about a receiver.
/// Example: ProcessPayment, ShipOrder, SendEmail
/// </remarks>
public interface ICommand : IMessage
{
}
