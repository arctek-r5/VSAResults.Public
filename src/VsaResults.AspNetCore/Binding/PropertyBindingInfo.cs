using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace VsaResults.AspNetCore.Binding;

/// <summary>
/// Cached binding information for a property.
/// </summary>
internal sealed class PropertyBindingInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyBindingInfo"/> class.
    /// </summary>
    /// <param name="property">The property to create binding info for.</param>
    public PropertyBindingInfo(PropertyInfo property)
    {
        Property = property;
        PropertyName = property.Name;
        PropertyType = property.PropertyType;
        IsCollection = GetCollectionElementType(PropertyType) is not null;

        var parameter = GetMatchingParameter(property);

        // Determine binding source from attributes
        var fromRoute = property.GetCustomAttribute<FromRouteAttribute>()
                        ?? parameter?.GetCustomAttribute<FromRouteAttribute>();
        var fromQuery = property.GetCustomAttribute<FromQueryAttribute>()
                        ?? parameter?.GetCustomAttribute<FromQueryAttribute>();
        var fromHeader = property.GetCustomAttribute<FromHeaderAttribute>()
                         ?? parameter?.GetCustomAttribute<FromHeaderAttribute>();
        var fromBody = property.GetCustomAttribute<FromBodyAttribute>()
                       ?? parameter?.GetCustomAttribute<FromBodyAttribute>();
        var fromServices = property.GetCustomAttribute<FromServicesAttribute>()
                           ?? parameter?.GetCustomAttribute<FromServicesAttribute>();

        if (fromRoute is not null)
        {
            Source = BindingSource.Route;
            BindingName = fromRoute.Name ?? property.Name;
            SourceName = "route";
        }
        else if (fromQuery is not null)
        {
            Source = BindingSource.Query;
            BindingName = fromQuery.Name ?? property.Name;
            SourceName = "query";
        }
        else if (fromHeader is not null)
        {
            Source = BindingSource.Header;
            BindingName = fromHeader.Name ?? property.Name;
            SourceName = "header";
        }
        else if (fromBody is not null)
        {
            Source = BindingSource.Body;
            BindingName = property.Name;
            SourceName = "body";
            IsBodyRoot = true;
        }
        else if (fromServices is not null)
        {
            Source = BindingSource.Services;
            BindingName = property.Name;
            SourceName = "services";
        }
        else
        {
            // Default: query for simple types, body for complex types
            Source = IsSimpleType(PropertyType) ? BindingSource.Query : BindingSource.Body;
            BindingName = property.Name;
            SourceName = Source == BindingSource.Query ? "query" : "body";
        }

        HasExplicitSource = fromRoute is not null ||
                            fromQuery is not null ||
                            fromHeader is not null ||
                            fromBody is not null ||
                            fromServices is not null;
        AllowRouteFallback = !HasExplicitSource && IsSimpleType(PropertyType);

        // Check if required
        IsRequired = property.GetCustomAttribute<System.ComponentModel.DataAnnotations.RequiredAttribute>() is not null
                     || parameter?.GetCustomAttribute<System.ComponentModel.DataAnnotations.RequiredAttribute>() is not null
                     || (property.PropertyType.IsValueType && Nullable.GetUnderlyingType(property.PropertyType) is null
                         && property.GetCustomAttribute<FromRouteAttribute>() is not null);

        // Check for default value
        var defaultAttr = property.GetCustomAttribute<System.ComponentModel.DefaultValueAttribute>()
                          ?? parameter?.GetCustomAttribute<System.ComponentModel.DefaultValueAttribute>();
        if (defaultAttr is not null)
        {
            HasDefaultValue = true;
            DefaultValue = defaultAttr.Value;
        }
    }

    /// <summary>Gets the property info.</summary>
    public PropertyInfo Property { get; }

    /// <summary>Gets the property name.</summary>
    public string PropertyName { get; }

    /// <summary>Gets the property type.</summary>
    public Type PropertyType { get; }

    /// <summary>Gets the binding source.</summary>
    public BindingSource Source { get; }

    /// <summary>Gets the name used for binding.</summary>
    public string BindingName { get; }

    /// <summary>Gets the source name for error messages.</summary>
    public string SourceName { get; }

    /// <summary>Gets a value indicating whether the property is required.</summary>
    public bool IsRequired { get; }

    /// <summary>Gets a value indicating whether the binding source was explicitly specified.</summary>
    public bool HasExplicitSource { get; }

    /// <summary>Gets a value indicating whether to allow route fallback when unbound.</summary>
    public bool AllowRouteFallback { get; }

    /// <summary>Gets a value indicating whether the property is a collection.</summary>
    public bool IsCollection { get; }

    /// <summary>Gets a value indicating whether this is the root body object.</summary>
    public bool IsBodyRoot { get; }

    /// <summary>Gets a value indicating whether the property has a default value.</summary>
    public bool HasDefaultValue { get; }

    /// <summary>Gets the default value.</summary>
    public object? DefaultValue { get; }

    private static bool IsSimpleType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsPrimitive
               || underlying == typeof(string)
               || underlying == typeof(decimal)
               || underlying == typeof(DateTime)
               || underlying == typeof(DateTimeOffset)
               || underlying == typeof(TimeSpan)
               || underlying == typeof(Guid)
               || underlying.IsEnum;
    }

    private static Type? GetCollectionElementType(Type type)
    {
        if (type == typeof(string))
        {
            return null;
        }

        if (type.IsArray)
        {
            return type.GetElementType();
        }

        if (!type.IsGenericType)
        {
            return null;
        }

        var genericArgs = type.GetGenericArguments();
        if (genericArgs.Length != 1)
        {
            return null;
        }

        var enumerableType = typeof(IEnumerable<>).MakeGenericType(genericArgs[0]);
        return enumerableType.IsAssignableFrom(type)
            ? genericArgs[0]
            : null;
    }

    private static ParameterInfo? GetMatchingParameter(PropertyInfo property)
    {
        var declaringType = property.DeclaringType;
        if (declaringType is null)
        {
            return null;
        }

        return declaringType
            .GetConstructors()
            .OrderByDescending(ctor => ctor.GetParameters().Length)
            .SelectMany(ctor => ctor.GetParameters())
            .FirstOrDefault(parameter =>
                string.Equals(parameter.Name, property.Name, StringComparison.OrdinalIgnoreCase));
    }
}
