using VsaResults.Errors;
using VsaResults.Messaging.Messages;
using VsaResults.Messaging.Transports;

namespace VsaResults.Messaging.ErrorOr;

/// <summary>
/// Messaging-specific errors following VsaResults Error patterns.
/// </summary>
public static class MessagingErrors
{
    /// <summary>
    /// Standard error codes for messaging operations.
    /// </summary>
    public static class Codes
    {
        /// <summary>Invalid message ID format.</summary>
        public const string InvalidMessageId = "Messaging.InvalidMessageId";

        /// <summary>Invalid correlation ID format.</summary>
        public const string InvalidCorrelationId = "Messaging.InvalidCorrelationId";

        /// <summary>Invalid conversation ID format.</summary>
        public const string InvalidConversationId = "Messaging.InvalidConversationId";

        /// <summary>Invalid endpoint address format.</summary>
        public const string InvalidEndpointAddress = "Messaging.InvalidEndpointAddress";

        /// <summary>Unknown message type.</summary>
        public const string UnknownMessageType = "Messaging.UnknownMessageType";

        /// <summary>Message serialization failed.</summary>
        public const string SerializationFailed = "Messaging.SerializationFailed";

        /// <summary>Message deserialization failed.</summary>
        public const string DeserializationFailed = "Messaging.DeserializationFailed";

        /// <summary>Transport error.</summary>
        public const string TransportError = "Messaging.TransportError";

        /// <summary>Connection to broker failed.</summary>
        public const string ConnectionFailed = "Messaging.ConnectionFailed";

        /// <summary>Operation timed out.</summary>
        public const string Timeout = "Messaging.Timeout";

        /// <summary>Circuit breaker is open.</summary>
        public const string CircuitBreakerOpen = "Messaging.CircuitBreakerOpen";

        /// <summary>Consumer processing failed.</summary>
        public const string ConsumerFailed = "Messaging.ConsumerFailed";

        /// <summary>Saga instance not found.</summary>
        public const string SagaNotFound = "Messaging.SagaNotFound";

        /// <summary>Saga concurrency conflict.</summary>
        public const string SagaConcurrencyConflict = "Messaging.SagaConcurrencyConflict";

        /// <summary>Message delivery failed.</summary>
        public const string DeliveryFailed = "Messaging.DeliveryFailed";

        /// <summary>Endpoint not found.</summary>
        public const string EndpointNotFound = "Messaging.EndpointNotFound";

        /// <summary>Consumer not registered.</summary>
        public const string ConsumerNotRegistered = "Messaging.ConsumerNotRegistered";

        /// <summary>Bus not started.</summary>
        public const string BusNotStarted = "Messaging.BusNotStarted";

        /// <summary>Retry exhausted.</summary>
        public const string RetryExhausted = "Messaging.RetryExhausted";

        /// <summary>Message expired.</summary>
        public const string MessageExpired = "Messaging.MessageExpired";

        /// <summary>Invalid message type.</summary>
        public const string InvalidMessageType = "Messaging.InvalidMessageType";
    }

    /// <summary>Creates an invalid message ID error.</summary>
    public static Error InvalidMessageId(string value) =>
        Error.Validation(Codes.InvalidMessageId, $"Invalid message ID: '{value}'.");

    /// <summary>Creates an invalid correlation ID error.</summary>
    public static Error InvalidCorrelationId(string value) =>
        Error.Validation(Codes.InvalidCorrelationId, $"Invalid correlation ID: '{value}'.");

    /// <summary>Creates an invalid conversation ID error.</summary>
    public static Error InvalidConversationId(string value) =>
        Error.Validation(Codes.InvalidConversationId, $"Invalid conversation ID: '{value}'.");

    /// <summary>Creates an invalid endpoint address error.</summary>
    public static Error InvalidEndpointAddress(string uri) =>
        Error.Validation(Codes.InvalidEndpointAddress, $"Invalid endpoint address: '{uri}'.");

    /// <summary>Creates an unknown message type error.</summary>
    public static Error UnknownMessageType(string typeIdentifier) =>
        Error.NotFound(Codes.UnknownMessageType, $"Unknown message type: '{typeIdentifier}'.");

    /// <summary>Creates a serialization failure error.</summary>
    public static Error SerializationFailed(string typeName, string reason) =>
        Error.Failure(Codes.SerializationFailed, $"Failed to serialize '{typeName}': {reason}");

    /// <summary>Creates a deserialization failure error.</summary>
    public static Error DeserializationFailed(string typeName, string reason) =>
        Error.Failure(Codes.DeserializationFailed, $"Failed to deserialize '{typeName}': {reason}");

    /// <summary>Creates a transport error.</summary>
    public static Error TransportError(string description) =>
        Error.Failure(Codes.TransportError, description);

