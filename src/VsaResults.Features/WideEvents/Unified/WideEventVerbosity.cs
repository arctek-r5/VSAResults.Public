namespace VsaResults.Features.WideEvents.Unified;

/// <summary>
/// Controls the level of detail included in wide events.
/// </summary>
public enum WideEventVerbosity
{
    /// <summary>
    /// Only essential fields: outcome, timing, errors, trace IDs.
    /// Best for high-throughput production with cost-sensitive logging.
    /// </summary>
    Minimal = 0,

    /// <summary>
    /// Standard fields including pipeline stages, loaded entities, and context.
    /// Recommended for most production environments.
    /// </summary>
    Standard = 1,

    /// <summary>
    /// All available fields including stack traces, full exception details,
    /// and nested child spans with their own context.
    /// Best for debugging and development environments.
    /// </summary>
    Verbose = 2,
}
