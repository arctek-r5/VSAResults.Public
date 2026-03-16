using VsaResults.Errors;
using VsaResults.Features.Features;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Pipeline.Filters;

/// <summary>
/// Filter that converts unhandled exceptions to VsaResult errors.
/// Ensures the pipeline returns errors instead of throwing exceptions.
/// </summary>
/// <typeparam name="TContext">The context type.</typeparam>
public sealed class ExceptionFilter<TContext> : IFilter<TContext>
    where TContext : PipeContext
{
    private readonly bool _includeStackTrace;

    /// <summary>
    /// Creates a new exception filter.
    /// </summary>
    /// <param name="includeStackTrace">Whether to include stack traces in error metadata.</param>
    public ExceptionFilter(bool includeStackTrace = true)
    {
        _includeStackTrace = includeStackTrace;
    }

    /// <inheritdoc />
    public async Task<VsaResult<Unit>> SendAsync(
        TContext context,
        IPipe<TContext> next,
        CancellationToken ct = default)
    {
        try
        {
            return await next.SendAsync(context, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Re-throw cancellation if it was requested
            throw;
        }
        catch (Exception ex)
        {
            return CreateErrorFromException(ex);
        }
    }

    /// <inheritdoc />
    public void Probe(ProbeContext context) =>
        context.Add("ExceptionFilter", new Dictionary<string, object>
        {
            ["IncludeStackTrace"] = _includeStackTrace
        });

    private Error CreateErrorFromException(Exception ex)
    {
        var metadata = new Dictionary<string, object>
        {
            ["ExceptionType"] = ex.GetType().FullName ?? ex.GetType().Name,
            ["ExceptionMessage"] = ex.Message
        };

        if (_includeStackTrace && !string.IsNullOrEmpty(ex.StackTrace))
        {
            metadata["StackTrace"] = ex.StackTrace;
        }

        if (ex.InnerException is not null)
        {
            metadata["InnerExceptionType"] = ex.InnerException.GetType().FullName ?? ex.InnerException.GetType().Name;
            metadata["InnerExceptionMessage"] = ex.InnerException.Message;
        }

        return Error.Unexpected(
            $"Pipeline.Exception.{ex.GetType().Name}",
            ex.Message,
            metadata);
    }
}

/// <summary>
/// Extension methods for adding exception handling to pipelines.
/// </summary>
public static class ExceptionFilterExtensions
{
    /// <summary>
    /// Adds exception handling to the pipeline.
    /// Converts unhandled exceptions to errors.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="builder">The pipe builder.</param>
    /// <param name="includeStackTrace">Whether to include stack traces.</param>
    /// <returns>The builder for chaining.</returns>
    public static PipeBuilder<TContext> UseExceptionHandling<TContext>(
        this PipeBuilder<TContext> builder,
        bool includeStackTrace = true)
        where TContext : PipeContext
    {
        return builder.UseFilter(new ExceptionFilter<TContext>(includeStackTrace));
    }
}
