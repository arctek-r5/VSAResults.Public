using VsaResults.Messaging.Messages;

namespace VsaResults.Messaging.Sagas;

/// <summary>
/// Command to retry a faulted saga from the step where it failed.
/// The saga's <see cref="ISagaState.FaultedAtState"/> determines which step to re-enter.
/// </summary>
public record RetrySagaCommand : ICommand
{
    public required Guid CorrelationId { get; init; }
}
