using System.Threading.Channels;
using VsaResults.Messaging.Messages;

namespace VsaResults.Messaging.Transports.InMemory;

/// <summary>
/// In-memory queue for testing.
/// Uses a channel for async message passing.
/// </summary>
internal sealed class InMemoryQueue
{
    private readonly Channel<MessageEnvelope> _channel;

    /// <summary>Gets the queue name.</summary>
    public string Name { get; }

    /// <summary>
    /// Creates a new in-memory queue.
    /// </summary>
    /// <param name="name">The queue name.</param>
    /// <param name="capacity">Optional bounded capacity.</param>
    public InMemoryQueue(string name, int? capacity = null)
    {
        Name = name;

        _channel = capacity.HasValue
            ? Channel.CreateBounded<MessageEnvelope>(new BoundedChannelOptions(capacity.Value)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            })
            : Channel.CreateUnbounded<MessageEnvelope>(new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false
            });
    }

    /// <summary>
    /// Enqueues a message.
    /// </summary>
    public async Task EnqueueAsync(MessageEnvelope envelope, CancellationToken ct = default)
    {
        await _channel.Writer.WriteAsync(envelope, ct);
    }

    /// <summary>
    /// Tries to enqueue a message without waiting.
    /// </summary>
    public bool TryEnqueue(MessageEnvelope envelope)
    {
        return _channel.Writer.TryWrite(envelope);
    }

    /// <summary>
    /// Dequeues a message.
    /// </summary>
    public async Task<MessageEnvelope?> DequeueAsync(CancellationToken ct = default)
    {
        try
        {
            return await _channel.Reader.ReadAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (ChannelClosedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Tries to dequeue a message without waiting.
    /// </summary>
    public bool TryDequeue(out MessageEnvelope? envelope)
    {
        return _channel.Reader.TryRead(out envelope);
    }

    /// <summary>
    /// Gets an async enumerable of all messages.
    /// </summary>
    public IAsyncEnumerable<MessageEnvelope> ReadAllAsync(CancellationToken ct = default)
    {
        return _channel.Reader.ReadAllAsync(ct);
    }

    /// <summary>
    /// Gets the approximate count of messages in the queue.
    /// </summary>
    public int Count => _channel.Reader.Count;

    /// <summary>
    /// Marks the queue as complete (no more writes).
    /// </summary>
    public void Complete()
    {
        _channel.Writer.Complete();
    }
}
