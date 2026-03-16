using System.Collections.Concurrent;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;
using VsaResults.Features.Features;
using VsaResults.Messaging.ErrorOr;
using VsaResults.Messaging.Transports;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.AzureServiceBus;

/// <summary>
/// Azure Service Bus transport implementation.
/// </summary>
public class AzureServiceBusTransport : ITransport
{
    private readonly AzureServiceBusTransportOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AzureServiceBusTransport>? _logger;
    private readonly SemaphoreSlim _clientLock = new(1, 1);
    private readonly ConcurrentDictionary<string, ServiceBusSender> _senders = new();
    private readonly List<AzureServiceBusReceiveEndpoint> _receiveEndpoints = [];

    private ServiceBusClient? _client;
    private ServiceBusAdministrationClient? _adminClient;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureServiceBusTransport"/> class.
    /// </summary>
    public AzureServiceBusTransport(
        AzureServiceBusTransportOptions options,
        IServiceProvider serviceProvider,
        ILogger<AzureServiceBusTransport>? logger = null)
    {
        options.Validate();
        _options = options;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Scheme => "azuresb";

    /// <summary>
    /// Gets the shared Service Bus client instance.
    /// </summary>
    internal ServiceBusClient? Client => _client;

    /// <summary>
    /// Gets the administration client for creating queues/topics.
    /// </summary>
    internal ServiceBusAdministrationClient? AdminClient => _adminClient;

    /// <inheritdoc />
    public async Task<VsaResult<IReceiveEndpoint>> CreateReceiveEndpointAsync(
        EndpointAddress address,
        Action<IReceiveEndpointConfigurator> configure,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();

        var clientResult = await EnsureClientAsync(ct);
        if (clientResult.IsError)
        {
            return clientResult.Errors.ToResult<IReceiveEndpoint>();
        }

        var configurator = new AzureServiceBusReceiveEndpointConfigurator(address);
        configure(configurator);

        var endpoint = new AzureServiceBusReceiveEndpoint(
            address,
            _client!,
            _adminClient,
            _options,
            _serviceProvider,
            configurator,
            _logger);

        _receiveEndpoints.Add(endpoint);

        VsaResult<IReceiveEndpoint> result = endpoint;
        return result;
    }

    /// <inheritdoc />
    public async Task<VsaResult<ISendTransport>> GetSendTransportAsync(
        EndpointAddress address,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();

        var clientResult = await EnsureClientAsync(ct);
        if (clientResult.IsError)
        {
            return clientResult.Errors.ToResult<ISendTransport>();
        }

        var sender = await GetOrCreateSenderAsync(address.Name, isQueue: true, ct);
        if (sender.IsError)
        {
            return sender.Errors.ToResult<ISendTransport>();
        }

        var transport = new AzureServiceBusSendTransport(address, sender.Value, _logger);
        VsaResult<ISendTransport> result = transport;
        return result;
    }

    /// <inheritdoc />
    public async Task<VsaResult<IPublishTransport>> GetPublishTransportAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        var clientResult = await EnsureClientAsync(ct);
        if (clientResult.IsError)
        {
            return clientResult.Errors.ToResult<IPublishTransport>();
        }

        var transport = new AzureServiceBusPublishTransport(this, _logger);
        VsaResult<IPublishTransport> result = transport;
        return result;
    }

    /// <summary>
    /// Gets or creates a sender for the specified queue or topic.
    /// </summary>
    internal async Task<VsaResult<ServiceBusSender>> GetOrCreateSenderAsync(
        string entityName,
        bool isQueue,
        CancellationToken ct = default)
    {
        var key = $"{(isQueue ? "queue" : "topic")}:{entityName}";

        if (_senders.TryGetValue(key, out var existingSender) && !existingSender.IsClosed)
        {
            return existingSender;
        }

        try
        {
            // Ensure entity exists if auto-create is enabled
            if (isQueue && _options.AutoCreateQueues && _adminClient is not null)
            {
                await EnsureQueueExistsAsync(entityName, ct);
            }
            else if (!isQueue && _options.AutoCreateTopics && _adminClient is not null)
            {
                await EnsureTopicExistsAsync(entityName, ct);
            }

            var sender = _client!.CreateSender(entityName);
            _senders[key] = sender;

            _logger?.LogDebug(
                "Created Service Bus sender for {EntityType} '{EntityName}'",
                isQueue ? "queue" : "topic",
                entityName);

            return sender;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create sender for {EntityName}", entityName);
            return MessagingErrors.TransportError($"Failed to create sender: {ex.Message}");
        }
    }

