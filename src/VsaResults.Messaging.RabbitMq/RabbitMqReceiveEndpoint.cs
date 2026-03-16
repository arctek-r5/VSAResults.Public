using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using VsaResults.Features.Features;
using VsaResults.Messaging.Consumers;
using VsaResults.Messaging.ErrorOr;
using VsaResults.Messaging.Messages;
using VsaResults.Messaging.Pipeline;
using VsaResults.Messaging.Retry;
using VsaResults.Messaging.Serialization;
using VsaResults.Messaging.Transports;
using VsaResults.Messaging.Transports.InMemory;
using VsaResults.Messaging.WideEvents;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.RabbitMq;

/// <summary>
/// RabbitMQ receive endpoint implementation.
/// </summary>
public class RabbitMqReceiveEndpoint : IReceiveEndpoint
{
    private readonly RabbitMqTransport _transport;
    private readonly RabbitMqTransportOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMqReceiveEndpointConfigurator _configurator;
    private readonly ILogger? _logger;

    private IChannel? _channel;
    private string? _consumerTag;
    private CancellationTokenSource? _processingCts;
    private bool _isRunning;

    internal RabbitMqReceiveEndpoint(
        EndpointAddress address,
        RabbitMqTransport transport,
        RabbitMqTransportOptions options,
        IServiceProvider serviceProvider,
        RabbitMqReceiveEndpointConfigurator configurator,
        ILogger? logger)
    {
        Address = address;
        _transport = transport;
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
            // Ensure transport is connected
            var connectResult = await _transport.EnsureConnectedAsync(ct);
            if (connectResult.IsError)
            {
                return connectResult.Errors.ToResult<Unit>();
            }

            // Create a channel for this endpoint
            var channelResult = await _transport.CreateChannelAsync(ct);
            if (channelResult.IsError)
            {
                return channelResult.Errors.ToResult<Unit>();
            }

            _channel = channelResult.Value;

            // Dead letter infrastructure: declare DLX + error queue so failed
            // messages land in {queue}_error instead of requeuing forever.
            // If the main queue already exists without DLQ args (pre-existing),
            // fall back to declaring without them — the queue will work but
            // without automatic dead-lettering until it is recreated.
            Dictionary<string, object?>? queueArgs = null;
            if (_options.DeadLetterOnFailure)
            {
                var dlxName = $"dlx.{Name}";
                var errorQueueName = $"{Name}_error";

                await _channel.ExchangeDeclareAsync(
                    exchange: dlxName,
                    type: ExchangeType.Fanout,
                    durable: true,
                    autoDelete: false,
                    arguments: null,
                    cancellationToken: ct);

                await _channel.QueueDeclareAsync(
                    queue: errorQueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null,
                    cancellationToken: ct);

                await _channel.QueueBindAsync(
                    queue: errorQueueName,
                    exchange: dlxName,
                    routingKey: string.Empty,
                    arguments: null,
                    cancellationToken: ct);

                queueArgs = new Dictionary<string, object?>
                {
                    ["x-dead-letter-exchange"] = dlxName,
                };
            }

            // Declare the main queue (with DLX args if dead-lettering is enabled).
            // If this fails with PRECONDITION_FAILED, the queue already exists with
            // different arguments — delete the RabbitMQ data volume and restart:
            //   docker volume rm arctek-rabbitmq-data
            await _channel.QueueDeclareAsync(
                queue: Name,
                durable: _options.Durable,
                exclusive: false,
                autoDelete: false,
                arguments: queueArgs,
                cancellationToken: ct);

            // Bind to exchanges for each message type
            foreach (var messageTypeName in _configurator.GetMessageTypes())
            {
                // Use same naming convention as RabbitMqPublishTransport
                var exchangeName = messageTypeName.Replace(':', '_').Replace('/', '_');

                // Declare the exchange (fanout for pub/sub)
                await _channel.ExchangeDeclareAsync(
                    exchange: exchangeName,
                    type: ExchangeType.Fanout,
                    durable: _options.Durable,
                    autoDelete: false,
                    arguments: null,
                    cancellationToken: ct);

                // Bind queue to exchange
                await _channel.QueueBindAsync(
                    queue: Name,
                    exchange: exchangeName,
                    routingKey: string.Empty,
                    arguments: null,
                    cancellationToken: ct);

                _logger?.LogDebug(
                    "Bound queue {Queue} to exchange {Exchange}",
                    Name,
                    exchangeName);
            }

            // Set prefetch count
            var prefetch = _configurator.PrefetchCount ?? _options.PrefetchCount;
            await _channel.BasicQosAsync(
                prefetchSize: 0,
                prefetchCount: (ushort)prefetch,
                global: false,
                cancellationToken: ct);

            // Create consumer
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += OnMessageReceivedAsync;
            _processingCts = new CancellationTokenSource();

            // Start consuming
            _consumerTag = await _channel.BasicConsumeAsync(
                queue: Name,
                autoAck: false,
                consumer: consumer,
                cancellationToken: ct);

            _isRunning = true;

            _logger?.LogInformation(
                "RabbitMQ endpoint '{EndpointName}' started with {ConsumerCount} consumer registrations",
                Name,
                _configurator.Consumers.Count);

            return Unit.Value;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start RabbitMQ endpoint '{EndpointName}'", Name);
            return MessagingErrors.TransportError($"Failed to start endpoint: {ex.Message}");
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
            _processingCts?.Cancel();

            if (_channel is not null && _consumerTag is not null)
            {
                await _channel.BasicCancelAsync(_consumerTag, cancellationToken: ct);
            }

            _isRunning = false;
            _logger?.LogInformation("RabbitMQ endpoint '{EndpointName}' stopped", Name);

            return Unit.Value;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error stopping RabbitMQ endpoint '{EndpointName}'", Name);
            return MessagingErrors.TransportError($"Failed to stop endpoint: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        await StopAsync();

        if (_channel is not null)
        {
            await _channel.CloseAsync();
            _channel.Dispose();
            _channel = null;
        }

        _processingCts?.Dispose();
        _processingCts = null;
    }

    private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        // Extract trace context from message headers for distributed tracing
        var parentContext = ExtractTraceContext(ea.BasicProperties.Headers);

        // Start activity for consumer processing, linked to producer trace
        using var activity = RabbitMqDiagnostics.Source.StartActivity(
            $"{Name} receive",
            ActivityKind.Consumer,
            parentContext);

        activity?.SetTag(RabbitMqDiagnostics.Tags.MessagingSystem, RabbitMqDiagnostics.Values.MessagingSystemRabbitmq);
        activity?.SetTag(RabbitMqDiagnostics.Tags.MessagingDestinationName, Name);
        activity?.SetTag(RabbitMqDiagnostics.Tags.MessagingDestinationKind, RabbitMqDiagnostics.Values.DestinationKindQueue);
        activity?.SetTag(RabbitMqDiagnostics.Tags.MessagingOperation, "receive");
        activity?.SetTag(RabbitMqDiagnostics.Tags.MessagingMessagePayloadSize, ea.Body.Length);

        try
        {
            // Parse the message envelope from RabbitMQ properties
            var envelope = ParseEnvelope(ea);

            activity?.SetTag(RabbitMqDiagnostics.Tags.MessagingMessageId, envelope.MessageId.ToString());
            activity?.SetTag(RabbitMqDiagnostics.Tags.MessagingConversationId, envelope.CorrelationId.ToString());

            // Record queue dwell time: how long the message sat in RabbitMQ before being consumed
            if (envelope.SentTime != default)
            {
                var dwellTime = DateTimeOffset.UtcNow - envelope.SentTime;
                activity?.SetTag("messaging.rabbitmq.queue_dwell_ms", dwellTime.TotalMilliseconds);
            }

            _logger?.LogDebug(
                "Received message {MessageId} on endpoint {Endpoint}",
                envelope.MessageId,
                Name);

            // Process the message
            await ProcessEnvelopeAsync(envelope, _processingCts?.Token ?? CancellationToken.None);

            activity?.SetStatus(ActivityStatusCode.Ok);

            // Acknowledge the message
            if (_channel is not null)
            {
                await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", ex.GetType().FullName);

            // Emit wide event for the unhandled exception so it appears in the
            // canonical observability stream, not just as a log line.
            try
            {
                using var wideScope = _serviceProvider.CreateScope();
                var emitter = wideScope.ServiceProvider.GetService<IMessageWideEventEmitter>();
                if (emitter is not null)
                {
                    var envelope = ParseEnvelope(ea);
                    var primaryType = envelope.MessageTypes.Count > 0 ? envelope.MessageTypes[0] : "unknown";
                    var builder = new MessageWideEventBuilder(
                        envelope.MessageId.ToString(),
                        envelope.CorrelationId.ToString(),
                        primaryType,
                        Name,
                        "consumer");
                    emitter.Emit(builder.Exception(ex));
                }
            }
            catch
            {
                // Wide event emission must not mask the original exception handling.
            }

            _logger?.LogError(ex, "Error processing message on endpoint {Endpoint}. ExType={ExType}, Message={ExMessage}",
                Name, ex.GetType().FullName, ex.Message);

            if (_channel is not null)
            {
                // When dead-lettering is enabled, NACK without requeue so RabbitMQ
                // routes the message to the dead letter exchange → {queue}_error.
                // When disabled, requeue for legacy infinite-retry behavior.
                var requeue = !_options.DeadLetterOnFailure;
                await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: requeue);
            }
        }
    }

