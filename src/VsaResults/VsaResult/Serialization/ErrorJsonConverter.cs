using System.Text.Json;
using System.Text.Json.Serialization;
using VsaResults.Errors;

namespace VsaResults.VsaResult.Serialization;

/// <summary>
/// JSON converter for the <see cref="Error"/> type.
/// </summary>
public class ErrorJsonConverter : JsonConverter<Error>
{
    /// <inheritdoc/>
    public override Error Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException(ExceptionMessages.ExpectedStartOfObject);
        }

        var code = DefaultValues.Code;
        var description = DefaultValues.Description;
        var numericType = 0;
        Dictionary<string, object>? metadata = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException(ExceptionMessages.ExpectedPropertyName);
            }

            var propertyName = reader.GetString()!;
            reader.Read();

            if (string.Equals(propertyName, JsonPropertyNames.Code, StringComparison.OrdinalIgnoreCase))
            {
                code = reader.GetString() ?? code;
            }
            else if (string.Equals(propertyName, JsonPropertyNames.Description, StringComparison.OrdinalIgnoreCase))
            {
                description = reader.GetString() ?? description;
            }
            else if (string.Equals(propertyName, JsonPropertyNames.Type, StringComparison.OrdinalIgnoreCase))
            {
                // Type can be serialized as a string (enum name) or number
                if (reader.TokenType == JsonTokenType.String)
                {
                    var typeString = reader.GetString();
                    if (typeString is not null && Enum.TryParse<ErrorType>(typeString, ignoreCase: true, out var errorType))
                    {
                        numericType = (int)errorType;
                    }
                }
                else if (reader.TokenType == JsonTokenType.Number)
                {
                    numericType = reader.GetInt32();
                }
            }
            else if (string.Equals(propertyName, JsonPropertyNames.NumericType, StringComparison.OrdinalIgnoreCase))
            {
                numericType = reader.GetInt32();
            }
            else if (string.Equals(propertyName, JsonPropertyNames.Metadata, StringComparison.OrdinalIgnoreCase))
            {
                if (reader.TokenType != JsonTokenType.Null)
                {
                    metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(ref reader, options);
                }
            }
            else
            {
                reader.Skip();
            }
        }

        return Error.Custom(numericType, code, description, metadata);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Error value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteString(JsonPropertyNames.Code, value.Code);
        writer.WriteString(JsonPropertyNames.Description, value.Description);
        writer.WriteString(JsonPropertyNames.Type, value.Type.ToString());
        writer.WriteNumber(JsonPropertyNames.NumericType, value.NumericType);

        if (value.Metadata is not null)
        {
            writer.WritePropertyName(JsonPropertyNames.Metadata);
            JsonSerializer.Serialize(writer, value.Metadata, options);
        }

        writer.WriteEndObject();
    }

    private static class JsonPropertyNames
    {
        public const string Code = "code";
        public const string Description = "description";
        public const string Type = "type";
        public const string NumericType = "numericType";
        public const string Metadata = "metadata";
    }

    private static class DefaultValues
    {
        public const string Code = "General.Unknown";
        public const string Description = "An unknown error occurred.";
    }

    private static class ExceptionMessages
    {
        public const string ExpectedStartOfObject = "Expected start of object";
        public const string ExpectedPropertyName = "Expected property name";
    }
}
