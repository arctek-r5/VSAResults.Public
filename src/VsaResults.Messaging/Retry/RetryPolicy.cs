using VsaResults.Errors;

namespace VsaResults.Messaging.Retry;

/// <summary>
/// Fluent factory for creating retry policies.
/// </summary>
public static class RetryPolicy
{
    /// <summary>
    /// Creates an immediate retry policy with no delay between attempts.
    /// </summary>
    /// <param name="maxRetries">The maximum number of retries.</param>
    /// <returns>A retry policy.</returns>
    public static IRetryPolicy Immediate(int maxRetries) =>
        new ImmediateRetryPolicy(maxRetries);

    /// <summary>
    /// Creates an interval retry policy with fixed delay between attempts.
    /// </summary>
    /// <param name="maxRetries">The maximum number of retries.</param>
    /// <param name="interval">The delay between retries.</param>
    /// <returns>A retry policy.</returns>
    public static IRetryPolicy Interval(int maxRetries, TimeSpan interval) =>
        new IntervalRetryPolicy(maxRetries, interval);

    /// <summary>
    /// Creates retry policy with incrementing intervals.
    /// </summary>
    /// <param name="maxRetries">The maximum number of retries.</param>
    /// <param name="initialInterval">The initial delay.</param>
    /// <param name="intervalIncrement">The increment for each retry.</param>
    /// <returns>A retry policy.</returns>
    public static IRetryPolicy Incremental(int maxRetries, TimeSpan initialInterval, TimeSpan intervalIncrement) =>
        new IncrementalRetryPolicy(maxRetries, initialInterval, intervalIncrement);

    /// <summary>
    /// Creates an exponential backoff retry policy.
    /// </summary>
    /// <param name="maxRetries">The maximum number of retries.</param>
    /// <param name="initialInterval">The initial delay.</param>
    /// <param name="maxInterval">The maximum delay (optional, defaults to 5 minutes).</param>
    /// <returns>A retry policy.</returns>
    public static IRetryPolicy Exponential(
        int maxRetries,
        TimeSpan initialInterval,
        TimeSpan? maxInterval = null) =>
        new ExponentialRetryPolicy(maxRetries, initialInterval, maxInterval ?? TimeSpan.FromMinutes(5));

    /// <summary>
    /// Creates an exponential backoff policy with jitter.
    /// </summary>
    /// <param name="maxRetries">The maximum number of retries.</param>
    /// <param name="initialInterval">The initial delay.</param>
    /// <param name="maxInterval">The maximum delay.</param>
    /// <param name="jitterFactor">Jitter factor (0-1), defaults to 0.2.</param>
    /// <returns>A retry policy.</returns>
    public static IRetryPolicy ExponentialWithJitter(
        int maxRetries,
        TimeSpan initialInterval,
        TimeSpan? maxInterval = null,
        double jitterFactor = 0.2) =>
        new ExponentialRetryPolicy(maxRetries, initialInterval, maxInterval ?? TimeSpan.FromMinutes(5), jitterFactor);

    /// <summary>
    /// Wraps a policy to only retry specific error types.
    /// </summary>
    /// <param name="basePolicy">The base policy.</param>
    /// <param name="errorTypes">The error types to retry.</param>
    /// <returns>A filtered retry policy.</returns>
    public static IRetryPolicy ForErrors(IRetryPolicy basePolicy, params ErrorType[] errorTypes) =>
        new FilteredRetryPolicy(basePolicy, errorTypes);

    /// <summary>
    /// Wraps a policy to ignore specific error types.
    /// </summary>
    /// <param name="basePolicy">The base policy.</param>
    /// <param name="errorTypes">The error types to NOT retry.</param>
    /// <returns>A filtered retry policy.</returns>
    public static IRetryPolicy ExceptErrors(IRetryPolicy basePolicy, params ErrorType[] errorTypes) =>
        new ExcludeErrorsRetryPolicy(basePolicy, errorTypes);

    /// <summary>
    /// Creates a policy that never retries.
    /// </summary>
    public static IRetryPolicy None => NoRetryPolicy.Instance;
}

/// <summary>
/// Retry policy with no delay.
/// </summary>
internal sealed class ImmediateRetryPolicy : IRetryPolicy
{
    public int MaxRetries { get; }

    public ImmediateRetryPolicy(int maxRetries)
    {
        MaxRetries = maxRetries;
    }

    public bool ShouldRetry(RetryContext context, IReadOnlyList<Error> errors)
        => context.Attempt < MaxRetries;

    public TimeSpan GetDelay(RetryContext context) => TimeSpan.Zero;
}

/// <summary>
/// Retry policy with fixed interval.
/// </summary>
internal sealed class IntervalRetryPolicy : IRetryPolicy
{
    public int MaxRetries { get; }
    private readonly TimeSpan _interval;

    public IntervalRetryPolicy(int maxRetries, TimeSpan interval)
    {
        MaxRetries = maxRetries;
        _interval = interval;
    }

