using System.Collections.Concurrent;
using System.Text.Json;
using Dapper;
using Npgsql;
using VsaResults.Features.Features;
using VsaResults.Messaging.Sagas;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.PostgreSql;

/// <summary>
/// PostgreSQL-backed saga repository using Dapper.
/// Supports optimistic concurrency via a version column.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
public sealed class PostgreSqlSagaRepository<TState> : ISagaRepository<TState>
    where TState : class, ISagaState, new()
{
    private readonly string _connectionString;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly string _sagaType = typeof(TState).Name;

    // Track versions internally — ISagaState doesn't have a version property
    private static readonly ConcurrentDictionary<Guid, int> VersionCache = new();

    public PostgreSqlSagaRepository(string connectionString, JsonSerializerOptions? serializerOptions = null)
    {
        _connectionString = connectionString;
        _serializerOptions = serializerOptions ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <inheritdoc />
    public async Task<VsaResult<TState>> GetAsync(Guid correlationId, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);

        var row = await connection.QuerySingleOrDefaultAsync<SagaRow>(
            SagaStateSql.Get,
            new { CorrelationId = correlationId, SagaType = _sagaType });

        if (row is null)
        {
            return Messaging.ErrorOr.MessagingErrors.SagaNotFound(
                Messaging.Messages.CorrelationId.From(correlationId));
        }

        var state = JsonSerializer.Deserialize<TState>(row.StateData, _serializerOptions);
        if (state is null)
        {
            return Messaging.ErrorOr.MessagingErrors.DeserializationFailed(
                typeof(TState).Name, "Failed to deserialize saga state from JSON");
        }

        VersionCache[correlationId] = row.Version;
        VsaResult<TState> result = state;
        return result;
    }

    /// <inheritdoc />
    public async Task<VsaResult<Unit>> SaveAsync(TState state, CancellationToken ct = default)
    {
        var stateData = JsonSerializer.Serialize(state, _serializerOptions);
        await using var connection = new NpgsqlConnection(_connectionString);

        if (VersionCache.TryGetValue(state.CorrelationId, out var expectedVersion))
        {
            // Update existing
            var rowsAffected = await connection.ExecuteAsync(
                SagaStateSql.Update,
                new
                {
                    CorrelationId = state.CorrelationId,
                    SagaType = _sagaType,
                    CurrentState = state.CurrentState,
                    StateData = stateData,
                    ExpectedVersion = expectedVersion,
                    Now = DateTimeOffset.UtcNow
                });

            if (rowsAffected == 0)
            {
                throw new SagaConcurrencyException(state.CorrelationId);
            }

            VersionCache[state.CorrelationId] = expectedVersion + 1;
        }
        else
        {
            // Insert new
            await connection.ExecuteAsync(
                SagaStateSql.Insert,
                new
                {
                    CorrelationId = state.CorrelationId,
                    SagaType = _sagaType,
                    CurrentState = state.CurrentState,
                    StateData = stateData,
                    Now = DateTimeOffset.UtcNow
                });

            VersionCache[state.CorrelationId] = 1;
        }

        VsaResult<Unit> result = Unit.Value;
        return result;
    }

    /// <inheritdoc />
    public async Task<VsaResult<Unit>> DeleteAsync(Guid correlationId, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);

        await connection.ExecuteAsync(
            SagaStateSql.Delete,
            new { CorrelationId = correlationId, SagaType = _sagaType });

        VersionCache.TryRemove(correlationId, out _);

        VsaResult<Unit> result = Unit.Value;
        return result;
    }

    /// <inheritdoc />
    public async Task<VsaResult<IReadOnlyList<TState>>> QueryByStateAsync(string stateName, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);

        var rows = await connection.QueryAsync<string>(
            SagaStateSql.QueryByState,
            new { SagaType = _sagaType, StateName = stateName });

        var states = rows
            .Select(json => JsonSerializer.Deserialize<TState>(json, _serializerOptions)!)
            .ToList();

        VsaResult<IReadOnlyList<TState>> result = states;
        return result;
    }

    /// <summary>
    /// Ensures the saga_state table exists. Call once at startup.
    /// </summary>
    public async Task EnsureTableAsync(CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.ExecuteAsync(SagaStateSql.EnsureTable);
    }

    private sealed record SagaRow(string StateData, int Version);
}
