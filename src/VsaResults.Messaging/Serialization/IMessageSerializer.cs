using VsaResults.VsaResult;

namespace VsaResults.Messaging.Serialization;

/// <summary>
/// Interface for message body serialization.
/// Implementations convert messages to/from byte arrays for transport.
/// </summary>
public interface IMessageSerializer
{
    /// <summary>Gets the content type produced by this serializer (e.g., "application/json").</summary>
    string ContentType { get; }

    /// <summary>
    /// Serializes a message to bytes.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="message">The message to serialize.</param>
    /// <returns>The serialized bytes or an error.</returns>
    VsaResult<byte[]> Serialize<TMessage>(TMessage message)
        where TMessage : class;

    /// <summary>
    /// Deserializes bytes to a strongly-typed message.
    /// </summary>
    /// <typeparam name="TMessage">The expected message type.</typeparam>
    /// <param name="data">The serialized data.</param>
    /// <returns>The deserialized message or an error.</returns>
    VsaResult<TMessage> Deserialize<TMessage>(byte[] data)
        where TMessage : class;

    /// <summary>
    /// Deserializes bytes to a message of the specified runtime type.
    /// </summary>
    /// <param name="data">The serialized data.</param>
    /// <param name="messageType">The target message type.</param>
    /// <returns>The deserialized message or an error.</returns>
    VsaResult<object> Deserialize(byte[] data, Type messageType);
}
