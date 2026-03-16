using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using VsaResults.Errors;
using VsaResults.VsaResult;

namespace VsaResults.AspNetCore.Binding;

/// <summary>
/// Manual request binder that provides full control over model binding,
/// allowing wide events to be emitted for binding failures.
/// </summary>
public static class RequestBinder
{
    private static readonly ConcurrentDictionary<Type, PropertyBindingInfo[]> BindingInfoCache = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Binds a request from the HTTP context.
    /// </summary>
    /// <typeparam name="TRequest">The request type to bind.</typeparam>
    /// <param name="context">The HTTP context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The bound request or binding errors.</returns>
    public static async ValueTask<VsaResult<TRequest>> BindAsync<TRequest>(
        HttpContext context,
        CancellationToken ct = default)
    {
        var errors = new List<Error>();
        var bindingInfos = GetBindingInfos<TRequest>();
        var boundValues = new Dictionary<string, BoundValue>(StringComparer.OrdinalIgnoreCase);

        // Pre-read body if any properties need it
        var bodyState = await ReadBodyIfNeededAsync(context, bindingInfos, ct);

        foreach (var info in bindingInfos)
        {
            try
            {
                var value = info.Source switch
                {
                    BindingSource.Route => BindFromRoute(context, info),
                    BindingSource.Query => BindFromQuery(context, info),
                    BindingSource.Header => BindFromHeader(context, info),
                    BindingSource.Body => BindFromBody(info, bodyState),
                    BindingSource.Services => BindFromServices(context, info),
                    _ => GetDefaultValue(info),
                };

                var isProvided = value is not null;
                if (!isProvided && info.AllowRouteFallback)
                {
                    value = BindFromRoute(context, info);
                    isProvided = value is not null;
                }

                // Check for required properties
                if (!isProvided && info.IsRequired)
                {
                    errors.Add(Error.Validation(
                        $"Binding.{info.PropertyName}.Required",
                        $"The {info.SourceName} parameter '{info.BindingName}' is required."));
                    continue;
                }

                // Convert value if needed
                if (isProvided && value is not null && value.GetType() != info.PropertyType)
                {
                    var convertResult = ConvertValue(value, info.PropertyType, info.BindingName);
                    if (convertResult.IsError)
                    {
                        errors.AddRange(convertResult.Errors);
                        continue;
                    }

                    value = convertResult.Value;
                }

                boundValues[info.PropertyName] = new BoundValue(info, value, isProvided);
            }
            catch (Exception ex)
            {
                errors.Add(Error.Validation(
                    $"Binding.{info.PropertyName}.Failed",
                    $"Failed to bind '{info.BindingName}' from {info.SourceName}: {ex.Message}"));
            }
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        if (!TryCreateRequestInstance<TRequest>(boundValues, out var request, out var ctorParameters, out var createErrors))
        {
            return createErrors;
        }

        foreach (var info in bindingInfos)
        {
            if (!boundValues.TryGetValue(info.PropertyName, out var bound))
            {
                continue;
            }

            if (!bound.IsProvided && ctorParameters.Contains(info.PropertyName))
            {
                continue;
            }

            var valueToSet = bound.IsProvided
                ? bound.Value
                : info.HasDefaultValue
                    ? info.DefaultValue
                    : null;

            if (!bound.IsProvided && valueToSet is null && !info.HasDefaultValue)
            {
                continue;
            }

            try
            {
                info.Property.SetValue(request, valueToSet);
            }
            catch (Exception ex)
            {
                errors.Add(Error.Validation(
                    $"Binding.{info.PropertyName}.Failed",
                    $"Failed to bind '{info.BindingName}' from {info.SourceName}: {ex.Message}"));
            }
        }

        return errors.Count > 0
            ? errors
            : request;
    }

    /// <summary>
    /// Gets binding context information for debugging/logging.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A dictionary of binding context for wide events.</returns>
    public static Dictionary<string, object?> GetBindingContext<TRequest>(HttpContext context)
    {
        var ctx = new Dictionary<string, object?>
        {
            ["request_type"] = typeof(TRequest).Name,
            ["http_method"] = context.Request.Method,
            ["http_path"] = context.Request.Path.Value,
        };

        // Add route values
        foreach (var (key, value) in context.Request.RouteValues)
        {
            ctx[$"route_{key}"] = value?.ToString();
        }

        // Add query parameters (limited to avoid bloat)
        var queryCount = 0;
        foreach (var (key, value) in context.Request.Query)
        {
            if (queryCount++ >= 10)
            {
                break;
            }

            ctx[$"query_{key}"] = value.ToString();
        }

        return ctx;
    }

    private static PropertyBindingInfo[] GetBindingInfos<TRequest>()
    {
        return BindingInfoCache.GetOrAdd(typeof(TRequest), type =>
        {
            // Include properties that are writable OR init-only (common in C# records).
            // Init-only properties have a setter with IsInitOnly = true on the return parameter.
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite || IsInitOnly(p))
                .Select(p => new PropertyBindingInfo(p))
                .ToArray();
            return properties;
        });
    }

