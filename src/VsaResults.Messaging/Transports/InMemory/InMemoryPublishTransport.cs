using System.Collections.Concurrent;
using VsaResults.Features.Features;
using VsaResults.Messaging.Messages;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Transports.InMemory;

/// <summary>
/// In-memory publish transport.
/// Routes messages to exchanges based on message type.
/// </summary>
internal sealed class InMemoryPublishTransport : IPublishTransport
{
    private readonly ConcurrentDictionary<string, InMemoryExchange> _exchanges;

    public InMemoryPublishTransport(
        ConcurrentDictionary<string, InMemoryExchange> exchanges)
    {
        _exchanges = exchanges;
    }

    /// <inheritdoc />
    public async Task<VsaResult<Unit>> PublishAsync<TMessage>(
        MessageEnvelope envelope,
        CancellationToken ct = default)
        where TMessage : class, IEvent
    {
        // Publish to all exchanges that match the message types
        foreach (var messageType in envelope.MessageTypes)
        {
            if (_exchanges.TryGetValue(messageType, out var exchange))
            {
                await exchange.PublishAsync(envelope, ct);
            }
        }

        return Unit.Value;
    }
}
