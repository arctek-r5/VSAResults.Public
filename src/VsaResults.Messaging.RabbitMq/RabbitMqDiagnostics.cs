using System.Diagnostics;

namespace VsaResults.Messaging.RabbitMq;

/// <summary>
/// Diagnostics support for RabbitMQ messaging operations.
/// Provides ActivitySource for OpenTelemetry instrumentation.
/// </summary>
public static class RabbitMqDiagnostics
{
    /// <summary>
    /// The name of the ActivitySource used for RabbitMQ operations.
    /// Use this when configuring OpenTelemetry to add this source.
    /// </summary>
    public const string ActivitySourceName = "VsaResults.Messaging.RabbitMq";

    /// <summary>
    /// ActivitySource for RabbitMQ messaging operations.
    /// </summary>
    internal static readonly ActivitySource Source = new(ActivitySourceName, "1.0.0");

    /// <summary>
    /// Standard semantic convention tags for messaging.
    /// </summary>
    internal static class Tags
    {
        public const string MessagingSystem = "messaging.system";
        public const string MessagingDestinationName = "messaging.destination.name";
        public const string MessagingDestinationKind = "messaging.destination_kind";
        public const string MessagingMessageId = "messaging.message_id";
        public const string MessagingConversationId = "messaging.conversation_id";
        public const string MessagingMessagePayloadSize = "messaging.message.payload_size_bytes";
        public const string MessagingOperation = "messaging.operation";
        public const string MessagingRabbitmqRoutingKey = "messaging.rabbitmq.routing_key";
    }

    /// <summary>
    /// Standard values for messaging tags.
    /// </summary>
    internal static class Values
    {
        public const string MessagingSystemRabbitmq = "rabbitmq";
        public const string DestinationKindQueue = "queue";
        public const string DestinationKindExchange = "exchange";
        public const string OperationPublish = "publish";
        public const string OperationSend = "send";
    }
}
