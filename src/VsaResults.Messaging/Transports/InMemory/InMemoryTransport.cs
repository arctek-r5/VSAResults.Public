using System.Collections.Concurrent;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Transports.InMemory;

/// <summary>
/// In-memory transport for testing.
/// Simulates RabbitMQ topology with exchanges and queues.
/// </summary>
public sealed class InMemoryTransport : ITransport
{
    private readonly ConcurrentDictionary<string, InMemoryExchange> _exchanges = new();
    private readonly ConcurrentDictionary<string, InMemoryQueue> _queues = new();
    private readonly ConcurrentDictionary<string, InMemoryReceiveEndpoint> _endpoints = new();
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Creates a new in-memory transport.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    public InMemoryTransport(
        IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public string Scheme => "inmemory";

    /// <inheritdoc />
    public Task<VsaResult<IReceiveEndpoint>> CreateReceiveEndpointAsync(
        EndpointAddress address,
        Action<IReceiveEndpointConfigurator> configure,
        CancellationToken ct = default)
    {
        var queueName = address.Name;
        var queue = GetOrCreateQueue(queueName);

        var configurator = new InMemoryReceiveEndpointConfigurator(queueName);
        configure(configurator);

        var endpoint = new InMemoryReceiveEndpoint(
            address,
            queue,
            configurator,
            _serviceProvider);

        _endpoints.TryAdd(queueName, endpoint);

        // Bind queue to exchanges for message types
        foreach (var messageType in configurator.GetMessageTypes())
        {
            var exchange = GetOrCreateExchange(messageType);
            exchange.Bind(queue);
        }

        return Task.FromResult<VsaResult<IReceiveEndpoint>>(endpoint);
    }

    /// <inheritdoc />
    public Task<VsaResult<ISendTransport>> GetSendTransportAsync(
        EndpointAddress address,
        CancellationToken ct = default)
    {
        var queue = GetOrCreateQueue(address.Name);
        var transport = new InMemorySendTransport(address, queue);
        return Task.FromResult<VsaResult<ISendTransport>>(transport);
    }

    /// <inheritdoc />
    public Task<VsaResult<IPublishTransport>> GetPublishTransportAsync(
        CancellationToken ct = default)
    {
        var transport = new InMemoryPublishTransport(_exchanges);
        return Task.FromResult<VsaResult<IPublishTransport>>(transport);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        foreach (var endpoint in _endpoints.Values)
        {
            _ = endpoint.StopAsync();
        }

        foreach (var queue in _queues.Values)
        {
            queue.Complete();
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Gets or creates a queue.
    /// </summary>
    internal InMemoryQueue GetOrCreateQueue(string name)
    {
        return _queues.GetOrAdd(name, n => new InMemoryQueue(n));
    }

    /// <summary>
    /// Gets or creates an exchange.
    /// </summary>
    internal InMemoryExchange GetOrCreateExchange(string name)
    {
        return _exchanges.GetOrAdd(name, n => new InMemoryExchange(n));
    }

    /// <summary>
    /// Binds a queue to an exchange.
    /// </summary>
    internal void BindQueue(string exchangeName, string queueName, string routingKey = "#")
    {
        var exchange = GetOrCreateExchange(exchangeName);
        var queue = GetOrCreateQueue(queueName);
        exchange.Bind(queue, routingKey);
    }

    /// <summary>
    /// Gets all queues (for testing).
    /// </summary>
    internal IEnumerable<InMemoryQueue> Queues => _queues.Values;

    /// <summary>
    /// Gets all exchanges (for testing).
    /// </summary>
    internal IEnumerable<InMemoryExchange> Exchanges => _exchanges.Values;
}
