using VsaResults.Features.Features;
using VsaResults.Messaging.ErrorOr;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Pipeline.Filters;

/// <summary>
/// Circuit breaker filter to prevent cascading failures.
/// When failures exceed a threshold, the circuit opens and rejects messages.
/// </summary>
/// <typeparam name="TContext">The context type.</typeparam>
public sealed class CircuitBreakerFilter<TContext> : IFilter<TContext>
    where TContext : PipeContext
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _openDuration;
    private readonly TimeSpan _halfOpenTestInterval;
    private readonly Lock _lock = new();

    private int _failureCount;
    private DateTimeOffset _lastFailure;
    private DateTimeOffset _lastHalfOpenTest;
    private CircuitState _state = CircuitState.Closed;

    /// <summary>
    /// Creates a new circuit breaker filter.
    /// </summary>
    /// <param name="failureThreshold">Number of failures before opening.</param>
    /// <param name="openDuration">Duration to remain open before half-open.</param>
    /// <param name="halfOpenTestInterval">Interval between half-open tests.</param>
    public CircuitBreakerFilter(
        int failureThreshold = 5,
        TimeSpan? openDuration = null,
        TimeSpan? halfOpenTestInterval = null)
    {
        _failureThreshold = failureThreshold;
        _openDuration = openDuration ?? TimeSpan.FromSeconds(30);
        _halfOpenTestInterval = halfOpenTestInterval ?? TimeSpan.FromSeconds(5);
    }

    /// <summary>Gets the current circuit state.</summary>
    public CircuitState State
    {
        get
        {
            lock (_lock)
            {
                return _state;
            }
        }
    }

    /// <summary>Gets the current failure count.</summary>
    public int FailureCount
    {
        get
        {
            lock (_lock)
            {
                return _failureCount;
            }
        }
    }

    /// <inheritdoc />
    public async Task<VsaResult<Unit>> SendAsync(
        TContext context,
        IPipe<TContext> next,
        CancellationToken ct = default)
    {
        var currentState = GetCurrentState();

        switch (currentState)
        {
            case CircuitState.Open:
                return MessagingErrors.CircuitBreakerOpen(_failureCount, GetRemainingOpenTime());

            case CircuitState.HalfOpen:
                // In half-open state, only allow one test request at a time
                if (!TryStartHalfOpenTest())
                {
                    return MessagingErrors.CircuitBreakerOpen(_failureCount, GetRemainingOpenTime());
                }

                break;
        }

        var result = await next.SendAsync(context, ct);

        RecordResult(result.IsError);

        return result;
    }

    /// <inheritdoc />
    public void Probe(ProbeContext context) =>
        context.Add("CircuitBreakerFilter", new Dictionary<string, object>
        {
            ["State"] = _state.ToString(),
            ["FailureThreshold"] = _failureThreshold,
            ["FailureCount"] = _failureCount,
            ["OpenDuration"] = _openDuration.ToString()
        });

    /// <summary>
    /// Resets the circuit breaker to closed state.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _state = CircuitState.Closed;
            _failureCount = 0;
        }
    }

    private CircuitState GetCurrentState()
    {
        lock (_lock)
        {
            if (_state == CircuitState.Open && DateTimeOffset.UtcNow - _lastFailure >= _openDuration)
            {
                _state = CircuitState.HalfOpen;
            }

            return _state;
        }
    }

    private bool TryStartHalfOpenTest()
    {
        lock (_lock)
        {
            if (_state != CircuitState.HalfOpen)
            {
                return false;
            }

            if (DateTimeOffset.UtcNow - _lastHalfOpenTest < _halfOpenTestInterval)
            {
                return false;
            }

            _lastHalfOpenTest = DateTimeOffset.UtcNow;
            return true;
        }
    }

    private void RecordResult(bool isError)
    {
        lock (_lock)
        {
            if (isError)
            {
                _failureCount++;
                _lastFailure = DateTimeOffset.UtcNow;

                if (_failureCount >= _failureThreshold)
                {
                    _state = CircuitState.Open;
                }
            }
            else if (_state == CircuitState.HalfOpen)
            {
                // Success in half-open state closes the circuit
                _state = CircuitState.Closed;
                _failureCount = 0;
            }
            else if (_state == CircuitState.Closed && _failureCount > 0)
            {
                _failureCount--;
            }
        }
    }

    private TimeSpan GetRemainingOpenTime()
    {
        lock (_lock)
        {
            var elapsed = DateTimeOffset.UtcNow - _lastFailure;
            var remaining = _openDuration - elapsed;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }
}

/// <summary>
/// Circuit breaker states.
/// </summary>
public enum CircuitState
{
    /// <summary>Circuit is closed, requests flow normally.</summary>
    Closed,

    /// <summary>Circuit is open, requests are rejected.</summary>
    Open,

    /// <summary>Circuit is testing if it can close.</summary>
    HalfOpen
}

/// <summary>
/// Extension methods for adding circuit breaker to pipelines.
/// </summary>
public static class CircuitBreakerFilterExtensions
{
    /// <summary>
    /// Adds circuit breaker protection to the pipeline.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="builder">The pipe builder.</param>
    /// <param name="failureThreshold">Failures before opening.</param>
    /// <param name="openDuration">Duration to remain open.</param>
    /// <returns>The builder for chaining.</returns>
    public static PipeBuilder<TContext> UseCircuitBreaker<TContext>(
        this PipeBuilder<TContext> builder,
        int failureThreshold = 5,
        TimeSpan? openDuration = null)
        where TContext : PipeContext
    {
        return builder.UseFilter(new CircuitBreakerFilter<TContext>(failureThreshold, openDuration));
    }
}