    private static async ValueTask<BodyState> ReadBodyIfNeededAsync(
        HttpContext context,
        PropertyBindingInfo[] bindingInfos,
        CancellationToken ct)
    {
        // Check if any property needs body binding
        var needsBody = bindingInfos.Any(i => i.Source == BindingSource.Body);
        if (!needsBody)
        {
            return new BodyState();
        }

        // Try to read and parse the body
        if (context.Request.ContentLength > 0 || context.Request.ContentType?.Contains("json") == true)
        {
            try
            {
                using var doc = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: ct);
                return new BodyState { Json = doc.RootElement.Clone() };
            }
            catch
            {
                // Body is not valid JSON - will be handled as null
            }
        }

        return new BodyState();
    }

    private static object? BindFromRoute(HttpContext context, PropertyBindingInfo info)
    {
        if (context.Request.RouteValues.TryGetValue(info.BindingName, out var value))
        {
            return value;
        }

        return null;
    }

    private static object? BindFromQuery(HttpContext context, PropertyBindingInfo info)
    {
        if (context.Request.Query.TryGetValue(info.BindingName, out var values))
        {
            // Handle collections
            if (info.IsCollection)
            {
                return values.ToArray();
            }

            return values.Count > 0 ? values[0] : null;
        }

        return null;
    }

    private static string? BindFromHeader(HttpContext context, PropertyBindingInfo info)
    {
        if (context.Request.Headers.TryGetValue(info.BindingName, out var values))
        {
            return values.Count > 0 ? values[0] : null;
        }

        return null;
    }

    private static object? BindFromBody(PropertyBindingInfo info, BodyState bodyState)
    {
        if (bodyState.Json is not { } bodyJson)
        {
            return null;
        }

        // If the property is the entire body
        if (info.IsBodyRoot)
        {
            return JsonSerializer.Deserialize(bodyJson.GetRawText(), info.PropertyType, JsonOptions);
        }

        // Otherwise, look for a specific property in the body
        if (bodyJson.TryGetProperty(info.BindingName, out var prop) ||
            bodyJson.TryGetProperty(ToCamelCase(info.BindingName), out prop))
        {
            return JsonSerializer.Deserialize(prop.GetRawText(), info.PropertyType, JsonOptions);
        }

        return null;
    }

    private static object? BindFromServices(HttpContext context, PropertyBindingInfo info)
    {
        return context.RequestServices.GetService(info.PropertyType);
    }

    private static object? GetDefaultValue(PropertyBindingInfo info)
    {
        if (info.HasDefaultValue)
        {
            return info.DefaultValue;
        }

        return info.PropertyType.IsValueType
            ? Activator.CreateInstance(info.PropertyType)
            : null;
    }

    private static bool TryCreateRequestInstance<TRequest>(
        Dictionary<string, BoundValue> boundValues,
        out TRequest request,
        out HashSet<string> ctorParameters,
        out List<Error> errors)
    {
        errors = [];
        ctorParameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var requestType = typeof(TRequest);
        var parameterless = requestType.GetConstructor(Type.EmptyTypes);
        if (parameterless is not null)
        {
            request = (TRequest)Activator.CreateInstance(requestType)!;
            return true;
        }

        var constructors = requestType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(c => c.GetParameters().Length)
            .ToArray();

        if (constructors.Length == 0)
        {
            errors.Add(Error.Validation(
                "Binding.Request.CreationFailed",
                $"No public constructors found for request type '{requestType.Name}'."));
            request = default!;
            return false;
        }

        var ctor = constructors[0];
        var parameters = ctor.GetParameters();
        var args = new object?[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            if (parameter.Name is null)
            {
                errors.Add(Error.Validation(
                    "Binding.Request.CreationFailed",
                    $"Constructor parameter name missing for request type '{requestType.Name}'."));
                request = default!;
                return false;
            }

            ctorParameters.Add(parameter.Name);

            if (boundValues.TryGetValue(parameter.Name, out var bound) && bound.IsProvided)
            {
                args[i] = bound.Value;
                continue;
            }

            if (parameter.HasDefaultValue)
            {
                args[i] = parameter.DefaultValue;
                continue;
            }

            if (boundValues.TryGetValue(parameter.Name, out bound) && bound.Info.HasDefaultValue)
            {
                args[i] = bound.Info.DefaultValue;
                continue;
            }

            args[i] = GetDefaultValue(parameter.ParameterType);
        }

        try
        {
            request = (TRequest)ctor.Invoke(args);
            return true;
        }
        catch (Exception ex)
        {
            errors.Add(Error.Validation(
                "Binding.Request.CreationFailed",
                $"Failed to create request type '{requestType.Name}': {ex.Message}"));
            request = default!;
            return false;
        }
    }

    private static object? GetDefaultValue(Type type)
    {
        return type.IsValueType
            ? Activator.CreateInstance(type)
            : null;
    }

    private static VsaResult<object?> ConvertValue(object value, Type targetType, string parameterName)
    {
        try
        {
            // Handle nullable types
            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (TryGetCollectionElementType(underlyingType, out var elementType))
            {
                return ConvertCollectionValue(value, underlyingType, elementType, parameterName);
            }

            // Handle string to various types
            if (value is string stringValue)
            {
                // Handle Guid
                if (underlyingType == typeof(Guid))
                {
                    if (Guid.TryParse(stringValue, out var guid))
                    {
                        return guid;
                    }

                    return Error.Validation(
                        $"Binding.{parameterName}.InvalidGuid",
                        $"The value '{stringValue}' is not a valid GUID.");
                }

                // Handle enums
                if (underlyingType.IsEnum)
                {
                    if (Enum.TryParse(underlyingType, stringValue, ignoreCase: true, out var enumValue))
                    {
                        return enumValue;
                    }

                    var validValues = string.Join(", ", Enum.GetNames(underlyingType));
                    return Error.Validation(
                        $"Binding.{parameterName}.InvalidEnum",
                        $"The value '{stringValue}' is not valid. Valid values: {validValues}");
                }

                // Use TypeConverter for other types
                var converter = TypeDescriptor.GetConverter(underlyingType);
                if (converter.CanConvertFrom(typeof(string)))
                {
                    return converter.ConvertFromString(stringValue);
                }
            }

            // Handle StringValues
            if (value is StringValues stringValues)
            {
                return ConvertValue(stringValues.ToString(), targetType, parameterName);
            }

            // Direct assignment if compatible
            if (targetType.IsInstanceOfType(value))
            {
                return value;
            }

            // Try Convert.ChangeType
            return Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            return Error.Validation(
                $"Binding.{parameterName}.ConversionFailed",
                $"Failed to convert value to {targetType.Name}: {ex.Message}");
        }
    }

    private static VsaResult<object?> ConvertCollectionValue(
        object value,
        Type targetType,
        Type elementType,
        string parameterName)
    {
        if (value is not IEnumerable rawValues || value is string)
        {
            return Error.Validation(
                $"Binding.{parameterName}.ConversionFailed",
                $"Failed to convert value to {targetType.Name}: expected a collection input.");
        }

        var convertedValues = new List<object?>();
        foreach (var item in rawValues)
        {
            var conversion = ConvertScalarValue(item, elementType, parameterName);
            if (conversion.IsError)
            {
                return conversion;
            }

            convertedValues.Add(conversion.Value);
        }

        if (targetType.IsArray)
        {
            var array = Array.CreateInstance(elementType, convertedValues.Count);
            for (var i = 0; i < convertedValues.Count; i++)
            {
                array.SetValue(convertedValues[i], i);
            }

            return array;
        }

        var listType = typeof(List<>).MakeGenericType(elementType);
        var list = (IList)Activator.CreateInstance(listType)!;
        foreach (var item in convertedValues)
        {
            list.Add(item);
        }

        if (targetType.IsAssignableFrom(listType))
        {
            return VsaResultFactory.From<object?>(list);
        }

        if (targetType.IsGenericType)
        {
            var genericDef = targetType.GetGenericTypeDefinition();
            if (genericDef == typeof(HashSet<>))
            {
                return VsaResultFactory.From<object?>(Activator.CreateInstance(targetType, list));
            }
        }

        if (Activator.CreateInstance(targetType, list) is { } collectionInstance)
        {
            return VsaResultFactory.From<object?>(collectionInstance);
        }

        return Error.Validation(
            $"Binding.{parameterName}.ConversionFailed",
            $"Failed to convert value to {targetType.Name}: unsupported collection type.");
    }

    private static VsaResult<object?> ConvertScalarValue(object? value, Type targetType, string parameterName)
    {
        try
        {
            if (value is null)
            {
                return VsaResultFactory.From<object?>(null);
            }

            if (value is StringValues stringValues)
            {
                value = stringValues.ToString();
            }

            if (targetType.IsInstanceOfType(value))
            {
                return value;
            }

            if (value is string stringValue)
            {
                if (targetType == typeof(Guid))
                {
                    if (Guid.TryParse(stringValue, out var guid))
                    {
                        return guid;
                    }

                    return Error.Validation(
                        $"Binding.{parameterName}.InvalidGuid",
                        $"The value '{stringValue}' is not a valid GUID.");
                }

                if (targetType.IsEnum)
                {
                    if (Enum.TryParse(targetType, stringValue, ignoreCase: true, out var enumValue))
                    {
                        return enumValue;
                    }

                    var validValues = string.Join(", ", Enum.GetNames(targetType));
                    return Error.Validation(
                        $"Binding.{parameterName}.InvalidEnum",
                        $"The value '{stringValue}' is not valid. Valid values: {validValues}");
                }

                var converter = TypeDescriptor.GetConverter(targetType);
                if (converter.CanConvertFrom(typeof(string)))
                {
                    return converter.ConvertFromString(stringValue);
                }
            }

            return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            return Error.Validation(
                $"Binding.{parameterName}.ConversionFailed",
                $"Failed to convert value to {targetType.Name}: {ex.Message}");
        }
    }

    private static bool TryGetCollectionElementType(Type type, out Type elementType)
    {
        if (type == typeof(string))
        {
            elementType = null!;
            return false;
        }

        if (type.IsArray)
        {
            elementType = type.GetElementType()!;
            return true;
        }

        if (type.IsGenericType)
        {
            var genericArgs = type.GetGenericArguments();
            if (genericArgs.Length == 1)
            {
                var enumerableType = typeof(IEnumerable<>).MakeGenericType(genericArgs[0]);
                if (enumerableType.IsAssignableFrom(type))
                {
                    elementType = genericArgs[0];
                    return true;
                }
            }
        }

        elementType = null!;
        return false;
    }

    /// <summary>
    /// Detects init-only properties (C# <c>init</c> accessors) which have <c>CanWrite = false</c>
    /// but can still be set during object construction. These are identified by the
    /// <c>IsExternalInit</c> modifier on the setter's return parameter.
    /// </summary>
    private static bool IsInitOnly(PropertyInfo property)
    {
        var setter = property.GetSetMethod(nonPublic: true);
        if (setter is null)
        {
            return false;
        }

        return setter.ReturnParameter
            .GetRequiredCustomModifiers()
            .Any(m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit");
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
        {
            return name;
        }

        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private readonly record struct BoundValue(PropertyBindingInfo Info, object? Value, bool IsProvided);

    /// <summary>
    /// State container for parsed request body.
    /// </summary>
    private sealed class BodyState
    {
        public JsonElement? Json { get; init; }
    }
}
