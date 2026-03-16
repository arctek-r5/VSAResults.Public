using System.Collections.Concurrent;
using VsaResults.Features.Features;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Sagas;

/// <summary>
/// In-memory implementation of the saga repository.
/// Suitable for testing and single-instance deployments.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
public sealed class InMemorySagaRepository<TState> : ISagaRepository<TState>
    where TState : class, ISagaState, new()
{
    private readonly ConcurrentDictionary<Guid, TState> _sagas = new();

    /// <inheritdoc />
    public Task<VsaResult<TState>> GetAsync(Guid correlationId, CancellationToken ct = default)
    {
        if (_sagas.TryGetValue(correlationId, out var state))
        {
            VsaResult<TState> result = state;
            return Task.FromResult(result);
        }

        return Task.FromResult<VsaResult<TState>>(
            ErrorOr.MessagingErrors.SagaNotFound(Messages.CorrelationId.From(correlationId)));
    }

    /// <inheritdoc />
    public Task<VsaResult<Unit>> SaveAsync(TState state, CancellationToken ct = default)
    {
        _sagas.AddOrUpdate(
            state.CorrelationId,
            state,
            (_, _) => state);

        VsaResult<Unit> result = Unit.Value;
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<VsaResult<Unit>> DeleteAsync(Guid correlationId, CancellationToken ct = default)
    {
        _sagas.TryRemove(correlationId, out _);
        VsaResult<Unit> result = Unit.Value;
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<VsaResult<IReadOnlyList<TState>>> QueryByStateAsync(string stateName, CancellationToken ct = default)
    {
        var matching = _sagas.Values
            .Where(s => s.CurrentState == stateName)
            .ToList();

        VsaResult<IReadOnlyList<TState>> result = matching;
        return Task.FromResult(result);
    }

    /// <summary>
    /// Gets the total number of saga instances in the repository.
    /// </summary>
    public int Count => _sagas.Count;

    /// <summary>
    /// Clears all saga instances from the repository.
    /// </summary>
    public void Clear() => _sagas.Clear();
}