    /// <summary>Creates a connection failure error.</summary>
    public static Error ConnectionFailed(string host, string reason) =>
        Error.Unavailable(Codes.ConnectionFailed, $"Failed to connect to '{host}': {reason}");

    /// <summary>Creates a timeout error.</summary>
    public static Error Timeout(TimeSpan timeout) =>
        Error.Timeout(Codes.Timeout, $"Operation timed out after {timeout.TotalSeconds:F1} seconds.");

    /// <summary>Creates a timeout error with custom message.</summary>
    public static Error Timeout(string message) =>
        Error.Timeout(Codes.Timeout, message);

    /// <summary>Creates a circuit breaker open error.</summary>
    public static Error CircuitBreakerOpen() =>
        Error.Unavailable(Codes.CircuitBreakerOpen, "Circuit breaker is open. Service is temporarily unavailable.");

    /// <summary>Creates a circuit breaker open error with details.</summary>
    public static Error CircuitBreakerOpen(int failureCount, TimeSpan reopenTime) =>
        Error.Unavailable(
            Codes.CircuitBreakerOpen,
            $"Circuit breaker is open after {failureCount} failures. Will reopen in {reopenTime.TotalSeconds:F0} seconds.");

    /// <summary>Creates a consumer failure error.</summary>
    public static Error ConsumerFailed(string consumerType, string reason) =>
        Error.Failure(Codes.ConsumerFailed, $"Consumer '{consumerType}' failed: {reason}");

    /// <summary>Creates a consumer failure error with errors.</summary>
    public static Error ConsumerFailed(string consumerType, IReadOnlyList<Error> errors)
    {
        var firstError = errors.Count > 0 ? errors[0] : Error.Unexpected();
        return Error.Failure(
            Codes.ConsumerFailed,
            $"Consumer '{consumerType}' failed: {firstError.Description}",
            new Dictionary<string, object>
            {
                ["ConsumerType"] = consumerType,
                ["ErrorCount"] = errors.Count,
                ["Errors"] = errors.Select(e => new { e.Code, e.Description, Type = e.Type.ToString() }).ToList()
            });
    }

    /// <summary>Creates a saga not found error.</summary>
    public static Error SagaNotFound(CorrelationId correlationId) =>
        Error.NotFound(Codes.SagaNotFound, $"Saga with correlation ID '{correlationId}' not found.");

    /// <summary>Creates a saga concurrency conflict error.</summary>
    public static Error SagaConcurrencyConflict(CorrelationId correlationId) =>
        Error.Conflict(Codes.SagaConcurrencyConflict, $"Saga '{correlationId}' was modified by another process.");

    /// <summary>Creates a delivery failure error.</summary>
    public static Error DeliveryFailed(EndpointAddress address, string reason) =>
        Error.Failure(Codes.DeliveryFailed, $"Failed to deliver message to '{address}': {reason}");

    /// <summary>Creates an endpoint not found error.</summary>
    public static Error EndpointNotFound(EndpointAddress address) =>
        Error.NotFound(Codes.EndpointNotFound, $"Endpoint '{address}' not found.");

    /// <summary>Creates a consumer not registered error.</summary>
    public static Error ConsumerNotRegistered(Type messageType) =>
        Error.NotFound(Codes.ConsumerNotRegistered, $"No consumer registered for message type '{messageType.Name}'.");

    /// <summary>Creates a bus not started error.</summary>
    public static Error BusNotStarted() =>
        Error.Failure(Codes.BusNotStarted, "The message bus has not been started. Call StartAsync() first.");

    /// <summary>Creates a retry exhausted error.</summary>
    public static Error RetryExhausted(int attemptCount, IReadOnlyList<Error> errors) =>
        Error.Failure(
            Codes.RetryExhausted,
            $"All {attemptCount} retry attempts exhausted.",
            new Dictionary<string, object>
            {
                ["AttemptCount"] = attemptCount,
                ["LastErrors"] = errors.Select(e => new { e.Code, e.Description }).ToList()
            });

    /// <summary>Creates a message expired error.</summary>
    public static Error MessageExpired(MessageId messageId, DateTimeOffset expirationTime) =>
        Error.Failure(Codes.MessageExpired, $"Message '{messageId}' expired at {expirationTime:O}.");

    /// <summary>Creates an invalid message type error.</summary>
    public static Error InvalidMessageType(string actualType, string expectedType) =>
        Error.Validation(Codes.InvalidMessageType, $"Invalid message type: expected '{expectedType}', got '{actualType}'.");

    /// <summary>Creates a transport not connected error.</summary>
    public static Error TransportNotConnected() =>
        Error.Failure(Codes.TransportError, "Transport is not connected.");

    /// <summary>Creates a transport connection failed error.</summary>
    public static Error TransportConnectionFailed(string host, int port, string reason) =>
        Error.Unavailable(Codes.ConnectionFailed, $"Failed to connect to '{host}:{port}': {reason}");
}
