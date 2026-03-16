using VsaResults.Features.Features;
using VsaResults.Messaging.Messages;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Transports.InMemory;

/// <summary>
/// In-memory send transport.
/// </summary>
internal sealed class InMemorySendTransport : ISendTransport
{
    private readonly InMemoryQueue _queue;

    public InMemorySendTransport(EndpointAddress address, InMemoryQueue queue)
    {
        Address = address;
        _queue = queue;
    }

    /// <inheritdoc />
    public EndpointAddress Address { get; }

    /// <inheritdoc />
    public async Task<VsaResult<Unit>> SendAsync<TMessage>(
        MessageEnvelope envelope,
        CancellationToken ct = default)
        where TMessage : class, IMessage
    {
        await _queue.EnqueueAsync(envelope, ct);
        return Unit.Value;
    }
}
