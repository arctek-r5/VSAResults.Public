namespace VsaResults.Messaging.Faults;

/// <summary>
/// Serializable exception information for fault messages.
/// Captures exception details without holding references to the actual exception.
/// </summary>
public sealed record ExceptionInfo
{
    /// <summary>Gets the full type name of the exception.</summary>
    public required string ExceptionType { get; init; }

    /// <summary>Gets the exception message.</summary>
    public required string Message { get; init; }

    /// <summary>Gets the stack trace if available.</summary>
    public string? StackTrace { get; init; }

    /// <summary>Gets the source of the exception.</summary>
    public string? Source { get; init; }

    /// <summary>Gets the inner exception information.</summary>
    public ExceptionInfo? InnerException { get; init; }

    /// <summary>Gets additional exception data.</summary>
    public Dictionary<string, object?>? Data { get; init; }

    /// <summary>
    /// Creates exception info from an exception.
    /// </summary>
    /// <param name="exception">The exception to capture.</param>
    /// <param name="includeStackTrace">Whether to include the stack trace.</param>
    /// <returns>The exception info.</returns>
    public static ExceptionInfo From(Exception exception, bool includeStackTrace = true)
    {
        Dictionary<string, object?>? data = null;

        if (exception.Data.Count > 0)
        {
            data = [];
            foreach (var key in exception.Data.Keys)
            {
                var keyString = key?.ToString();
                if (keyString is not null)
                {
                    data[keyString] = exception.Data[key!];
                }
            }
        }

        return new ExceptionInfo
        {
            ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
            Message = exception.Message,
            StackTrace = includeStackTrace ? exception.StackTrace : null,
            Source = exception.Source,
            InnerException = exception.InnerException is not null
                ? From(exception.InnerException, includeStackTrace)
                : null,
            Data = data
        };
    }

    /// <summary>
    /// Formats the exception info as a string for logging.
    /// </summary>
    /// <returns>A formatted string representation.</returns>
    public string ToFormattedString()
    {
        var lines = new List<string>
        {
            $"{ExceptionType}: {Message}"
        };

        if (!string.IsNullOrEmpty(StackTrace))
        {
            lines.Add(StackTrace);
        }

        if (InnerException is not null)
        {
            lines.Add("--- Inner Exception ---");
            lines.Add(InnerException.ToFormattedString());
        }

        return string.Join(Environment.NewLine, lines);
    }
}
