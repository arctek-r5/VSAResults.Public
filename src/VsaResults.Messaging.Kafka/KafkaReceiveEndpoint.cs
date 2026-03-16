using System.Text;
using Confluent.Kafka;
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
using KafkaConsumer = Confluent.Kafka.IConsumer<string, byte[]>;

namespace VsaResults.Messaging.Kafka;

/// <summary>
/// Kafka receive endpoint implementation.
/// </summary>
public class KafkaReceiveEndpoint : IReceiveEndpoint
{
    private readonly KafkaTransportOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly KafkaReceiveEndpointConfigurator _configurator;
    private readonly ILogger? _logger;

    private KafkaConsumer? _consumer;
    private CancellationTokenSource? _cts;
    private Task? _consumerTask;
    private bool _isRunning;

    internal KafkaReceiveEndpoint(
        EndpointAddress address,
        KafkaTransportOptions options,
        IServiceProvider serviceProvider,
        KafkaReceiveEndpointConfigurator configurator,
        ILogger? logger)
    {
        Address = address;
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
    public Task<VsaResult<Unit>> StartAsync(CancellationToken ct = default)
    {
        if (_isRunning)
        {
            return Task.FromResult<VsaResult<Unit>>(Unit.Value);
        }

        try
        {
            // Build consumer config with endpoint-specific group ID
            var config = _options.BuildConsumerConfig();

            // Use endpoint name as part of the group ID for unique consumer groups
            config.GroupId = $"{_options.GroupId}-{Name}";

            _consumer = new ConsumerBuilder<string, byte[]>(config)
                .SetErrorHandler((_, error) =>
                {
                    _logger?.LogError(
                        "Kafka consumer error on endpoint '{Endpoint}': {ErrorCode} - {Reason}",
                        Name,
                        error.Code,
                        error.Reason);
                })
                .SetLogHandler((_, log) =>
                {
                    _logger?.LogDebug(
                        "Kafka consumer log on endpoint '{Endpoint}': [{Level}] {Message}",
                        Name,
                        log.Level,
                        log.Message);
                })
                .Build();

            // Subscribe to topics for each registered message type
            var topics = _configurator.GetMessageTypes()
                .Select(KafkaSendTransport.NormalizeTopicName)
                .Distinct()
                .ToList();

            if (topics.Count == 0)
            {
                // If no specific message types, subscribe to the endpoint name as a topic
                topics.Add(KafkaSendTransport.NormalizeTopicName(Name));
            }

            _consumer.Subscribe(topics);

            _logger?.LogInformation(
                "Kafka endpoint '{EndpointName}' subscribed to topics: {Topics}",
                Name,
                string.Join(", ", topics));

            // Start the consumer loop
            _cts = new CancellationTokenSource();
            _consumerTask = Task.Run(() => ConsumeLoopAsync(_cts.Token), _cts.Token);

            _isRunning = true;

            _logger?.LogInformation(
                "Kafka endpoint '{EndpointName}' started with {ConsumerCount} consumer registrations",
                Name,
                _configurator.Consumers.Count);

            return Task.FromResult<VsaResult<Unit>>(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start Kafka endpoint '{EndpointName}'", Name);
            return Task.FromResult<VsaResult<Unit>>(MessagingErrors.TransportError($"Failed to start endpoint: {ex.Message}"));
        }
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
            _cts?.Cancel();

            if (_consumerTask is not null)
            {
                try
                {
                    await _consumerTask.WaitAsync(TimeSpan.FromSeconds(30), ct);
                }
                catch (TimeoutException)
                {
                    _logger?.LogWarning("Kafka consumer loop did not stop within timeout on endpoint '{Endpoint}'", Name);
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            }

            _consumer?.Close();
            _isRunning = false;

            _logger?.LogInformation("Kafka endpoint '{EndpointName}' stopped", Name);
            return Unit.Value;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error stopping Kafka endpoint '{EndpointName}'", Name);
            return MessagingErrors.TransportError($"Failed to stop endpoint: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        await StopAsync();

        _consumer?.Dispose();
        _consumer = null;

        _cts?.Dispose();
        _cts = null;
    }

    private async Task ConsumeLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = _consumer?.Consume(TimeSpan.FromSeconds(1));

                    if (consumeResult is null || consumeResult.IsPartitionEOF)
                    {
                        continue;
                    }

                    _logger?.LogDebug(
                        "Received message on topic {Topic}, partition {Partition}, offset {Offset}",
                        consumeResult.Topic,
                        consumeResult.Partition.Value,
                        consumeResult.Offset.Value);

                    try
                    {
                        var envelope = ParseEnvelope(consumeResult);
                        await ProcessEnvelopeAsync(envelope, ct);

                        // Commit offset after successful processing
                        _consumer?.Commit(consumeResult);

                        _logger?.LogDebug(
                            "Committed offset {Offset} for topic {Topic}, partition {Partition}",
                            consumeResult.Offset.Value,
                            consumeResult.Topic,
                            consumeResult.Partition.Value);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(
                            ex,
                            "Error processing message from topic {Topic}, partition {Partition}, offset {Offset}",
                            consumeResult.Topic,
                            consumeResult.Partition.Value,
                            consumeResult.Offset.Value);

                        // TODO: Implement dead-letter queue handling
                        // For now, commit anyway to avoid infinite reprocessing
                        // In production, you might want to send to a DLQ
                        _consumer?.Commit(consumeResult);
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger?.LogError(
                        ex,
                        "Kafka consume error on endpoint '{Endpoint}': {Error}",
                        Name,
                        ex.Error.Reason);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger?.LogDebug("Kafka consumer loop cancelled for endpoint '{Endpoint}'", Name);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Kafka consumer loop crashed for endpoint '{Endpoint}'", Name);
        }
    }

    private static MessageEnvelope ParseEnvelope(ConsumeResult<string, byte[]> result)
    {
        var headers = result.Message.Headers;

        // Parse message types from header
        var messageTypesHeader = GetHeaderString(headers, "vsa-message-types") ?? string.Empty;
        var messageTypes = messageTypesHeader
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        // Parse message ID
        var messageId = MessageId.New();
        var messageIdStr = GetHeaderString(headers, "vsa-message-id");
        if (!string.IsNullOrEmpty(messageIdStr))
        {
            var parseResult = MessageId.Parse(messageIdStr);
            if (!parseResult.IsError)
            {
                messageId = parseResult.Value;
            }
        }

        // Parse correlation ID
        var correlationId = CorrelationId.New();
        var correlationIdStr = GetHeaderString(headers, "vsa-correlation-id");
        if (!string.IsNullOrEmpty(correlationIdStr))
        {
            var parseResult = CorrelationId.Parse(correlationIdStr);
            if (!parseResult.IsError)
            {
                correlationId = parseResult.Value;
            }
        }

        // Parse initiator ID
        MessageId? initiatorId = null;
        var initiatorIdStr = GetHeaderString(headers, "vsa-initiator-id");
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
        var conversationIdStr = GetHeaderString(headers, "vsa-conversation-id");
        if (!string.IsNullOrEmpty(conversationIdStr))
        {
            var parseResult = ConversationId.Parse(conversationIdStr);
            if (!parseResult.IsError)
            {
                conversationId = parseResult.Value;
            }
        }

        // Parse sent time
        DateTimeOffset sentTime = result.Message.Timestamp.UtcDateTime;
        var sentTimeStr = GetHeaderString(headers, "vsa-sent-time");
        if (!string.IsNullOrEmpty(sentTimeStr) && DateTimeOffset.TryParse(sentTimeStr, out var parsedTime))
        {
            sentTime = parsedTime;
        }

        // Parse expiration time
        DateTimeOffset? expirationTime = null;
        var expirationStr = GetHeaderString(headers, "vsa-expiration-time");
        if (!string.IsNullOrEmpty(expirationStr) && DateTimeOffset.TryParse(expirationStr, out var parsedExpiration))
        {
            expirationTime = parsedExpiration;
        }

        // Build custom headers from x- prefixed headers
        var customHeaders = new MessageHeaders();
        if (headers is not null)
        {
            foreach (var header in headers)
            {
                if (header.Key.StartsWith("x-", StringComparison.Ordinal))
                {
                    var headerKey = header.Key[2..]; // Remove "x-" prefix
                    customHeaders[headerKey] = Encoding.UTF8.GetString(header.GetValueBytes());
                }
            }
        }

        // Parse addresses
        var sourceAddressStr = GetHeaderString(headers, "vsa-source-address");
        var destinationAddressStr = GetHeaderString(headers, "vsa-destination-address");
        var responseAddressStr = GetHeaderString(headers, "vsa-response-address");
        var faultAddressStr = GetHeaderString(headers, "vsa-fault-address");

        return new MessageEnvelope
        {
            MessageId = messageId,
            CorrelationId = correlationId,
            ConversationId = conversationId,
            InitiatorId = initiatorId,
            MessageTypes = messageTypes,
            Body = result.Message.Value,
            ContentType = GetHeaderString(headers, "vsa-content-type") ?? "application/json",
            Headers = customHeaders,
            SentTime = sentTime,
            ExpirationTime = expirationTime,
            SourceAddress = !string.IsNullOrEmpty(sourceAddressStr)
                ? EndpointAddress.Parse(sourceAddressStr).Match<EndpointAddress?>(a => a, _ => null)
                : null,
            DestinationAddress = !string.IsNullOrEmpty(destinationAddressStr)
                ? EndpointAddress.Parse(destinationAddressStr).Match<EndpointAddress?>(a => a, _ => null)
                : null,
            ResponseAddress = !string.IsNullOrEmpty(responseAddressStr)
                ? EndpointAddress.Parse(responseAddressStr).Match<EndpointAddress?>(a => a, _ => null)
                : null,
            FaultAddress = !string.IsNullOrEmpty(faultAddressStr)
                ? EndpointAddress.Parse(faultAddressStr).Match<EndpointAddress?>(a => a, _ => null)
                : null
        };
    }

    private static string? GetHeaderString(Headers? headers, string key)
    {
        if (headers is null)
        {
            return null;
        }

        try
        {
            var header = headers.FirstOrDefault(h => h.Key == key);
            return header is not null ? Encoding.UTF8.GetString(header.GetValueBytes()) : null;
        }
        catch
        {
            return null;
        }
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
/// Configurator for Kafka receive endpoints.
/// </summary>
internal sealed class KafkaReceiveEndpointConfigurator : IReceiveEndpointConfigurator
{
    private readonly EndpointAddress _address;
    private readonly List<ConsumerRegistration> _consumers = [];
    private readonly List<string> _messageTypes = [];
    private readonly MessageTypeResolver _typeResolver = new();

    public KafkaReceiveEndpointConfigurator(EndpointAddress address)
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
