using VsaResults.Errors;

namespace VsaResults.Messaging.Consumers;

/// <summary>
/// Exception thrown when a consumer returns a VsaResult with errors.
/// This ensures the message is NACKed (and dead-lettered) rather than silently ACKed,
/// and the error is visible in wide events and logs.
/// </summary>
public sealed class ConsumerErrorException : Exception
{
    public ConsumerErrorException(string message, IReadOnlyList<Error> errors)
        : base(message)
    {
        Errors = errors;
    }

    /// <summary>
    /// Gets the errors returned by the consumer.
    /// </summary>
    public IReadOnlyList<Error> Errors { get; }
}