    /// <summary>
    /// Extracts W3C trace context from message headers for distributed tracing.
    /// Supports both W3C traceparent header and VsaResults custom x-x-trace-id/x-x-span-id headers.
    /// </summary>
    private static ActivityContext ExtractTraceContext(IDictionary<string, object?>? headers)
    {
        if (headers is null)
        {
            return default;
        }

        // First, try W3C traceparent header
        if (headers.TryGetValue("traceparent", out var traceparentObj) && traceparentObj is not null)
        {
            var traceparent = GetHeaderString(traceparentObj);

            if (!string.IsNullOrEmpty(traceparent))
            {
                // Extract tracestate if present
                string? tracestate = null;
                if (headers.TryGetValue("tracestate", out var tracestateObj) && tracestateObj is not null)
                {
                    tracestate = GetHeaderString(tracestateObj);
                }

                if (ActivityContext.TryParse(traceparent, tracestate, out var parsedContext))
                {
                    // Create new context with isRemote=true to indicate this came from another process
                    return new ActivityContext(
                        parsedContext.TraceId,
                        parsedContext.SpanId,
                        parsedContext.TraceFlags,
                        parsedContext.TraceState,
                        isRemote: true);
                }
            }
        }

        // Fallback: Try VsaResults custom headers (x-x-trace-id, x-x-span-id)
        // These are double-prefixed because MessageHeaders uses x- prefix keys and
        // RabbitMqPublishTransport adds another x- prefix for custom headers
        if (headers.TryGetValue("x-x-trace-id", out var traceIdObj) &&
            headers.TryGetValue("x-x-span-id", out var spanIdObj) &&
            traceIdObj is not null && spanIdObj is not null)
        {
            var traceIdStr = GetHeaderString(traceIdObj);
            var spanIdStr = GetHeaderString(spanIdObj);

            // Validate format: trace ID should be 32 hex chars, span ID should be 16 hex chars
            if (!string.IsNullOrEmpty(traceIdStr) && !string.IsNullOrEmpty(spanIdStr) &&
                traceIdStr.Length == 32 && spanIdStr.Length == 16)
            {
                // Construct W3C traceparent format: {version}-{trace-id}-{span-id}-{flags}
                // version=00, flags=01 (sampled)
                var syntheticTraceparent = $"00-{traceIdStr}-{spanIdStr}-01";

                if (ActivityContext.TryParse(syntheticTraceparent, traceState: null, out var parsedContext))
                {
                    return new ActivityContext(
                        parsedContext.TraceId,
                        parsedContext.SpanId,
                        parsedContext.TraceFlags,
                        parsedContext.TraceState,
                        isRemote: true);
                }
            }
        }

        return default;
    }