    public bool ShouldRetry(RetryContext context, IReadOnlyList<Error> errors)
        => context.Attempt < MaxRetries;

    public TimeSpan GetDelay(RetryContext context) => _interval;
}

/// <summary>
/// Retry policy with incrementing intervals.
/// </summary>
internal sealed class IncrementalRetryPolicy : IRetryPolicy
{
    public int MaxRetries { get; }
    private readonly TimeSpan _initialInterval;
    private readonly TimeSpan _intervalIncrement;

    public IncrementalRetryPolicy(int maxRetries, TimeSpan initialInterval, TimeSpan intervalIncrement)
    {
        MaxRetries = maxRetries;
        _initialInterval = initialInterval;
        _intervalIncrement = intervalIncrement;
    }

    public bool ShouldRetry(RetryContext context, IReadOnlyList<Error> errors)
        => context.Attempt < MaxRetries;

    public TimeSpan GetDelay(RetryContext context)
        => _initialInterval + TimeSpan.FromTicks(_intervalIncrement.Ticks * context.Attempt);
}

/// <summary>
/// Retry policy with exponential backoff.
/// </summary>
internal sealed class ExponentialRetryPolicy : IRetryPolicy
{
    // ReSharper disable once InconsistentNaming — static readonly field intentionally prefixed with underscore
    private static readonly Random _random = new();

    public int MaxRetries { get; }
    private readonly TimeSpan _initialInterval;
    private readonly TimeSpan _maxInterval;
    private readonly double _jitterFactor;

    public ExponentialRetryPolicy(
        int maxRetries,
        TimeSpan initialInterval,
        TimeSpan maxInterval,
        double jitterFactor = 0)
    {
        MaxRetries = maxRetries;
        _initialInterval = initialInterval;
        _maxInterval = maxInterval;
        _jitterFactor = Math.Clamp(jitterFactor, 0, 1);
    }

    public bool ShouldRetry(RetryContext context, IReadOnlyList<Error> errors)
        => context.Attempt < MaxRetries;

    public TimeSpan GetDelay(RetryContext context)
    {
        var exponentialDelay = TimeSpan.FromTicks(
            _initialInterval.Ticks * (long)Math.Pow(2, context.Attempt));

        var delay = exponentialDelay > _maxInterval ? _maxInterval : exponentialDelay;

        if (_jitterFactor > 0)
        {
            var jitter = delay.TotalMilliseconds * _jitterFactor;
            var randomJitter = (_random.NextDouble() * 2 - 1) * jitter;
            delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds + randomJitter);
        }

        return delay;
    }
}

/// <summary>
/// Retry policy that filters by error type.
/// </summary>
internal sealed class FilteredRetryPolicy : IRetryPolicy
{
    private readonly IRetryPolicy _basePolicy;
    private readonly HashSet<ErrorType> _errorTypes;

    public int MaxRetries => _basePolicy.MaxRetries;

    public FilteredRetryPolicy(IRetryPolicy basePolicy, ErrorType[] errorTypes)
    {
        _basePolicy = basePolicy;
        _errorTypes = [.. errorTypes];
    }

    public bool ShouldRetry(RetryContext context, IReadOnlyList<Error> errors)
    {
        if (errors.Count == 0)
        {
            return false;
        }

        // Only retry if at least one error is in the allowed types
        var hasRetryableError = errors.Any(e => _errorTypes.Contains(e.Type));
        return hasRetryableError && _basePolicy.ShouldRetry(context, errors);
    }

    public TimeSpan GetDelay(RetryContext context) => _basePolicy.GetDelay(context);
}

/// <summary>
/// Retry policy that excludes certain error types.
/// </summary>
internal sealed class ExcludeErrorsRetryPolicy : IRetryPolicy
{
    private readonly IRetryPolicy _basePolicy;
    private readonly HashSet<ErrorType> _excludedTypes;

    public int MaxRetries => _basePolicy.MaxRetries;

    public ExcludeErrorsRetryPolicy(IRetryPolicy basePolicy, ErrorType[] excludedTypes)
    {
        _basePolicy = basePolicy;
        _excludedTypes = [.. excludedTypes];
    }

    public bool ShouldRetry(RetryContext context, IReadOnlyList<Error> errors)
    {
        if (errors.Count == 0)
        {
            return false;
        }

        // Don't retry if any error is in the excluded types
        var hasExcludedError = errors.Any(e => _excludedTypes.Contains(e.Type));
        return !hasExcludedError && _basePolicy.ShouldRetry(context, errors);
    }

    public TimeSpan GetDelay(RetryContext context) => _basePolicy.GetDelay(context);
}

/// <summary>
/// Retry policy that never retries.
/// </summary>
internal sealed class NoRetryPolicy : IRetryPolicy
{
    public static NoRetryPolicy Instance { get; } = new();

    private NoRetryPolicy()
    {
    }

    public int MaxRetries => 0;

    public bool ShouldRetry(RetryContext context, IReadOnlyList<Error> errors) => false;

    public TimeSpan GetDelay(RetryContext context) => TimeSpan.Zero;
}
