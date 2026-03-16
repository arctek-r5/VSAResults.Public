namespace VsaResults.Features.WideEvents.Unified;

/// <summary>
/// Configuration options for the unified wide events system.
/// Controls verbosity, sampling, filtering, and context limits.
/// </summary>
public sealed class WideEventOptions
{
    /// <summary>
    /// Gets or sets the verbosity level for wide events.
    /// Default: <see cref="WideEventVerbosity.Standard"/>.
    /// </summary>
    public WideEventVerbosity Verbosity { get; set; } = WideEventVerbosity.Standard;

    /// <summary>
    /// Gets or sets the default aggregation mode for scopes.
    /// Default: <see cref="WideEventAggregationMode.AggregateToParent"/>.
    /// </summary>
    public WideEventAggregationMode DefaultAggregationMode { get; set; } = WideEventAggregationMode.AggregateToParent;

    /// <summary>
    /// Gets or sets the sampling rate for successful events (0.0 to 1.0).
    /// A value of 0.1 means 10% of success events are emitted.
    /// Default: 1.0 (all events emitted).
    /// </summary>
    /// <remarks>
    /// Sampling only applies to successful events. Failures and exceptions
    /// are always emitted unless overridden by <see cref="AlwaysCaptureOutcomes"/>.
    /// </remarks>
    public double SamplingRate { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the outcomes that are always captured regardless of sampling.
    /// Default: exception, validation_failure, requirements_failure, execution_failure,
    /// side_effects_failure, consumer_error, deserialization_error, retry_exhausted.
    /// </summary>
    public HashSet<string> AlwaysCaptureOutcomes { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "exception",
        "validation_failure",
        "requirements_failure",
        "execution_failure",
        "side_effects_failure",
        "consumer_error",
        "deserialization_error",
        "retry_exhausted",
        "circuit_breaker_open",
        "timeout",
    };

    /// <summary>
    /// Gets or sets the context keys to exclude from events.
    /// Used to filter sensitive data before emission.
    /// Default: password, token, secret, key, authorization, bearer.
    /// </summary>
    public HashSet<string> ExcludedContextKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "token",
        "secret",
        "key",
        "authorization",
        "bearer",
        "api_key",
        "apikey",
        "access_token",
        "refresh_token",
    };

    /// <summary>
    /// Gets or sets the maximum number of context entries per event.
    /// Excess entries are dropped (oldest first for dictionaries).
    /// Default: 50.
    /// </summary>
    public int MaxContextEntries { get; set; } = 50;

    /// <summary>
    /// Gets or sets the maximum string value length in context.
    /// Longer strings are truncated with "..." suffix.
    /// Default: 1000.
    /// </summary>
    public int MaxStringValueLength { get; set; } = 1000;

    /// <summary>
    /// Gets or sets a value indicating whether to include exception stack traces.
    /// Default: false (recommended for production).
    /// </summary>
    /// <remarks>
    /// Stack traces can significantly increase event size and may expose
    /// internal implementation details. Consider enabling only in
    /// development/staging environments.
    /// </remarks>
    public bool IncludeStackTraces { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of child spans per event.
    /// Excess child spans are dropped (oldest first).
    /// Default: 20.
    /// </summary>
    public int MaxChildSpans { get; set; } = 20;

    /// <summary>
    /// Gets or sets a value indicating whether to include child span context in aggregated events.
    /// When false, child span context is omitted to reduce event size.
    /// Default: true.
    /// </summary>
    public bool IncludeChildSpanContext { get; set; } = true;

    /// <summary>
    /// Gets or sets the event types that should create scopes automatically.
    /// Default: message.
    /// </summary>
    public HashSet<string> AutoScopeEventTypes { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "message",
    };

    /// <summary>
    /// Gets or sets a value indicating whether sampling is enabled.
    /// When disabled, all events are emitted (useful for debugging).
    /// Default: true.
    /// </summary>
    public bool EnableSampling { get; set; } = true;

    /// <summary>
    /// Creates default options for development environments.
    /// Verbose logging, all events captured, stack traces included.
    /// </summary>
    /// <returns>Options configured for development.</returns>
    public static WideEventOptions Development()
    {
        return new WideEventOptions
        {
            Verbosity = WideEventVerbosity.Verbose,
            SamplingRate = 1.0,
            EnableSampling = false,
            IncludeStackTraces = true,
            MaxContextEntries = 100,
            MaxStringValueLength = 5000,
        };
    }

    /// <summary>
    /// Creates default options for production environments.
    /// Standard logging, 10% sampling for successes, no stack traces.
    /// </summary>
    /// <returns>Options configured for production.</returns>
    public static WideEventOptions Production()
    {
        return new WideEventOptions
        {
            Verbosity = WideEventVerbosity.Standard,
            SamplingRate = 0.1,
            EnableSampling = true,
            IncludeStackTraces = false,
        };
    }

    /// <summary>
    /// Creates minimal options for high-throughput production.
    /// Minimal logging, 1% sampling, reduced limits.
    /// </summary>
    /// <returns>Options configured for high-throughput.</returns>
    public static WideEventOptions HighThroughput()
    {
        return new WideEventOptions
        {
            Verbosity = WideEventVerbosity.Minimal,
            SamplingRate = 0.01,
            EnableSampling = true,
            IncludeStackTraces = false,
            MaxContextEntries = 20,
            MaxStringValueLength = 500,
            MaxChildSpans = 5,
            IncludeChildSpanContext = false,
        };
    }
}
