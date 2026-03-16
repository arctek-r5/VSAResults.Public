using System.Text.Json;
using System.Text.Json.Serialization;
using VsaResults.Errors;

namespace VsaResults.VsaResult.Serialization;

/// <summary>
/// JSON converter for the <see cref="VsaResult{TValue}"/> type.
/// </summary>
/// <typeparam name="TValue">The type of the value.</typeparam>
public class ErrorOrJsonConverter<TValue> : JsonConverter<VsaResult<TValue>>
{
    /// <inheritdoc/>
    public override VsaResult<TValue> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected start of object");
        }

        bool? isError = null;
        TValue? value = default;
        var hasValue = false;
        List<Error>? errors = null;

        var errorConverter = (JsonConverter<Error>?)options.GetConverter(typeof(Error));

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected property name");
            }

            var propertyName = reader.GetString()!;
            reader.Read();

            if (string.Equals(propertyName, "isError", StringComparison.OrdinalIgnoreCase))
            {
                isError = reader.GetBoolean();
            }
            else if (string.Equals(propertyName, "value", StringComparison.OrdinalIgnoreCase))
            {
                if (reader.TokenType != JsonTokenType.Null)
                {
                    value = JsonSerializer.Deserialize<TValue>(ref reader, options);
                    hasValue = true;
                }
            }
            else if (string.Equals(propertyName, "errors", StringComparison.OrdinalIgnoreCase))
            {
                if (reader.TokenType != JsonTokenType.Null)
                {
                    errors = [];
                    if (reader.TokenType == JsonTokenType.StartArray)
                    {
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                        {
                            Error error;
                            if (errorConverter is not null)
                            {
                                error = errorConverter.Read(ref reader, typeof(Error), options);
                            }
                            else
                            {
                                error = JsonSerializer.Deserialize<Error>(ref reader, options);
                            }

                            errors.Add(error);
                        }
                    }
                }
            }
            else
            {
                reader.Skip();
            }
        }

        if (isError == true && errors is not null && errors.Count > 0)
        {
            return errors;
        }

        if (isError == false && hasValue && value is not null)
        {
            return value;
        }

        // If we can't determine the state, try to infer from what we have
        if (errors is not null && errors.Count > 0)
        {
            return errors;
        }

        if (hasValue && value is not null)
        {
            return value;
        }

        throw new JsonException("Unable to deserialize ErrorOr: neither valid value nor errors were found");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, VsaResult<TValue> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteBoolean("isError", value.IsError);

        if (value.IsError)
        {
            writer.WritePropertyName("errors");
            writer.WriteStartArray();

            var errorConverter = (JsonConverter<Error>?)options.GetConverter(typeof(Error));
            foreach (var error in value.Errors)
            {
                if (errorConverter is not null)
                {
                    errorConverter.Write(writer, error, options);
                }
                else
                {
                    JsonSerializer.Serialize(writer, error, options);
                }
            }

            writer.WriteEndArray();
        }
        else
        {
            writer.WritePropertyName("value");
            JsonSerializer.Serialize(writer, value.Value, options);
        }

        writer.WriteEndObject();
    }
}
