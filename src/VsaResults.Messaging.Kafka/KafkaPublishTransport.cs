using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using VsaResults.Features.Features;
using VsaResults.Messaging.ErrorOr;
using VsaResults.Messaging.Messages;
using VsaResults.Messaging.Serialization;
using VsaResults.Messaging.Transports;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Kafka;

/// <summary>
/// Kafka publish transport for pub/sub messaging.
/// </summary>
public class KafkaPublishTransport : IPublishTransport
{
    private readonly IProducer<string, byte[]> _producer;
    private readonly ILogger? _logger;

    internal KafkaPublishTransport(
        IProducer<string, byte[]> producer,
        ILogger? logger)
    {
        _producer = producer;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<VsaResult<Unit>> PublishAsync<TMessage>(
        MessageEnvelope envelope,
        CancellationToken ct = default)
        where TMessage : class, IEvent
    {
        try
        {
            // Use the message type URN as the topic name (normalized for Kafka)
            var topic = GetTopicName<TMessage>();

            // Use correlation ID as the key for consistent partitioning
            var key = envelope.CorrelationId.ToString();

            var message = new Message<string, byte[]>
            {
                Key = key,
                Value = envelope.Body,
                Headers = KafkaSendTransport.ConvertHeaders(envelope),
                Timestamp = new Timestamp(envelope.SentTime)
            };

            var result = await _producer.ProduceAsync(topic, message, ct);

            _logger?.LogDebug(
                "Published {MessageType} to Kafka topic {Topic}, partition {Partition}, offset {Offset}",
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
                "Failed to publish {MessageType} to Kafka: {Error}",
                typeof(TMessage).Name,
                ex.Error.Reason);
            return MessagingErrors.TransportError($"Failed to publish message: {ex.Error.Reason}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to publish {MessageType}", typeof(TMessage).Name);
            return MessagingErrors.TransportError($"Failed to publish message: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the Kafka topic name for a message type.
    /// Uses the URN format from MessageTypeResolver for consistency with consumer subscriptions.
    /// </summary>
    protected virtual string GetTopicName<TMessage>()
        where TMessage : class, IEvent
    {
        // Use the URN format that MessageTypeResolver produces
        // urn:message:Namespace:TypeName becomes topic name with colons/slashes replaced
        var primaryType = MessageTypeResolver.GetPrimaryIdentifier<TMessage>();
        return KafkaSendTransport.NormalizeTopicName(primaryType);
    }
}
