using VsaResults.Features.Features;
using VsaResults.Messaging.ErrorOr;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Pipeline.Filters;

/// <summary>
/// Timeout filter for message processing.
/// Cancels processing if it exceeds the specified duration.
/// </summary>
/// <typeparam name="TContext">The context type.</typeparam>
public sealed class TimeoutFilter<TContext> : IFilter<TContext>
    where TContext : PipeContext
{
    private readonly TimeSpan _timeout;

    /// <summary>
    /// Creates a new timeout filter.
    /// </summary>
    /// <param name="timeout">The timeout duration.</param>
    public TimeoutFilter(TimeSpan timeout)
    {
        _timeout = timeout;
    }

    /// <inheritdoc />
    public async Task<VsaResult<Unit>> SendAsync(
        TContext context,
        IPipe<TContext> next,
        CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_timeout);

        try
        {
            return await next.SendAsync(context, cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return MessagingErrors.Timeout(_timeout);
        }
    }

    /// <inheritdoc />
    public void Probe(ProbeContext context) =>
        context.Add("TimeoutFilter", new Dictionary<string, object>
        {
            ["Timeout"] = _timeout.ToString(),
            ["TimeoutMs"] = _timeout.TotalMilliseconds
        });
}

/// <summary>
/// Extension methods for adding timeout to pipelines.
/// </summary>
public static class TimeoutFilterExtensions
{
    /// <summary>
    /// Adds timeout protection to the pipeline.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="builder">The pipe builder.</param>
    /// <param name="timeout">The timeout duration.</param>
    /// <returns>The builder for chaining.</returns>
    public static PipeBuilder<TContext> UseTimeout<TContext>(
        this PipeBuilder<TContext> builder,
        TimeSpan timeout)
        where TContext : PipeContext
    {
        return builder.UseFilter(new TimeoutFilter<TContext>(timeout));
    }

    /// <summary>
    /// Adds timeout protection to the pipeline.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="builder">The pipe builder.</param>
    /// <param name="timeoutSeconds">The timeout in seconds.</param>
    /// <returns>The builder for chaining.</returns>
    public static PipeBuilder<TContext> UseTimeout<TContext>(
        this PipeBuilder<TContext> builder,
        int timeoutSeconds)
        where TContext : PipeContext
    {
        return builder.UseTimeout(TimeSpan.FromSeconds(timeoutSeconds));
    }
}
