using VsaResults.Errors;

namespace VsaResults.Messaging.Faults;

/// <summary>
/// Contains detailed information about a message processing fault.
/// </summary>
public sealed record FaultContext
{
    /// <summary>Gets the errors that caused the fault.</summary>
    public required IReadOnlyList<Error> Errors { get; init; }

    /// <summary>Gets the first error code.</summary>
    public string ErrorCode => Errors.Count > 0 ? Errors[0].Code : "Unknown";

    /// <summary>Gets the first error type.</summary>
    public ErrorType ErrorType => Errors.Count > 0 ? Errors[0].Type : ErrorType.Unexpected;

    /// <summary>Gets the first error description.</summary>
    public string ErrorMessage => Errors.Count > 0
        ? Errors[0].Description
        : "An unknown error occurred.";

    /// <summary>Gets the total number of errors.</summary>
    public int ErrorCount => Errors.Count;

    /// <summary>Gets the exception information if the fault was caused by an exception.</summary>
    public ExceptionInfo? Exception { get; init; }

    /// <summary>Gets the consumer type that faulted.</summary>
    public string? ConsumerType { get; init; }

    /// <summary>Gets the timestamp of the fault.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Creates a fault context from errors.
    /// </summary>
    /// <param name="errors">The errors that caused the fault.</param>
    /// <param name="consumerType">The consumer type name.</param>
    /// <returns>A new fault context.</returns>
    public static FaultContext FromErrors(IReadOnlyList<Error> errors, string? consumerType = null)
    {
        return new FaultContext
        {
            Errors = errors,
            ConsumerType = consumerType
        };
    }

    /// <summary>
    /// Creates a fault context from an exception.
    /// </summary>
    /// <param name="exception">The exception that caused the fault.</param>
    /// <param name="consumerType">The consumer type name.</param>
    /// <returns>A new fault context.</returns>
    public static FaultContext FromException(Exception exception, string? consumerType = null)
    {
        return new FaultContext
        {
            Errors = new[] { Error.Unexpected("Consumer.Exception", exception.Message) },
            Exception = ExceptionInfo.From(exception),
            ConsumerType = consumerType
        };
    }

    /// <summary>
    /// Creates a fault context from a single error.
    /// </summary>
    /// <param name="error">The error that caused the fault.</param>
    /// <param name="consumerType">The consumer type name.</param>
    /// <returns>A new fault context.</returns>
    public static FaultContext FromError(Error error, string? consumerType = null)
    {
        return new FaultContext
        {
            Errors = new[] { error },
            ConsumerType = consumerType
        };
    }
}
