using VsaResults.Errors;

namespace VsaResults.Messaging.Retry;

/// <summary>
/// Defines retry behavior for message processing.
/// </summary>
public interface IRetryPolicy
{
    /// <summary>Gets the maximum number of retry attempts.</summary>
    int MaxRetries { get; }

    /// <summary>
    /// Determines whether a retry should be attempted.
    /// </summary>
    /// <param name="context">The current retry context.</param>
    /// <param name="errors">The errors from the last attempt.</param>
    /// <returns>True if retry should be attempted.</returns>
    bool ShouldRetry(RetryContext context, IReadOnlyList<Error> errors);

    /// <summary>
    /// Gets the delay before the next retry attempt.
    /// </summary>
    /// <param name="context">The current retry context.</param>
    /// <returns>The delay duration.</returns>
    TimeSpan GetDelay(RetryContext context);
}