    /// <summary>
    /// Ensures the Service Bus client is initialized.
    /// </summary>
    internal async Task<VsaResult<Unit>> EnsureClientAsync(CancellationToken ct = default)
    {
        if (_client is not null)
        {
            return Unit.Value;
        }

        await _clientLock.WaitAsync(ct);
        try
        {
            if (_client is not null)
            {
                return Unit.Value;
            }

            _logger?.LogInformation(
                "Creating Azure Service Bus client for {Endpoint}",
                _options.SafeConnectionString);

            var clientOptions = _options.BuildClientOptions();

            if (!string.IsNullOrEmpty(_options.ConnectionString))
            {
                _client = new ServiceBusClient(_options.ConnectionString, clientOptions);
                _adminClient = new ServiceBusAdministrationClient(_options.ConnectionString);
            }
            else
            {
                _client = new ServiceBusClient(
                    _options.FullyQualifiedNamespace!,
                    _options.Credential!,
                    clientOptions);
                _adminClient = new ServiceBusAdministrationClient(
                    _options.FullyQualifiedNamespace!,
                    _options.Credential!);
            }

            _logger?.LogInformation("Azure Service Bus client created successfully");
            return Unit.Value;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create Azure Service Bus client");
            return MessagingErrors.TransportError($"Failed to create Service Bus client: {ex.Message}");
        }
        finally
        {
            _clientLock.Release();
        }
    }

    /// <summary>
    /// Ensures a queue exists, creating it if necessary.
    /// </summary>
    internal async Task EnsureQueueExistsAsync(string queueName, CancellationToken ct = default)
    {
        if (_adminClient is null || !_options.AutoCreateQueues)
        {
            return;
        }

        try
        {
            var exists = await _adminClient.QueueExistsAsync(queueName, ct);
            if (!exists)
            {
                _logger?.LogInformation("Creating queue '{QueueName}'", queueName);
                await _adminClient.CreateQueueAsync(queueName, ct);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Could not verify/create queue '{QueueName}'", queueName);
        }
    }

    /// <summary>
    /// Ensures a topic exists, creating it if necessary.
    /// </summary>
    internal async Task EnsureTopicExistsAsync(string topicName, CancellationToken ct = default)
    {
        if (_adminClient is null || !_options.AutoCreateTopics)
        {
            return;
        }

        try
        {
            var exists = await _adminClient.TopicExistsAsync(topicName, ct);
            if (!exists)
            {
                _logger?.LogInformation("Creating topic '{TopicName}'", topicName);
                await _adminClient.CreateTopicAsync(topicName, ct);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Could not verify/create topic '{TopicName}'", topicName);
        }
    }

    /// <summary>
    /// Ensures a subscription exists on a topic, creating it if necessary.
    /// </summary>
    internal async Task EnsureSubscriptionExistsAsync(
        string topicName,
        string subscriptionName,
        CancellationToken ct = default)
    {
        if (_adminClient is null || !_options.AutoCreateSubscriptions)
        {
            return;
        }

        try
        {
            await EnsureTopicExistsAsync(topicName, ct);

            var exists = await _adminClient.SubscriptionExistsAsync(topicName, subscriptionName, ct);
            if (!exists)
            {
                _logger?.LogInformation(
                    "Creating subscription '{SubscriptionName}' on topic '{TopicName}'",
                    subscriptionName,
                    topicName);
                await _adminClient.CreateSubscriptionAsync(topicName, subscriptionName, ct);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(
                ex,
                "Could not verify/create subscription '{SubscriptionName}' on topic '{TopicName}'",
                subscriptionName,
                topicName);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (_isDisposed)
        {
            return;
        }

        // Stop all receive endpoints
        foreach (var endpoint in _receiveEndpoints)
        {
            await endpoint.DisposeAsync();
        }

        _receiveEndpoints.Clear();

        // Dispose all senders
        foreach (var sender in _senders.Values)
        {
            await sender.DisposeAsync();
        }

        _senders.Clear();

        // Dispose client
        if (_client is not null)
        {
            await _client.DisposeAsync();
            _client = null;
        }

        _clientLock.Dispose();
        _isDisposed = true;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, nameof(AzureServiceBusTransport));
    }
}
