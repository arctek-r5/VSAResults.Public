namespace VsaResults.Messaging.Sagas;

/// <summary>
/// Exception thrown when a saga state save fails due to an optimistic concurrency conflict.
/// The saga dispatcher catches this and retries with a reloaded state.
/// </summary>
public sealed class SagaConcurrencyException : Exception
{
    public SagaConcurrencyException(Guid correlationId)
        : base($"Saga '{correlationId}' was modified by another process.")
    {
        CorrelationId = correlationId;
    }

    /// <summary>
    /// Gets the correlation ID of the conflicting saga instance.
    /// </summary>
    public Guid CorrelationId { get; }
}
