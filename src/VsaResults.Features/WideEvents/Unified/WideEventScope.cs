using VsaResults.Errors;
using VsaResults.Features.WideEvents.Unified.Segments;

namespace VsaResults.Features.WideEvents.Unified;

/// <summary>
/// Ambient scope for aggregating child wide events into a parent event.
/// Uses AsyncLocal to flow context across async boundaries.
///
/// <example>
/// <code>
/// using var scope = new WideEventScope("message", WideEventAggregationMode.AggregateToParent);
/// // Message consumption starts...
/// // Feature execution happens → captured as ChildSpan instead of separate event
/// var aggregatedEvent = scope.Complete("success");
/// // Single event emitted with full context
/// </code>
/// </example>
/// </summary>
public sealed class WideEventScope : IDisposable
{
    private static readonly AsyncLocal<WideEventScope?> CurrentScope = new();

    private readonly WideEventScope? _parent;
    private readonly WideEventBuilder _builder;
    private readonly WideEventAggregationMode _mode;
    private readonly List<WideEvent> _capturedEvents = [];
    private readonly Lock _lock = new();
    private bool _isDisposed;
    private bool _isCompleted;

    /// <summary>
    /// Initializes a new instance of the <see cref="WideEventScope"/> class.
    /// </summary>
    /// <param name="eventType">The type of event being scoped (e.g., "message", "feature").</param>
    /// <param name="mode">The aggregation mode for child events.</param>
    public WideEventScope(string eventType, WideEventAggregationMode mode = WideEventAggregationMode.AggregateToParent)
    {
        _parent = CurrentScope.Value;
        _mode = mode;
        _builder = new WideEventBuilder(eventType);
        CurrentScope.Value = this;
    }

    /// <summary>
    /// Gets the current active scope, or null if no scope is active.
    /// </summary>
    public static WideEventScope? Current => CurrentScope.Value;

    /// <summary>
    /// Gets a value indicating whether there is an active scope.
    /// </summary>
    public static bool IsActive => CurrentScope.Value != null;

    /// <summary>
    /// Gets the parent scope if this scope is nested, or null for the root scope.
    /// </summary>
    public WideEventScope? Parent => _parent;

    /// <summary>
    /// Gets the aggregation mode for this scope.
    /// </summary>
    public WideEventAggregationMode Mode => _mode;

    /// <summary>
    /// Gets the underlying builder for this scope.
    /// </summary>
    public WideEventBuilder Builder => _builder;

    /// <summary>
    /// Gets the event ID for this scope's event.
    /// Used as CausationId for linked events.
    /// </summary>
    public string EventId => _builder.Event.EventId;

    /// <summary>
    /// Gets the captured child events (for testing/inspection).
    /// </summary>
    internal IReadOnlyList<WideEvent> CapturedEvents
    {
        get
        {
            lock (_lock)
            {
                return _capturedEvents.ToList();
            }
        }
    }

    /// <summary>
    /// Tries to report a child event to the current scope.
    /// If a scope is active, the event is captured according to the aggregation mode.
    /// </summary>
    /// <param name="childEvent">The child event to report.</param>
    /// <returns>True if the event was captured; false if it should be emitted normally.</returns>
    public static bool TryReportToCurrentScope(WideEvent childEvent)
    {
        var scope = Current;
        return scope?.TryCaptureChildEvent(childEvent) ?? false;
    }

    /// <summary>
    /// Attempts to capture a child event based on the current aggregation mode.
    /// </summary>
    /// <param name="childEvent">The child event to potentially capture.</param>
    /// <returns>
    /// True if the event was captured (caller should NOT emit it separately).
    /// False if the event should be emitted normally.
    /// </returns>
    public bool TryCaptureChildEvent(WideEvent childEvent)
    {
        if (_isDisposed || _isCompleted)
        {
            return false;
        }

        switch (_mode)
        {
            case WideEventAggregationMode.AggregateToParent:
                lock (_lock)
                {
                    _capturedEvents.Add(childEvent);
                }

                return true;

            case WideEventAggregationMode.LinkOnly:
                // Set causation ID but let the caller emit it separately.
                // Safe to mutate because WideEventBuilder.Build() returns a clone,
                // so childEvent is already an isolated copy at this point.
                childEvent.CausationId = EventId;
                return false;

            case WideEventAggregationMode.Independent:
            default:
                return false;
        }
    }

    /// <summary>
    /// Completes the scope with a successful outcome.
    /// Converts captured events to child spans and returns the aggregated event.
    /// </summary>
    /// <returns>The completed wide event with aggregated child spans.</returns>
    public WideEvent CompleteSuccess()
    {
        return Complete(_builder.Success);
    }

    /// <summary>
    /// Completes the scope with the specified errors (validation failure).
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    /// <returns>The completed wide event.</returns>
    public WideEvent CompleteValidationFailure(IReadOnlyList<Error> errors)
    {
        return Complete(() => _builder.ValidationFailure(errors));
    }

    /// <summary>
    /// Completes the scope with the specified errors (requirements failure).
    /// </summary>
    /// <param name="errors">The requirements errors.</param>
    /// <returns>The completed wide event.</returns>
    public WideEvent CompleteRequirementsFailure(IReadOnlyList<Error> errors)
    {
        return Complete(() => _builder.RequirementsFailure(errors));
    }

    /// <summary>
    /// Completes the scope with the specified errors (execution failure).
    /// </summary>
    /// <param name="errors">The execution errors.</param>
    /// <returns>The completed wide event.</returns>
    public WideEvent CompleteExecutionFailure(IReadOnlyList<Error> errors)
    {
        return Complete(() => _builder.ExecutionFailure(errors));
    }

    /// <summary>
    /// Completes the scope with an exception.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="includeStackTrace">Whether to include the stack trace.</param>
    /// <returns>The completed wide event.</returns>
    public WideEvent CompleteException(Exception exception, bool includeStackTrace = false)
    {
        return Complete(() => _builder.Exception(exception, includeStackTrace));
    }

    /// <summary>
    /// Completes the scope with consumer error (for message processing).
    /// </summary>
    /// <param name="errors">The consumer errors.</param>
    /// <returns>The completed wide event.</returns>
    public WideEvent CompleteConsumerError(IReadOnlyList<Error> errors)
    {
        return Complete(() => _builder.ConsumerError(errors));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        CurrentScope.Value = _parent;
    }

    private WideEvent Complete(Func<WideEvent> completionFunc)
    {
        if (_isCompleted)
        {
            throw new InvalidOperationException("Scope has already been completed.");
        }

        _isCompleted = true;

        // Convert captured events to child spans
        lock (_lock)
        {
            foreach (var capturedEvent in _capturedEvents)
            {
                _builder.AddChildSpan(WideEventChildSpan.FromWideEvent(capturedEvent));
            }
        }

        return completionFunc();
    }
}
