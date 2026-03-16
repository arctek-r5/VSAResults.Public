using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VsaResults.VsaResult.Serialization;

/// <summary>
/// Factory for creating <see cref="ErrorOrJsonConverter{TValue}"/> instances.
/// Converters are cached for performance.
/// </summary>
public class ErrorOrJsonConverterFactory : JsonConverterFactory
{
    private static readonly ConcurrentDictionary<Type, JsonConverter> ConverterCache = new();

    /// <inheritdoc/>
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(VsaResult<>);

    /// <inheritdoc/>
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        ConverterCache.GetOrAdd(typeToConvert, static type =>
        {
            var converterType = typeof(ErrorOrJsonConverter<>).MakeGenericType(type.GetGenericArguments()[0]);
            return (JsonConverter)Activator.CreateInstance(converterType)!;
        });
}
