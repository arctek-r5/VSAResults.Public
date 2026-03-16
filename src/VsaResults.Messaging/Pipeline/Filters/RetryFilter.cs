using VsaResults.Features.Features;
using VsaResults.Messaging.Retry;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Pipeline.Filters;

/// <summary>
/// Retry filter with configurable policy.
/// Retries message processing when errors occur based on the retry policy.
/// </summary>
/// <typeparam name="TContext">The context type.</typeparam>
public sealed class RetryFilter<TContext> : IFilter<TContext>
    where TContext : PipeContext
{
    private readonly IRetryPolicy _policy;

    /// <summary>
    /// Creates a new retry filter.
    /// </summary>
    /// <param name="policy">The retry policy to use.</param>
    public RetryFilter(IRetryPolicy policy)
    {
        _policy = policy;
    }

    /// <inheritdoc />
    public async Task<VsaResult<Unit>> SendAsync(
        TContext context,
        IPipe<TContext> next,
        CancellationToken ct = default)
    {
        var retryContext = RetryContext.Initial;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var result = await next.SendAsync(context, ct);

            if (!result.IsError)
            {
                return result;
            }

            if (!_policy.ShouldRetry(retryContext, result.Errors))
            {
                return result;
            }

            var delay = _policy.GetDelay(retryContext);
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, ct);
            }

            retryContext = retryContext.NextAttempt(result.Errors);

            // Update context with retry information
            context.SetPayload("RetryAttempt", retryContext.Attempt);
        }
    }

    /// <inheritdoc />
    public void Probe(ProbeContext context) =>
        context.Add("RetryFilter", new Dictionary<string, object>
        {
            ["MaxRetries"] = _policy.MaxRetries,
            ["PolicyType"] = _policy.GetType().Name
        });
}

/// <summary>
/// Extension methods for adding retry filter to pipelines.
/// </summary>
public static class RetryFilterExtensions
{
    /// <summary>
    /// Adds retry capability to the pipeline.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="builder">The pipe builder.</param>
    /// <param name="policy">The retry policy.</param>
    /// <returns>The builder for chaining.</returns>
    public static PipeBuilder<TContext> UseRetry<TContext>(
        this PipeBuilder<TContext> builder,
        IRetryPolicy policy)
        where TContext : PipeContext
    {
        return builder.UseFilter(new RetryFilter<TContext>(policy));
    }

    /// <summary>
    /// Adds immediate retry to the pipeline.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="builder">The pipe builder.</param>
    /// <param name="maxRetries">Maximum retry attempts.</param>
    /// <returns>The builder for chaining.</returns>
    public static PipeBuilder<TContext> UseImmediateRetry<TContext>(
        this PipeBuilder<TContext> builder,
        int maxRetries)
        where TContext : PipeContext
    {
        return builder.UseRetry(RetryPolicy.Immediate(maxRetries));
    }

    /// <summary>
    /// Adds exponential backoff retry to the pipeline.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="builder">The pipe builder.</param>
    /// <param name="maxRetries">Maximum retry attempts.</param>
    /// <param name="initialInterval">Initial delay.</param>
    /// <returns>The builder for chaining.</returns>
    public static PipeBuilder<TContext> UseExponentialRetry<TContext>(
        this PipeBuilder<TContext> builder,
        int maxRetries,
        TimeSpan initialInterval)
        where TContext : PipeContext
    {
        return builder.UseRetry(RetryPolicy.Exponential(maxRetries, initialInterval));
    }
}
