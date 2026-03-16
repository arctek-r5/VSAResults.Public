using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VsaResults.Features.Features;
using VsaResults.Messaging.Consumers;
using VsaResults.Messaging.ErrorOr;
using VsaResults.Messaging.Messages;
using VsaResults.Messaging.Pipeline;
using VsaResults.Messaging.Retry;
using VsaResults.Messaging.Serialization;
using VsaResults.Messaging.Transports;
using VsaResults.Messaging.Transports.InMemory;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.AzureServiceBus;

/// <summary>
/// Azure Service Bus receive endpoint implementation.
/// </summary>
public class AzureServiceBusReceiveEndpoint : IReceiveEndpoint
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusAdministrationClient? _adminClient;
    private readonly AzureServiceBusTransportOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly AzureServiceBusReceiveEndpointConfigurator _configurator;
    private readonly ILogger? _logger;

    private readonly List<ServiceBusProcessor> _processors = [];
    private bool _isRunning;

    internal AzureServiceBusReceiveEndpoint(
        EndpointAddress address,
        ServiceBusClient client,
        ServiceBusAdministrationClient? adminClient,
        AzureServiceBusTransportOptions options,
        IServiceProvider serviceProvider,
        AzureServiceBusReceiveEndpointConfigurator configurator,
        ILogger? logger)
    {
        Address = address;
        _client = client;
        _adminClient = adminClient;
        _options = options;
        _serviceProvider = serviceProvider;
        _configurator = configurator;
        _logger = logger;

        Name = address.Name;
    }

    /// <inheritdoc />
    public EndpointAddress Address { get; }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public bool IsRunning => _isRunning;

    /// <inheritdoc />
    public async Task<VsaResult<Unit>> StartAsync(CancellationToken ct = default)
    {
        if (_isRunning)
        {
            return Unit.Value;
        }

        try
        {
            var processorOptions = _options.BuildProcessorOptions();

            // Apply endpoint-specific overrides
            if (_configurator.ConcurrencyLimit.HasValue)
            {
                processorOptions.MaxConcurrentCalls = _configurator.ConcurrencyLimit.Value;
            }

            if (_configurator.PrefetchCount.HasValue)
            {
                processorOptions.PrefetchCount = _configurator.PrefetchCount.Value;
            }

            // Get the message types this endpoint handles
            var messageTypes = _configurator.GetMessageTypes().ToList();

            if (messageTypes.Count == 0)
            {
                // Create processor for the endpoint queue directly
                await CreateQueueProcessorAsync(Name, processorOptions, ct);
            }
            else
            {
                // Create processors for each message type's topic subscription
                foreach (var messageType in messageTypes.Distinct())
                {
                    var topicName = AzureServiceBusPublishTransport.NormalizeTopicName(messageType);
                    var subscriptionName = $"{Name}{_options.SubscriptionSuffix}";

                    await CreateTopicSubscriptionProcessorAsync(
                        topicName,
                        subscriptionName,
                        processorOptions,
                        ct);
                }
            }

            // Start all processors
            foreach (var processor in _processors)
            {
                await processor.StartProcessingAsync(ct);
            }

            _isRunning = true;

            _logger?.LogInformation(
                "Azure Service Bus endpoint '{EndpointName}' started with {ProcessorCount} processors and {ConsumerCount} consumer registrations",
                Name,
                _processors.Count,
                _configurator.Consumers.Count);

            return Unit.Value;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start Azure Service Bus endpoint '{EndpointName}'", Name);
            return MessagingErrors.TransportError($"Failed to start endpoint: {ex.Message}");
        }
    }

    private async Task CreateQueueProcessorAsync(
        string queueName,
        ServiceBusProcessorOptions processorOptions,
        CancellationToken ct)
    {
        // Ensure queue exists
        if (_adminClient is not null && _options.AutoCreateQueues)
        {
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

        var processor = _client.CreateProcessor(queueName, processorOptions);
        processor.ProcessMessageAsync += OnProcessMessageAsync;
        processor.ProcessErrorAsync += OnProcessErrorAsync;

        _processors.Add(processor);

        _logger?.LogDebug("Created processor for queue '{QueueName}'", queueName);
    }

    private async Task CreateTopicSubscriptionProcessorAsync(
        string topicName,
        string subscriptionName,
        ServiceBusProcessorOptions processorOptions,
        CancellationToken ct)
    {
        // Ensure topic and subscription exist
        if (_adminClient is not null)
        {
            try
            {
                if (_options.AutoCreateTopics)
                {
                    var topicExists = await _adminClient.TopicExistsAsync(topicName, ct);
                    if (!topicExists)
                    {
                        _logger?.LogInformation("Creating topic '{TopicName}'", topicName);
                        await _adminClient.CreateTopicAsync(topicName, ct);
                    }
                }

                if (_options.AutoCreateSubscriptions)
                {
                    var subExists = await _adminClient.SubscriptionExistsAsync(topicName, subscriptionName, ct);
                    if (!subExists)
                    {
                        _logger?.LogInformation(
                            "Creating subscription '{SubscriptionName}' on topic '{TopicName}'",
                            subscriptionName,
                            topicName);
                        await _adminClient.CreateSubscriptionAsync(topicName, subscriptionName, ct);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(
                    ex,
                    "Could not verify/create topic '{TopicName}' or subscription '{SubscriptionName}'",
                    topicName,
                    subscriptionName);
            }
        }

        var processor = _client.CreateProcessor(topicName, subscriptionName, processorOptions);
        processor.ProcessMessageAsync += OnProcessMessageAsync;
        processor.ProcessErrorAsync += OnProcessErrorAsync;

        _processors.Add(processor);

        _logger?.LogDebug(
            "Created processor for topic '{TopicName}' subscription '{SubscriptionName}'",
            topicName,
            subscriptionName);
    }

    /// <inheritdoc />
    public async Task<VsaResult<Unit>> StopAsync(CancellationToken ct = default)
    {
        if (!_isRunning)
        {
            return Unit.Value;
        }

        try
        {
            foreach (var processor in _processors)
            {
                await processor.StopProcessingAsync(ct);
            }

            _isRunning = false;
            _logger?.LogInformation("Azure Service Bus endpoint '{EndpointName}' stopped", Name);

            return Unit.Value;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error stopping Azure Service Bus endpoint '{EndpointName}'", Name);
            return MessagingErrors.TransportError($"Failed to stop endpoint: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        await StopAsync();

        foreach (var processor in _processors)
        {
            await processor.DisposeAsync();
        }

        _processors.Clear();
    }

    private async Task OnProcessMessageAsync(ProcessMessageEventArgs args)
    {
        try
        {
            var envelope = ParseEnvelope(args.Message);

            _logger?.LogDebug(
                "Received message {MessageId} on endpoint '{Endpoint}'",
                envelope.MessageId,
                Name);

            await ProcessEnvelopeAsync(envelope, args.CancellationToken);

            // Complete the message after successful processing
            await args.CompleteMessageAsync(args.Message);

            _logger?.LogDebug(
                "Completed message {MessageId} on endpoint '{Endpoint}'",
                envelope.MessageId,
                Name);
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "Error processing message {MessageId} on endpoint '{Endpoint}'",
                args.Message.MessageId,
                Name);

            // Abandon the message to make it available for retry
            await args.AbandonMessageAsync(args.Message);
        }
    }

    private Task OnProcessErrorAsync(ProcessErrorEventArgs args)
    {
        _logger?.LogError(
            args.Exception,
            "Azure Service Bus processor error on endpoint '{Endpoint}': Source={ErrorSource}",
            Name,
            args.ErrorSource);

        return Task.CompletedTask;
    }

    private static MessageEnvelope ParseEnvelope(ServiceBusReceivedMessage message)
    {
        var props = message.ApplicationProperties;

        // Parse message types from Subject or ApplicationProperties
        var messageTypesStr = message.Subject
            ?? GetPropertyString(props, "vsa-message-types")
            ?? string.Empty;

        var messageTypes = messageTypesStr
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        // Parse message ID
        var messageId = MessageId.New();
        if (!string.IsNullOrEmpty(message.MessageId))
        {
            var parseResult = MessageId.Parse(message.MessageId);
            if (!parseResult.IsError)
            {
                messageId = parseResult.Value;
            }
        }

        // Parse correlation ID
        var correlationId = CorrelationId.New();
        if (!string.IsNullOrEmpty(message.CorrelationId))
        {
            var parseResult = CorrelationId.Parse(message.CorrelationId);
            if (!parseResult.IsError)
            {
                correlationId = parseResult.Value;
            }
        }

        // Parse initiator ID
        MessageId? initiatorId = null;
        var initiatorIdStr = GetPropertyString(props, "vsa-initiator-id");
        if (!string.IsNullOrEmpty(initiatorIdStr))
        {
            var parseResult = MessageId.Parse(initiatorIdStr);
            if (!parseResult.IsError)
            {
                initiatorId = parseResult.Value;
            }
        }

        // Parse conversation ID
        ConversationId? conversationId = null;
        var conversationIdStr = GetPropertyString(props, "vsa-conversation-id");
        if (!string.IsNullOrEmpty(conversationIdStr))
        {
            var parseResult = ConversationId.Parse(conversationIdStr);
            if (!parseResult.IsError)
            {
                conversationId = parseResult.Value;
            }
        }

        // Parse sent time
        var sentTime = message.EnqueuedTime;
        var sentTimeStr = GetPropertyString(props, "vsa-sent-time");
        if (!string.IsNullOrEmpty(sentTimeStr) && DateTimeOffset.TryParse(sentTimeStr, out var parsedTime))
        {
            sentTime = parsedTime;
        }

        // Build custom headers from x- prefixed properties
        var customHeaders = new MessageHeaders();
        foreach (var (key, value) in props)
        {
            if (key.StartsWith("x-", StringComparison.Ordinal) && value is not null)
            {
                var headerKey = key[2..]; // Remove "x-" prefix
                customHeaders[headerKey] = value.ToString() ?? string.Empty;
            }
        }

        // Parse addresses
        var sourceAddressStr = GetPropertyString(props, "vsa-source-address");
        var destinationAddressStr = GetPropertyString(props, "vsa-destination-address");
        var faultAddressStr = GetPropertyString(props, "vsa-fault-address");

        return new MessageEnvelope
        {
            MessageId = messageId,
            CorrelationId = correlationId,
            ConversationId = conversationId,
            InitiatorId = initiatorId,
            MessageTypes = messageTypes,
            Body = message.Body.ToArray(),
            ContentType = message.ContentType ?? "application/json",
            Headers = customHeaders,
            SentTime = sentTime,
            ExpirationTime = message.ExpiresAt,
            SourceAddress = !string.IsNullOrEmpty(sourceAddressStr)
                ? EndpointAddress.Parse(sourceAddressStr).Match<EndpointAddress?>(a => a, _ => null)
                : null,
            DestinationAddress = !string.IsNullOrEmpty(destinationAddressStr)
                ? EndpointAddress.Parse(destinationAddressStr).Match<EndpointAddress?>(a => a, _ => null)
                : null,
            ResponseAddress = !string.IsNullOrEmpty(message.ReplyTo)
                ? EndpointAddress.Parse(message.ReplyTo).Match<EndpointAddress?>(a => a, _ => null)
                : null,
            FaultAddress = !string.IsNullOrEmpty(faultAddressStr)
                ? EndpointAddress.Parse(faultAddressStr).Match<EndpointAddress?>(a => a, _ => null)
                : null
        };
    }

    private static string? GetPropertyString(IReadOnlyDictionary<string, object> props, string key)
    {
        return props.TryGetValue(key, out var value) ? value.ToString() : null;
    }

    private async Task ProcessEnvelopeAsync(MessageEnvelope envelope, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();

        foreach (var registration in _configurator.GetConsumerRegistrations())
        {
            // Check if this consumer handles this message type
            var primaryType = envelope.MessageTypes.Count > 0 ? envelope.MessageTypes[0] : null;
            if (primaryType is null)
            {
                continue;
            }

            if (!registration.HandlesMessageType(primaryType))
            {
                continue;
            }

            await registration.InvokeAsync(scope.ServiceProvider, envelope, ct);
        }
    }
}

/// <summary>
/// Configurator for Azure Service Bus receive endpoints.
/// </summary>
internal sealed class AzureServiceBusReceiveEndpointConfigurator : IReceiveEndpointConfigurator
{
    private readonly EndpointAddress _address;
    private readonly List<ConsumerRegistration> _consumers = [];
    private readonly List<string> _messageTypes = [];
    private readonly MessageTypeResolver _typeResolver = new();

    public AzureServiceBusReceiveEndpointConfigurator(EndpointAddress address)
    {
        _address = address;
    }

    public string EndpointName => _address.Name;
    public IReadOnlyList<ConsumerRegistration> Consumers => _consumers;

    /// <summary>Gets or sets the prefetch count.</summary>
    public int? PrefetchCount { get; private set; }

    /// <summary>Gets or sets the concurrency limit.</summary>
    public int? ConcurrencyLimit { get; private set; }

    /// <inheritdoc />
    public void Consumer<TConsumer>()
        where TConsumer : class, IConsumer
    {
        var registration = new ConsumerRegistration(typeof(TConsumer));
        _consumers.Add(registration);

        // Register message types this consumer handles
        foreach (var messageType in registration.GetMessageTypeNames())
        {
            _messageTypes.Add(messageType);
        }
    }

    /// <inheritdoc />
    public void Consumer<TConsumer>(Func<IServiceProvider, TConsumer> factory)
        where TConsumer : class, IConsumer
    {
        var registration = new ConsumerRegistration(typeof(TConsumer), factory);
        _consumers.Add(registration);

        foreach (var messageType in registration.GetMessageTypeNames())
        {
            _messageTypes.Add(messageType);
        }
    }

    /// <inheritdoc />
    public void Handler<TMessage>(Func<ConsumeContext<TMessage>, CancellationToken, Task<VsaResult<Unit>>> handler)
        where TMessage : class, IMessage
    {
        var registration = new HandlerRegistration<TMessage>(handler);
        _consumers.Add(registration);

        foreach (var typeName in _typeResolver.GetMessageTypes<TMessage>())
        {
            _messageTypes.Add(typeName);
        }
    }

    /// <inheritdoc />
    public void UseRetry(IRetryPolicy policy)
    {
        // Stored for pipeline configuration
    }

    /// <inheritdoc />
    public void UseConcurrencyLimit(int limit)
    {
        ConcurrencyLimit = limit;
    }

    /// <inheritdoc />
    public void UsePrefetch(int count)
    {
        PrefetchCount = count;
    }

    /// <inheritdoc />
    public void UseCircuitBreaker(int failureThreshold, TimeSpan resetInterval)
    {
        // Stored for pipeline configuration
    }

    /// <inheritdoc />
    public void UseTimeout(TimeSpan timeout)
    {
        // Stored for pipeline configuration
    }

    /// <inheritdoc />
    public void UseFilter<TFilter>()
        where TFilter : class
    {
        // Stored for pipeline configuration
    }

    /// <inheritdoc />
    public void UseFilter<TContext>(IFilter<TContext> filter)
        where TContext : PipeContext
    {
        // Stored for pipeline configuration
    }

    /// <summary>Gets the registered message types.</summary>
    public IEnumerable<string> GetMessageTypes() => _messageTypes;

    /// <summary>Gets the consumer registrations.</summary>
    public IEnumerable<ConsumerRegistration> GetConsumerRegistrations() => _consumers;
}
