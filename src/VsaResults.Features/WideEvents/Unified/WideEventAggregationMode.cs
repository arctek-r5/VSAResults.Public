using VsaResults.Features.WideEvents.Unified.Segments;

namespace VsaResults.Features.WideEvents.Unified;

/// <summary>
/// Specifies how child events should be handled when emitted within a parent scope.
/// </summary>
public enum WideEventAggregationMode
{
    /// <summary>
    /// Child events are captured as <see cref="WideEventChildSpan"/> within the parent event.
    /// Only the parent event is emitted at scope completion.
    /// This is the default and recommended mode for message-to-feature scenarios.
    /// </summary>
    AggregateToParent = 0,

    /// <summary>
    /// Child events are emitted separately but linked via CausationId.
    /// Both parent and child events appear in the telemetry stream.
    /// </summary>
    LinkOnly = 1,

    /// <summary>
    /// No aggregation or linking - current behavior.
    /// Events are completely independent.
    /// </summary>
    Independent = 2,
}
