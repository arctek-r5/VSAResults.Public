using VsaResults.Features.Features;
using VsaResults.Messaging.ErrorOr;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Pipeline.Filters;

/// <summary>
/// Concurrency limit filter to control parallel message processing.
/// Limits the number of messages being processed simultaneously.
/// </summary>
/// <typeparam name="TContext">The context type.</typeparam>
public sealed class ConcurrencyLimitFilter<TContext> : IFilter<TContext>, IDisposable
    where TContext : PipeContext
{
    private readonly SemaphoreSlim _semaphore;
    private readonly int _limit;
    private readonly TimeSpan? _waitTimeout;

    /// <summary>
    /// Creates a new concurrency limit filter.
    /// </summary>
    /// <param name="limit">Maximum concurrent operations.</param>
    /// <param name="waitTimeout">Optional timeout for acquiring the semaphore.</param>
    public ConcurrencyLimitFilter(int limit, TimeSpan? waitTimeout = null)
    {
        _limit = limit;
        _waitTimeout = waitTimeout;
        _semaphore = new SemaphoreSlim(limit, limit);
    }

    /// <summary>Gets the current number of available slots.</summary>
    public int AvailableCount => _semaphore.CurrentCount;

    /// <summary>Gets the limit.</summary>
    public int Limit => _limit;

    /// <inheritdoc />
    public async Task<VsaResult<Unit>> SendAsync(
        TContext context,
        IPipe<TContext> next,
        CancellationToken ct = default)
    {
        var acquired = _waitTimeout.HasValue
            ? await _semaphore.WaitAsync(_waitTimeout.Value, ct)
            : await WaitAsync(ct);

        if (!acquired)
        {
            return MessagingErrors.Timeout($"Concurrency limit of {_limit} reached. Could not acquire slot within timeout.");
        }

        try
        {
            return await next.SendAsync(context, ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<bool> WaitAsync(CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        return true;
    }

    /// <inheritdoc />
    public void Probe(ProbeContext context) =>
        context.Add("ConcurrencyLimitFilter", new Dictionary<string, object>
        {
            ["Limit"] = _limit,
            ["Available"] = _semaphore.CurrentCount
        });

    /// <inheritdoc />
    public void Dispose() => _semaphore.Dispose();
}

/// <summary>
/// Extension methods for adding concurrency limits to pipelines.
/// </summary>
public static class ConcurrencyLimitFilterExtensions
{
    /// <summary>
    /// Adds concurrency limiting to the pipeline.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="builder">The pipe builder.</param>
    /// <param name="limit">Maximum concurrent operations.</param>
    /// <param name="waitTimeout">Optional timeout for waiting.</param>
    /// <returns>The builder for chaining.</returns>
    public static PipeBuilder<TContext> UseConcurrencyLimit<TContext>(
        this PipeBuilder<TContext> builder,
        int limit,
        TimeSpan? waitTimeout = null)
        where TContext : PipeContext
    {
        return builder.UseFilter(new ConcurrencyLimitFilter<TContext>(limit, waitTimeout));
    }
}
