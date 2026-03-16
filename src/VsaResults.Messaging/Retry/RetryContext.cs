using VsaResults.Errors;

namespace VsaResults.Messaging.Retry;

/// <summary>
/// Tracks retry state during message processing.
/// </summary>
public sealed record RetryContext
{
    /// <summary>Gets the current retry attempt (0-based, 0 = first attempt).</summary>
    public int Attempt { get; init; }

    /// <summary>Gets when retries started.</summary>
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Gets the elapsed time since the first attempt.</summary>
    public TimeSpan Elapsed => DateTimeOffset.UtcNow - StartedAt;

    /// <summary>Gets the errors from previous attempts.</summary>
    public IReadOnlyList<Error> PreviousErrors { get; init; } = Array.Empty<Error>();

    /// <summary>Gets the last errors.</summary>
    public IReadOnlyList<Error>? LastErrors { get; init; }

    /// <summary>
    /// Creates the next retry context after an attempt.
    /// </summary>
    /// <param name="errors">The errors from this attempt.</param>
    /// <returns>A new context for the next attempt.</returns>
    public RetryContext NextAttempt(IReadOnlyList<Error> errors)
    {
        var allErrors = new List<Error>(PreviousErrors);
        if (LastErrors is not null)
        {
            allErrors.AddRange(LastErrors);
        }

        return this with
        {
            Attempt = Attempt + 1,
            PreviousErrors = allErrors,
            LastErrors = errors
        };
    }

    /// <summary>
    /// Creates a context for the first attempt.
    /// </summary>
    public static RetryContext Initial => new();
}
