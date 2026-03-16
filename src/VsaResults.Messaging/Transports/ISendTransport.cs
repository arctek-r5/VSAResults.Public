using VsaResults.Features.Features;
using VsaResults.Messaging.Messages;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Transports;

/// <summary>
/// Transport for sending messages to a specific endpoint.
/// </summary>
public interface ISendTransport
{
    /// <summary>Gets the destination address.</summary>
    EndpointAddress Address { get; }

    /// <summary>
    /// Sends a message to this transport's destination.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="envelope">The message envelope.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Unit on success, or an error.</returns>
    Task<VsaResult<Unit>> SendAsync<TMessage>(
        MessageEnvelope envelope,
        CancellationToken ct = default)
        where TMessage : class, IMessage;
}
