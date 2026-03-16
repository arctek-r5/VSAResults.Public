using System.Text.Json;
using System.Text.Json.Serialization;
using VsaResults.Messaging.ErrorOr;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Serialization;

/// <summary>
/// System.Text.Json based message serializer.
/// Provides high-performance JSON serialization with sensible defaults.
/// </summary>
public sealed class JsonMessageSerializer : IMessageSerializer
{
    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// Creates a new JSON serializer with default options.
    /// </summary>
    public JsonMessageSerializer()
        : this(CreateDefaultOptions())
    {
    }

    /// <summary>
    /// Creates a new JSON serializer with custom options.
    /// </summary>
    /// <param name="options">The serializer options.</param>
    public JsonMessageSerializer(JsonSerializerOptions options)
    {
        _options = options;
    }

    /// <inheritdoc />
    public string ContentType => "application/json";

    /// <inheritdoc />
    public VsaResult<byte[]> Serialize<TMessage>(TMessage message)
        where TMessage : class
    {
        try
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(message, _options);
            return json;
        }
        catch (JsonException ex)
        {
            return MessagingErrors.SerializationFailed(typeof(TMessage).Name, ex.Message);
        }
        catch (NotSupportedException ex)
        {
            return MessagingErrors.SerializationFailed(typeof(TMessage).Name, ex.Message);
        }
    }

    /// <inheritdoc />
    public VsaResult<TMessage> Deserialize<TMessage>(byte[] data)
        where TMessage : class
    {
        try
        {
            var message = JsonSerializer.Deserialize<TMessage>(data, _options);

            if (message is null)
            {
                return MessagingErrors.DeserializationFailed(typeof(TMessage).Name, "Deserialization returned null.");
            }

            return message;
        }
        catch (JsonException ex)
        {
            return MessagingErrors.DeserializationFailed(typeof(TMessage).Name, ex.Message);
        }
        catch (NotSupportedException ex)
        {
            return MessagingErrors.DeserializationFailed(typeof(TMessage).Name, ex.Message);
        }
    }

    /// <inheritdoc />
    public VsaResult<object> Deserialize(byte[] data, Type messageType)
    {
        try
        {
            var message = JsonSerializer.Deserialize(data, messageType, _options);

            if (message is null)
            {
                return MessagingErrors.DeserializationFailed(messageType.Name, "Deserialization returned null.");
            }

            return message;
        }
        catch (JsonException ex)
        {
            return MessagingErrors.DeserializationFailed(messageType.Name, ex.Message);
        }
        catch (NotSupportedException ex)
        {
            return MessagingErrors.DeserializationFailed(messageType.Name, ex.Message);
        }
    }

    /// <summary>
    /// Creates default serializer options optimized for messaging.
    /// </summary>
    public static JsonSerializerOptions CreateDefaultOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };

        return options;
    }
}
