using System.Globalization;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using VsaResults.Features.Features;
using VsaResults.Messaging.ErrorOr;
using VsaResults.Messaging.Messages;
using VsaResults.Messaging.Transports;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.AzureServiceBus;

/// <summary>
/// Azure Service Bus send transport for point-to-point messaging via queues.
/// </summary>
public class AzureServiceBusSendTransport : ISendTransport
{
    private readonly ServiceBusSender _sender;
    private readonly ILogger? _logger;

    internal AzureServiceBusSendTransport(
        EndpointAddress address,
        ServiceBusSender sender,
        ILogger? logger)
    {
        Address = address;
        _sender = sender;
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
            var message = CreateServiceBusMessage(envelope);

            await _sender.SendMessageAsync(message, ct);

            _logger?.LogDebug(
                "Sent {MessageType} to queue {Queue}, MessageId {MessageId}",
                typeof(TMessage).Name,
                Address.Name,
                message.MessageId);

            return Unit.Value;
        }
        catch (ServiceBusException ex)
        {
            _logger?.LogError(
                ex,
                "Failed to send {MessageType} to queue {Queue}: {Reason}",
                typeof(TMessage).Name,
                Address.Name,
                ex.Reason);
            return MessagingErrors.DeliveryFailed(Address, ex.Message);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to send {MessageType} to {Address}", typeof(TMessage).Name, Address);
            return MessagingErrors.DeliveryFailed(Address, ex.Message);
        }
    }

    /// <summary>
    /// Creates a ServiceBusMessage from a MessageEnvelope.
    /// </summary>
    internal static ServiceBusMessage CreateServiceBusMessage(MessageEnvelope envelope)
    {
        var message = new ServiceBusMessage(envelope.Body)
        {
            MessageId = envelope.MessageId.ToString(),
            CorrelationId = envelope.CorrelationId.ToString(),
            ContentType = envelope.ContentType,
            Subject = string.Join(";", envelope.MessageTypes)
        };

        // Set time-to-live if expiration is specified
        if (envelope.ExpirationTime.HasValue)
        {
            var ttl = envelope.ExpirationTime.Value - DateTimeOffset.UtcNow;
            if (ttl > TimeSpan.Zero)
            {
                message.TimeToLive = ttl;
            }
        }

        // Add standard properties
        message.ApplicationProperties["vsa-sent-time"] = envelope.SentTime.ToString("O");
        message.ApplicationProperties["vsa-message-types"] = string.Join(";", envelope.MessageTypes);

        if (envelope.SourceAddress is not null)
        {
            message.ApplicationProperties["vsa-source-address"] = envelope.SourceAddress.ToString();
        }

        if (envelope.DestinationAddress is not null)
        {
            message.ApplicationProperties["vsa-destination-address"] = envelope.DestinationAddress.ToString();
        }

        if (envelope.ResponseAddress is not null)
        {
            message.ReplyTo = envelope.ResponseAddress.ToString();
        }

        if (envelope.FaultAddress is not null)
        {
            message.ApplicationProperties["vsa-fault-address"] = envelope.FaultAddress.ToString();
        }

        if (envelope.InitiatorId is not null)
        {
            message.ApplicationProperties["vsa-initiator-id"] = envelope.InitiatorId.ToString();
        }

        if (envelope.ConversationId is not null)
        {
            message.ApplicationProperties["vsa-conversation-id"] = envelope.ConversationId.ToString();
        }

        // Add custom headers with x- prefix
        foreach (var (key, value) in envelope.Headers)
        {
            if (value is not null)
            {
                message.ApplicationProperties[$"x-{key}"] = value;
            }
        }

        // Add host info
        if (envelope.Host is not null)
        {
            message.ApplicationProperties["vsa-host-machine"] = envelope.Host.MachineName;
            message.ApplicationProperties["vsa-host-process"] = envelope.Host.ProcessName;
            message.ApplicationProperties["vsa-host-process-id"] = envelope.Host.ProcessId?.ToString(CultureInfo.InvariantCulture);
        }

        return message;
    }
}
