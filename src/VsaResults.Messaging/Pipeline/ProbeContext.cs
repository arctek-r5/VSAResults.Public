using System.Globalization;
using System.Text;

namespace VsaResults.Messaging.Pipeline;

/// <summary>
/// Context for pipeline diagnostics and introspection.
/// Collects information about filters in a pipeline for monitoring and debugging.
/// </summary>
public sealed class ProbeContext
{
    private readonly List<FilterProbe> _filters = [];
    private readonly string _scope;

    /// <summary>
    /// Creates a new probe context.
    /// </summary>
    public ProbeContext() : this(string.Empty)
    {
    }

    private ProbeContext(string scope)
    {
        _scope = scope;
    }

    /// <summary>Gets the probed filters.</summary>
    public IReadOnlyList<FilterProbe> Filters => _filters;

    /// <summary>
    /// Adds a filter to the probe.
    /// </summary>
    /// <param name="filterType">The filter type name.</param>
    /// <param name="properties">Optional properties describing the filter configuration.</param>
    /// <returns>This context for chaining.</returns>
    public ProbeContext Add(string filterType, Dictionary<string, object>? properties = null)
    {
        var fullName = string.IsNullOrEmpty(_scope) ? filterType : $"{_scope}.{filterType}";
        _filters.Add(new FilterProbe(fullName, properties ?? []));
        return this;
    }

    /// <summary>
    /// Adds a filter with a single property.
    /// </summary>
    /// <param name="filterType">The filter type name.</param>
    /// <param name="propertyName">The property name.</param>
    /// <param name="propertyValue">The property value.</param>
    /// <returns>This context for chaining.</returns>
    public ProbeContext Add(string filterType, string propertyName, object propertyValue)
    {
        return Add(filterType, new Dictionary<string, object> { [propertyName] = propertyValue });
    }

    /// <summary>
    /// Creates a scope for nested probing.
    /// </summary>
    /// <param name="name">The scope name.</param>
    /// <returns>A new probe context with the scope.</returns>
    public ProbeContext CreateScope(string name)
    {
        var scopedContext = new ProbeContext(string.IsNullOrEmpty(_scope) ? name : $"{_scope}.{name}");
        return scopedContext;
    }

    /// <summary>
    /// Merges another probe context into this one.
    /// </summary>
    /// <param name="other">The context to merge.</param>
    public void Merge(ProbeContext other)
    {
        foreach (var filter in other._filters)
        {
            _filters.Add(filter);
        }
    }

    /// <summary>
    /// Converts the probe results to a formatted string.
    /// </summary>
    /// <returns>A formatted string representation of the pipeline.</returns>
    public string ToFormattedString()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Pipeline Configuration:");
        builder.AppendLine("=======================");

        for (var i = 0; i < _filters.Count; i++)
        {
            var filter = _filters[i];
            builder.AppendLine(CultureInfo.InvariantCulture, $"{i + 1}. {filter.FilterType}");

            if (filter.Properties.Count > 0)
            {
                foreach (var (key, value) in filter.Properties)
                {
                    builder.AppendLine(CultureInfo.InvariantCulture, $"   - {key}: {value}");
                }
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Converts the probe results to a dictionary for serialization.
    /// </summary>
    /// <returns>A dictionary representation of the pipeline.</returns>
    public Dictionary<string, object> ToDictionary()
    {
        return new Dictionary<string, object>
        {
            ["filters"] = _filters.Select(f => new Dictionary<string, object>
            {
                ["type"] = f.FilterType,
                ["properties"] = f.Properties
            }).ToList()
        };
    }
}

/// <summary>
/// Information about a filter in the pipeline.
/// </summary>
/// <param name="FilterType">The filter type name.</param>
/// <param name="Properties">The filter properties/configuration.</param>
public sealed record FilterProbe(string FilterType, Dictionary<string, object> Properties);
