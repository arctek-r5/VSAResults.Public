namespace VsaResults.Messaging.Messages;

/// <summary>
/// Marker interface for event messages.
/// Events have publish-subscribe semantics: they are published to all interested subscribers
/// and can be processed by zero or more consumers.
/// </summary>
/// <remarks>
/// Use events to notify other services that something has happened.
/// Events imply the publisher does not know about subscribers.
/// Example: OrderCreated, PaymentProcessed, UserRegistered
/// </remarks>
public interface IEvent : IMessage
{
}
