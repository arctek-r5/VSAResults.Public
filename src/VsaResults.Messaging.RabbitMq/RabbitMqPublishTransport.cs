using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using VsaResults.Features.Features;
using VsaResults.Messaging.ErrorOr;
using VsaResults.Messaging.Messages;
using VsaResults.Messaging.Serialization;
using VsaResults.Messaging.Transports;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.RabbitMq;

/// <summary>
/// RabbitMQ publish transport for pub/sub messaging.
/// </summary>
public sealed class RabbitMqPublishTransport : IPublishTransport, IDisposable
{
    private readonly IChannel _channel;
    private readonly RabbitMqTransportOptions _options;
    private readonly ILogger? _logger;
    private readonly HashSet<string> _declaredExchanges = [];
    private readonly SemaphoreSlim _declareLock = new(1, 1);

    internal RabbitMqPublishTransport(
        IChannel channel,
        RabbitMqTransportOptions options,
        ILogger? logger)
    {
        _channel = channel;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<VsaResult<Unit>> PublishAsync<TMessage>(
        MessageEnvelope envelope,
        CancellationToken ct = default)
        where TMessage : class, IEvent
    {
        var exchangeName = GetExchangeName<TMessage>();

        // Start activity for tracing
        using var activity = RabbitMqDiagnostics.Source.StartActivity(
            $"{exchangeName} publish",
            ActivityKind.Producer);

        activity?.SetTag(RabbitMqDiagnostics.Tags.MessagingSystem, RabbitMqDiagnostics.Values.MessagingSystemRabbitmq);
        activity?.SetTag(RabbitMqDiagnostics.Tags.MessagingDestinationName, exchangeName);
        activity?.SetTag(RabbitMqDiagnostics.Tags.MessagingDestinationKind, RabbitMqDiagnostics.Values.DestinationKindExchange);
        activity?.SetTag(RabbitMqDiagnostics.Tags.MessagingOperation, RabbitMqDiagnostics.Values.OperationPublish);
        activity?.SetTag(RabbitMqDiagnostics.Tags.MessagingMessageId, envelope.MessageId.ToString());
        activity?.SetTag(RabbitMqDiagnostics.Tags.MessagingConversationId, envelope.CorrelationId.ToString());
        activity?.SetTag(RabbitMqDiagnostics.Tags.MessagingMessagePayloadSize, envelope.Body.Length);

        try
        {
            // Declare the exchange if we haven't already
            await EnsureExchangeDeclaredAsync(exchangeName, ct);

            // Create message properties with trace context propagation
            var headers = ConvertHeaders(envelope);

            // Propagate trace context via W3C traceparent header
            if (activity is not null)
            {
                headers["traceparent"] = activity.Id;
                if (activity.TraceStateString is not null)
                {
                    headers["tracestate"] = activity.TraceStateString;
                }
            }

            var properties = new BasicProperties
            {
                MessageId = envelope.MessageId.ToString(),
                CorrelationId = envelope.CorrelationId.ToString(),
                ContentType = envelope.ContentType,
                DeliveryMode = _options.PersistentMessages ? DeliveryModes.Persistent : DeliveryModes.Transient,
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                Type = string.Join(";", envelope.MessageTypes),
                Headers = headers
            };

            // Publish to the exchange (fanout, so routing key is empty)
            await _channel.BasicPublishAsync(
                exchange: exchangeName,
                routingKey: string.Empty,
                mandatory: false,
                basicProperties: properties,
                body: envelope.Body,
                cancellationToken: ct);

            activity?.SetStatus(ActivityStatusCode.Ok);

            _logger?.LogDebug(
                "Published {MessageType} to exchange {Exchange}",
                typeof(TMessage).Name,
                exchangeName);

            return Unit.Value;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", ex.GetType().FullName);
            _logger?.LogError(ex, "Failed to publish {MessageType}", typeof(TMessage).Name);
            return MessagingErrors.TransportError($"Failed to publish message: {ex.Message}");
        }
    }

    /// <summary>
    /// Ensures the exchange is declared.
    /// </summary>
    private async Task EnsureExchangeDeclaredAsync(string exchangeName, CancellationToken ct)
    {
        if (_declaredExchanges.Contains(exchangeName))
        {
            return;
        }

        await _declareLock.WaitAsync(ct);
        try
        {
            if (_declaredExchanges.Contains(exchangeName))
            {
                return;
            }

            await _channel.ExchangeDeclareAsync(
                exchange: exchangeName,
                type: ExchangeType.Fanout,
                durable: _options.Durable,
                autoDelete: false,
                arguments: null,
                cancellationToken: ct);

            _declaredExchanges.Add(exchangeName);

            _logger?.LogDebug("Declared exchange {Exchange}", exchangeName);
        }
        finally
        {
            _declareLock.Release();
        }
    }

    /// <summary>
    /// Converts envelope headers to RabbitMQ-compatible dictionary.
    /// </summary>
    private static Dictionary<string, object?> ConvertHeaders(MessageEnvelope envelope)
    {
        var headers = new Dictionary<string, object?>
        {
            ["vsa-message-id"] = envelope.MessageId.ToString(),
            ["vsa-correlation-id"] = envelope.CorrelationId.ToString(),
            ["vsa-sent-time"] = envelope.SentTime.ToString("O"),
            ["vsa-destination-address"] = envelope.DestinationAddress?.ToString()
        };

        if (envelope.SourceAddress is not null)
        {
            headers["vsa-source-address"] = envelope.SourceAddress.ToString();
        }

        if (envelope.InitiatorId is not null)
        {
            headers["vsa-initiator-id"] = envelope.InitiatorId.ToString();
        }

        if (envelope.ConversationId is not null)
        {
            headers["vsa-conversation-id"] = envelope.ConversationId.ToString();
        }

        // Add custom headers
        foreach (var (key, value) in envelope.Headers)
        {
            headers[$"x-{key}"] = value;
        }

        // Add host info
        if (envelope.Host is not null)
        {
            headers["vsa-host-machine"] = envelope.Host.MachineName;
            headers["vsa-host-process"] = envelope.Host.ProcessName;
            headers["vsa-host-process-id"] = envelope.Host.ProcessId?.ToString(CultureInfo.InvariantCulture);
        }

        // Propagate W3C trace context so consumers can correlate spans
        var activity = Activity.Current;
        if (activity is not null)
        {
            headers["traceparent"] = activity.Id;
            if (!string.IsNullOrEmpty(activity.TraceStateString))
            {
                headers["tracestate"] = activity.TraceStateString;
            }
        }

        return headers;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _declareLock.Dispose();
    }

    /// <summary>
    /// Gets the exchange name for a message type.
    /// Uses the URN format from MessageTypeResolver for consistency with consumer bindings.
    /// </summary>
    private static string GetExchangeName<TMessage>()
        where TMessage : class, IEvent
    {
        // Use the URN format that MessageTypeResolver produces
        // urn:message:Namespace:TypeName becomes exchange name with colons replaced
        var primaryType = MessageTypeResolver.GetPrimaryIdentifier<TMessage>();

        // Replace colons and slashes to make a valid RabbitMQ exchange name
        return primaryType.Replace(':', '_').Replace('/', '_');
    }
}