    private static MessageEnvelope ParseEnvelope(BasicDeliverEventArgs ea)
    {
        var props = ea.BasicProperties;
        var headers = props.Headers ?? new Dictionary<string, object?>();

        // Parse message types from the Type property
        var messageTypes = (props.Type ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        // Parse message ID
        var messageId = MessageId.New();
        if (!string.IsNullOrEmpty(props.MessageId))
        {
            var parseResult = MessageId.Parse(props.MessageId);
            if (!parseResult.IsError)
            {
                messageId = parseResult.Value;
            }
        }

        // Parse correlation ID
        var correlationId = CorrelationId.New();
        if (!string.IsNullOrEmpty(props.CorrelationId))
        {
            var parseResult = CorrelationId.Parse(props.CorrelationId);
            if (!parseResult.IsError)
            {
                correlationId = parseResult.Value;
            }
        }

        // Parse initiator ID from headers
        MessageId? initiatorId = null;
        if (headers.TryGetValue("vsa-initiator-id", out var initiatorValue) && initiatorValue is not null)
        {
            var initiatorStr = GetHeaderString(initiatorValue);
            if (!string.IsNullOrEmpty(initiatorStr))
            {
                var parseResult = MessageId.Parse(initiatorStr);
                if (!parseResult.IsError)
                {
                    initiatorId = parseResult.Value;
                }
            }
        }

        // Build custom headers from x- prefixed headers
        var customHeaders = new MessageHeaders();
        foreach (var (key, value) in headers)
        {
            if (key.StartsWith("x-", StringComparison.Ordinal) && value is not null)
            {
                var headerKey = key[2..]; // Remove "x-" prefix
                customHeaders[headerKey] = GetHeaderString(value);
            }
        }

        return new MessageEnvelope
        {
            MessageId = messageId,
            CorrelationId = correlationId,
            InitiatorId = initiatorId,
            MessageTypes = messageTypes,
            Body = ea.Body.ToArray(),
            ContentType = props.ContentType ?? "application/json",
            Headers = customHeaders,
            SentTime = props.Timestamp.UnixTime > 0
                ? DateTimeOffset.FromUnixTimeSeconds(props.Timestamp.UnixTime)
                : DateTimeOffset.UtcNow
        };
    }

    private static string GetHeaderString(object value)
    {
        return value switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            string str => str,
            _ => value.ToString() ?? string.Empty
        };
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
/// RabbitMQ receive endpoint configurator.
/// </summary>
internal sealed class RabbitMqReceiveEndpointConfigurator : IReceiveEndpointConfigurator
{
    private readonly EndpointAddress _address;
    private readonly List<ConsumerRegistration> _consumers = [];
    private readonly List<string> _messageTypes = [];
    private readonly MessageTypeResolver _typeResolver = new();

    public RabbitMqReceiveEndpointConfigurator(EndpointAddress address)
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
