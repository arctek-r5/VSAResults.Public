namespace VsaResults.Messaging.Sagas;

/// <summary>
/// Base interface for saga state.
/// All saga state classes must implement this interface.
/// </summary>
public interface ISagaState
{
    /// <summary>
    /// Gets or sets the unique correlation identifier for this saga instance.
    /// This is used to correlate messages to the correct saga instance.
    /// </summary>
    Guid CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the current state name of the saga.
    /// </summary>
    string CurrentState { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the saga was created.
    /// </summary>
    DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the saga was last modified.
    /// </summary>
    DateTimeOffset ModifiedAt { get; set; }

    /// <summary>
    /// Gets or sets the state the saga was in when it faulted.
    /// Captured automatically by <see cref="SagaContext{TState}.SetFaulted"/>
    /// so retry handlers know which step to re-enter.
    /// </summary>
    string? FaultedAtState { get; set; }
}

/// <summary>
/// Base class for saga state providing common properties.
/// </summary>
public abstract class SagaStateBase : ISagaState
{
    /// <inheritdoc />
    public Guid CorrelationId { get; set; }

    /// <inheritdoc />
    public string CurrentState { get; set; } = "Initial";

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public string? FaultedAtState { get; set; }
}
