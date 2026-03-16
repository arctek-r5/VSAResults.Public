namespace VsaResults.Features.WideEvents;

/// <summary>
/// Marks a property on a request record for automatic inclusion in wide event context.
/// The property name will be converted to snake_case for the log key unless a custom key is specified.
/// </summary>
/// <example>
/// <code>
/// public sealed record Request(
///     [property: WideEventProperty] string ResourceId,
///     [property: WideEventProperty("tenant_id")] Guid TenantId,
///     string InternalField
/// );
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class WideEventPropertyAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WideEventPropertyAttribute"/> class with an optional custom key.
    /// </summary>
    /// <param name="key">Custom key name, or null to use the property name converted to snake_case.</param>
    public WideEventPropertyAttribute(string? key = null) => Key = key;

    /// <summary>
    /// Gets the optional custom key name. If not specified, the property name is converted to snake_case.
    /// </summary>
    public string? Key { get; }
}
