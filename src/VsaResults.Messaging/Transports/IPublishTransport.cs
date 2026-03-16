using VsaResults.Features.Features;
using VsaResults.Messaging.Messages;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Transports;

/// <summary>
/// Transport for publishing events to multiple subscribers.
/// </summary>
public interface IPublishTransport
{
    /// <summary>
    /// Publishes a message to all subscribers.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="envelope">The message envelope.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Unit on success, or an error.</returns>
    Task<VsaResult<Unit>> PublishAsync<TMessage>(
        MessageEnvelope envelope,
        CancellationToken ct = default)
        where TMessage : class, IEvent;
}
