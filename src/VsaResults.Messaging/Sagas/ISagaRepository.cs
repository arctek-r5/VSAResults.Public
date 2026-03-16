using VsaResults.Features.Features;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Sagas;

/// <summary>
/// Repository interface for persisting and retrieving saga state.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
public interface ISagaRepository<TState>
    where TState : class, ISagaState, new()
{
    /// <summary>
    /// Gets a saga state by its correlation ID.
    /// </summary>
    /// <param name="correlationId">The correlation ID to find.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The saga state if found, or a <see cref="ErrorOr.MessagingErrors.SagaNotFound"/> error.</returns>
    Task<VsaResult<TState>> GetAsync(Guid correlationId, CancellationToken ct = default);

    /// <summary>
    /// Saves a saga state.
    /// Creates a new state if it doesn't exist, or updates an existing one.
    /// </summary>
    /// <param name="state">The saga state to save.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Unit on success, or errors on failure.</returns>
    Task<VsaResult<Unit>> SaveAsync(TState state, CancellationToken ct = default);

    /// <summary>
    /// Deletes a saga state by its correlation ID.
    /// </summary>
    /// <param name="correlationId">The correlation ID of the saga to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Unit on success, or errors on failure.</returns>
    Task<VsaResult<Unit>> DeleteAsync(Guid correlationId, CancellationToken ct = default);

    /// <summary>
    /// Queries saga states by their current state.
    /// </summary>
    /// <param name="stateName">The state name to filter by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A collection of matching saga states.</returns>
    Task<VsaResult<IReadOnlyList<TState>>> QueryByStateAsync(string stateName, CancellationToken ct = default);
}
