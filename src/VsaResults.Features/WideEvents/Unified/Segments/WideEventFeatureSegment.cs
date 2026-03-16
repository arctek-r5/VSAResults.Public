using System.Collections.Concurrent;

namespace VsaResults.Features.WideEvents.Unified.Segments;

/// <summary>
/// Feature execution segment for wide events.
/// Contains information specific to VSA feature pipeline execution.
/// Implemented as a record to enable safe, shallow cloning via <c>with {}</c>.
/// </summary>
public sealed record WideEventFeatureSegment
{
    /// <summary>Gets or sets the name of the feature being executed.</summary>
    public required string FeatureName { get; set; }

    /// <summary>Gets or sets the type of feature: "Mutation" or "Query".</summary>
    public required string FeatureType { get; set; }

    /// <summary>Gets or sets the name of the request type.</summary>
    public string? RequestType { get; set; }

    /// <summary>Gets or sets the name of the result type.</summary>
    public string? ResultType { get; set; }

    // Pipeline Stage Metadata

    /// <summary>Gets or sets the type name of the validator used.</summary>
    public string? ValidatorType { get; set; }

    /// <summary>Gets or sets the type name of the requirements enforcer used.</summary>
    public string? RequirementsType { get; set; }

    /// <summary>Gets or sets the type name of the mutator used.</summary>
    public string? MutatorType { get; set; }

    /// <summary>Gets or sets the type name of the query used.</summary>
    public string? QueryType { get; set; }

    /// <summary>Gets or sets the type name of the side effects handler used.</summary>
    public string? SideEffectsType { get; set; }

    /// <summary>Gets or sets a value indicating whether a custom validator was used (vs NoOp).</summary>
    public bool HasCustomValidator { get; set; }

    /// <summary>Gets or sets a value indicating whether custom requirements were used (vs NoOp).</summary>
    public bool HasCustomRequirements { get; set; }

    /// <summary>Gets or sets a value indicating whether custom side effects were used (vs NoOp).</summary>
    public bool HasCustomSideEffects { get; set; }

    // Timing Breakdown (milliseconds)

    /// <summary>Gets or sets the time spent in validation stage.</summary>
    public double? ValidationMs { get; set; }

    /// <summary>Gets or sets the time spent in requirements stage.</summary>
    public double? RequirementsMs { get; set; }

    /// <summary>Gets or sets the time spent in execution stage.</summary>
    public double? ExecutionMs { get; set; }

    /// <summary>Gets or sets the time spent in side effects stage.</summary>
    public double? SideEffectsMs { get; set; }

    // Entities loaded during requirements

    /// <summary>Gets or sets the type names of entities loaded during requirements stage.</summary>
    public ConcurrentDictionary<string, string> LoadedEntities { get; set; } = new();

    /// <summary>
    /// Creates a deep copy of this segment, including a copy of the <see cref="LoadedEntities"/> dictionary.
    /// </summary>
    /// <returns>A new segment with copied collection data.</returns>
    public WideEventFeatureSegment DeepClone() => this with
    {
        LoadedEntities = new ConcurrentDictionary<string, string>(LoadedEntities),
    };
}
