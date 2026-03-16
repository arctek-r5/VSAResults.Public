using System.Collections.Concurrent;
using VsaResults.Messaging.Messages;

namespace VsaResults.Messaging.Transports.InMemory;

/// <summary>
/// In-memory exchange for fanout message routing.
/// Simulates RabbitMQ exchange behavior for testing.
/// </summary>
internal sealed class InMemoryExchange
{
    private readonly ConcurrentDictionary<string, InMemoryQueue> _bindings = new();

    /// <summary>Gets the exchange name.</summary>
    public string Name { get; }

    /// <summary>
    /// Creates a new in-memory exchange.
    /// </summary>
    /// <param name="name">The exchange name.</param>
    public InMemoryExchange(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Binds a queue to this exchange.
    /// </summary>
    /// <param name="queue">The queue to bind.</param>
    /// <param name="routingKey">The routing key (for future use).</param>
    public void Bind(InMemoryQueue queue, string routingKey = "#")
    {
        _bindings.TryAdd(queue.Name, queue);
    }

    /// <summary>
    /// Unbinds a queue from this exchange.
    /// </summary>
    /// <param name="queueName">The queue name.</param>
    public void Unbind(string queueName)
    {
        _bindings.TryRemove(queueName, out _);
    }

    /// <summary>
    /// Gets all bound queues.
    /// </summary>
    public IEnumerable<InMemoryQueue> BoundQueues => _bindings.Values;

    /// <summary>
    /// Publishes a message to all bound queues.
    /// </summary>
    public async Task PublishAsync(MessageEnvelope envelope, CancellationToken ct = default)
    {
        var tasks = _bindings.Values.Select(q => q.EnqueueAsync(envelope, ct));
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Gets the count of bound queues.
    /// </summary>
    public int BindingCount => _bindings.Count;
}
