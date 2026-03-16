using System.Globalization;
using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using VsaResults.Features.Features;
using VsaResults.Messaging.ErrorOr;
using VsaResults.Messaging.Messages;
using VsaResults.Messaging.Transports;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Kafka;

/// <summary>
/// Kafka send transport for point-to-point messaging.
/// </summary>
public class KafkaSendTransport : ISendTransport
{
    private readonly IProducer<string, byte[]> _producer;
    private readonly ILogger? _logger;

    internal KafkaSendTransport(
        EndpointAddress address,
        IProducer<string, byte[]> producer,
        ILogger? logger)
    {
        Address = address;
        _producer = producer;
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
        try
        {
            // For point-to-point, use the endpoint name as the topic
            var topic = NormalizeTopicName(Address.Name);

            // Use correlation ID as the key for consistent partitioning
            var key = envelope.CorrelationId.ToString();

            var message = new Message<string, byte[]>
            {
                Key = key,
                Value = envelope.Body,
                Headers = ConvertHeaders(envelope),
                Timestamp = new Timestamp(envelope.SentTime)
            };

            var result = await _producer.ProduceAsync(topic, message, ct);

            _logger?.LogDebug(
                "Sent {MessageType} to Kafka topic {Topic}, partition {Partition}, offset {Offset}",
                typeof(TMessage).Name,
                result.Topic,
                result.Partition.Value,
                result.Offset.Value);

            return Unit.Value;
        }
        catch (ProduceException<string, byte[]> ex)
        {
            _logger?.LogError(
                ex,
                "Failed to send {MessageType} to Kafka: {Error}",
                typeof(TMessage).Name,
                ex.Error.Reason);
            return MessagingErrors.DeliveryFailed(Address, ex.Error.Reason);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to send {MessageType} to {Address}", typeof(TMessage).Name, Address);
            return MessagingErrors.DeliveryFailed(Address, ex.Message);
        }
    }

    /// <summary>
    /// Normalizes a topic name to be Kafka-compliant.
    /// </summary>
    internal static string NormalizeTopicName(string name)
    {
        // Kafka topic names can contain: letters, numbers, dots, underscores, hyphens
        // Replace any colons, slashes with dots
        return name
            .Replace(':', '.')
            .Replace('/', '.')
            .Replace(' ', '-')
            .ToLowerInvariant();
    }

    /// <summary>
    /// Converts envelope headers to Kafka headers.
    /// </summary>
    internal static Headers ConvertHeaders(MessageEnvelope envelope)
    {
        var headers = new Headers
        {
            { "vsa-message-id", Encoding.UTF8.GetBytes(envelope.MessageId.ToString()) },
            { "vsa-correlation-id", Encoding.UTF8.GetBytes(envelope.CorrelationId.ToString()) },
            { "vsa-sent-time", Encoding.UTF8.GetBytes(envelope.SentTime.ToString("O")) },
            { "vsa-content-type", Encoding.UTF8.GetBytes(envelope.ContentType) },
            { "vsa-message-types", Encoding.UTF8.GetBytes(string.Join(";", envelope.MessageTypes)) }
        };

        if (envelope.SourceAddress is not null)
        {
            headers.Add("vsa-source-address", Encoding.UTF8.GetBytes(envelope.SourceAddress.ToString()));
        }

        if (envelope.DestinationAddress is not null)
        {
            headers.Add("vsa-destination-address", Encoding.UTF8.GetBytes(envelope.DestinationAddress.ToString()));
        }

        if (envelope.InitiatorId is not null)
        {
            headers.Add("vsa-initiator-id", Encoding.UTF8.GetBytes(envelope.InitiatorId.ToString()!));
        }

        if (envelope.ConversationId is not null)
        {
            headers.Add("vsa-conversation-id", Encoding.UTF8.GetBytes(envelope.ConversationId.ToString()!));
        }

        if (envelope.ResponseAddress is not null)
        {
            headers.Add("vsa-response-address", Encoding.UTF8.GetBytes(envelope.ResponseAddress.ToString()));
        }

        if (envelope.FaultAddress is not null)
        {
            headers.Add("vsa-fault-address", Encoding.UTF8.GetBytes(envelope.FaultAddress.ToString()));
        }

        if (envelope.ExpirationTime.HasValue)
        {
            headers.Add("vsa-expiration-time", Encoding.UTF8.GetBytes(envelope.ExpirationTime.Value.ToString("O")));
        }

        // Add custom headers with x- prefix
        foreach (var (key, value) in envelope.Headers)
        {
            if (value is not null)
            {
                var valueStr = value.ToString();
                if (valueStr is not null)
                {
                    headers.Add($"x-{key}", Encoding.UTF8.GetBytes(valueStr));
                }
            }
        }

        // Add host info
        if (envelope.Host is not null)
        {
            headers.Add("vsa-host-machine", Encoding.UTF8.GetBytes(envelope.Host.MachineName));

            if (envelope.Host.ProcessName is not null)
            {
                headers.Add("vsa-host-process", Encoding.UTF8.GetBytes(envelope.Host.ProcessName));
            }

            headers.Add("vsa-host-process-id", Encoding.UTF8.GetBytes(envelope.Host.ProcessId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty));
        }

        return headers;
    }
}
