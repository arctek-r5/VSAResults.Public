using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using VsaResults.Features.Features;
using VsaResults.Messaging.ErrorOr;
using VsaResults.Messaging.Messages;
using VsaResults.Messaging.Transports;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.RabbitMq;

/// <summary>
/// RabbitMQ send transport for point-to-point messaging.
/// The send transport does NOT declare queues — the receive endpoint (consumer) is the
/// authoritative owner of queue topology. This avoids 406 PRECONDITION_FAILED errors
/// when the consumer declares queues with additional arguments (e.g. x-dead-letter-exchange)
/// that the sender doesn't know about.
/// </summary>
public sealed class RabbitMqSendTransport : ISendTransport, IDisposable
{
    private readonly IChannel _channel;
    private readonly RabbitMqTransportOptions _options;
    private readonly ILogger? _logger;
    private readonly SemaphoreSlim _declareLock = new(1, 1);

    internal RabbitMqSendTransport(
        EndpointAddress address,
        IChannel channel,
        RabbitMqTransportOptions options,
        ILogger? logger)
    {
        Address = address;
        _channel = channel;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public EndpointAddress Address { get; }

    /// <inheritdoc />
    public async Task<VsaResult<Unit>> SendAsync<TMessage>(
        MessageEnvelope envelope,
        CancellationToken ct = default)
        where TMessage : class, IMessage
    {
        var queueName = Address.Name;

        // Start activity for tracing
        using var activity = RabbitMqDiagnostics.Source.StartActivity(
            $"{queueName} send",
            ActivityKind.Producer);

        activity?.SetTag(RabbitMqDiagnostics.Tags.MessagingSystem, RabbitMqDiagnostics.Values.MessagingSystemRabbitmq);
        activity?.SetTag(RabbitMqDiagnostics.Tags.MessagingDestinationName, queueName);
        activity?.SetTag(RabbitMqDiagnostics.Tags.MessagingDestinationKind, RabbitMqDiagnostics.Values.DestinationKindQueue);
        activity?.SetTag(RabbitMqDiagnostics.Tags.MessagingOperation, RabbitMqDiagnostics.Values.OperationSend);
        activity?.SetTag(RabbitMqDiagnostics.Tags.MessagingMessageId, envelope.MessageId.ToString());
        activity?.SetTag(RabbitMqDiagnostics.Tags.MessagingConversationId, envelope.CorrelationId.ToString());
        activity?.SetTag(RabbitMqDiagnostics.Tags.MessagingRabbitmqRoutingKey, queueName);
        activity?.SetTag(RabbitMqDiagnostics.Tags.MessagingMessagePayloadSize, envelope.Body.Length);

        try
        {
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

            // Send directly to the queue using default exchange.
            // With RabbitMQ's default exchange, routing key = queue name.
            // mandatory=false: if the queue doesn't exist, the message is silently dropped
            // rather than causing a channel error.
            await _channel.BasicPublishAsync(
                exchange: string.Empty, // Default exchange
                routingKey: queueName,
                mandatory: false,
                basicProperties: properties,
                body: envelope.Body,
                cancellationToken: ct);

            activity?.SetStatus(ActivityStatusCode.Ok);

            _logger?.LogDebug(
                "Sent {MessageType} to queue {Queue}",
                typeof(TMessage).Name,
                queueName);

            return Unit.Value;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", ex.GetType().FullName);
            _logger?.LogError(ex, "Failed to send {MessageType} to {Address}", typeof(TMessage).Name, Address);
            return MessagingErrors.DeliveryFailed(Address, ex.Message);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _declareLock.Dispose();
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
}
