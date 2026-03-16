using VsaResults.Features.Features;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Pipeline;

/// <summary>
/// Filter in the message processing pipeline.
/// Filters are composable middleware components that can intercept, modify,
/// or handle messages before and after processing.
/// </summary>
/// <typeparam name="TContext">The context type being filtered.</typeparam>
/// <remarks>
/// <para>
/// The filter pattern follows MassTransit's middleware design:
/// </para>
/// <list type="bullet">
/// <item><description>Each filter has complete control over message processing</description></item>
/// <item><description>Filters can short-circuit the pipeline by not calling next</description></item>
/// <item><description>Filters can wrap processing with try/catch, timing, etc.</description></item>
/// <item><description>Filters compose naturally for cross-cutting concerns</description></item>
/// </list>
/// <para>
/// Example:
/// <code>
/// public class LoggingFilter&lt;TContext&gt; : IFilter&lt;TContext&gt;
///     where TContext : PipeContext
/// {
///     public async Task&lt;ErrorOr&lt;Unit&gt;&gt; SendAsync(
///         TContext context,
///         IPipe&lt;TContext&gt; next,
///         CancellationToken ct = default)
///     {
///         _logger.LogInformation("Processing message...");
///         var result = await next.SendAsync(context, ct);
///         _logger.LogInformation("Processing complete");
///         return result;
///     }
///
///     public void Probe(ProbeContext context) =>
///         context.Add("LoggingFilter");
/// }
/// </code>
/// </para>
/// </remarks>
public interface IFilter<TContext>
    where TContext : PipeContext
{
    /// <summary>
    /// Processes the context through this filter.
    /// </summary>
    /// <param name="context">The context to process.</param>
    /// <param name="next">The next pipe in the chain.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Unit on success, or errors on failure.</returns>
    Task<VsaResult<Unit>> SendAsync(
        TContext context,
        IPipe<TContext> next,
        CancellationToken ct = default);

    /// <summary>
    /// Probes this filter for diagnostic information.
    /// Called during pipeline introspection.
    /// </summary>
    /// <param name="context">The probe context to add information to.</param>
    void Probe(ProbeContext context);
}

/// <summary>
/// Filter that can transform the context type.
/// </summary>
/// <typeparam name="TInput">The input context type.</typeparam>
/// <typeparam name="TOutput">The output context type.</typeparam>
public interface IFilter<TInput, TOutput>
    where TInput : PipeContext
    where TOutput : PipeContext
{
    /// <summary>
    /// Processes the context through this filter with type transformation.
    /// </summary>
    /// <param name="context">The input context.</param>
    /// <param name="next">The next pipe expecting the output context type.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Unit on success, or errors on failure.</returns>
    Task<VsaResult<Unit>> SendAsync(
        TInput context,
        IPipe<TOutput> next,
        CancellationToken ct = default);

    /// <summary>
    /// Probes this filter for diagnostic information.
    /// </summary>
    /// <param name="context">The probe context.</param>
    void Probe(ProbeContext context);
}
