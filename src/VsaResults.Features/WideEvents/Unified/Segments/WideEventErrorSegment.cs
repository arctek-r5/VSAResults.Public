using VsaResults.Errors;

namespace VsaResults.Features.WideEvents.Unified.Segments;

/// <summary>
/// Error information segment for wide events.
/// Populated when the operation fails with business errors or exceptions.
/// Implemented as a record to enable safe, shallow cloning via <c>with {}</c>.
/// </summary>
public sealed record WideEventErrorSegment
{
    /// <summary>Gets or sets the first error code.</summary>
    public string? Code { get; set; }

    /// <summary>Gets or sets the first error type (e.g., Validation, NotFound, Unauthorized).</summary>
    public string? Type { get; set; }

    /// <summary>Gets or sets the first error message/description.</summary>
    public string? Message { get; set; }

    /// <summary>Gets or sets all error descriptions joined (when multiple errors).</summary>
    public string? AllDescriptions { get; set; }

    /// <summary>Gets or sets the total number of errors.</summary>
    public int Count { get; set; }

    /// <summary>Gets or sets which pipeline stage failed.</summary>
    public string? FailedAtStage { get; set; }

    /// <summary>Gets or sets the namespace of the component that failed.</summary>
    public string? FailedInNamespace { get; set; }

    /// <summary>Gets or sets the class name of the component that failed.</summary>
    public string? FailedInClass { get; set; }

    /// <summary>Gets or sets the method name that was executing when failure occurred.</summary>
    public string? FailedInMethod { get; set; }

    /// <summary>Gets or sets the exception type name (if an exception was thrown).</summary>
    public string? ExceptionType { get; set; }

    /// <summary>Gets or sets the exception message.</summary>
    public string? ExceptionMessage { get; set; }

    /// <summary>Gets or sets the exception stack trace.</summary>
    /// <remarks>
    /// Only populated when <see cref="WideEventOptions.IncludeStackTraces"/> is true
    /// or when <see cref="WideEventVerbosity.Verbose"/> is enabled.
    /// </remarks>
    public string? ExceptionStackTrace { get; set; }

    /// <summary>
    /// Creates an error segment from a list of VsaResult errors.
    /// </summary>
    /// <param name="errors">The errors to capture.</param>
    /// <param name="failedAtStage">The stage where failure occurred.</param>
    /// <returns>A populated error segment.</returns>
    public static WideEventErrorSegment FromErrors(IReadOnlyList<Error> errors, string? failedAtStage = null)
    {
        var segment = new WideEventErrorSegment
        {
            FailedAtStage = failedAtStage,
            Count = errors.Count,
        };

        if (errors.Count == 0)
        {
            return segment;
        }

        var firstError = errors[0];
        segment.Code = firstError.Code;
        segment.Type = firstError.Type.ToString();
        segment.Message = firstError.Description;

        if (errors.Count > 1)
        {
            segment.AllDescriptions = string.Join("; ", errors.Select(e => $"{e.Code}: {e.Description}"));
        }

        return segment;
    }

    /// <summary>
    /// Creates an error segment from an exception.
    /// </summary>
    /// <param name="exception">The exception to capture.</param>
    /// <param name="failedAtStage">The stage where the exception occurred.</param>
    /// <param name="includeStackTrace">Whether to include the stack trace.</param>
    /// <returns>A populated error segment.</returns>
    public static WideEventErrorSegment FromException(Exception exception, string? failedAtStage = null, bool includeStackTrace = false)
    {
        return new WideEventErrorSegment
        {
            FailedAtStage = failedAtStage,
            ExceptionType = exception.GetType().FullName,
            ExceptionMessage = exception.Message,
            ExceptionStackTrace = includeStackTrace ? exception.StackTrace : null,
            Count = 1,
        };
    }
}
