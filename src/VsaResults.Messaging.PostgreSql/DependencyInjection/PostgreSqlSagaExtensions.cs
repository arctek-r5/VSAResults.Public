using VsaResults.Features.Features;
using VsaResults.Messaging.DependencyInjection;
using VsaResults.Messaging.Sagas;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.PostgreSql.DependencyInjection;

/// <summary>
/// Extension methods for configuring PostgreSQL-backed saga persistence.
/// </summary>
public static class PostgreSqlSagaExtensions
{
    /// <summary>
    /// Configures the saga to use PostgreSQL for state persistence.
    /// </summary>
    /// <typeparam name="TState">The saga state type.</typeparam>
    /// <param name="configurator">The saga configurator.</param>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    /// <returns>The configurator for chaining.</returns>
    public static ISagaConfigurator<TState> UsePostgreSql<TState>(
        this ISagaConfigurator<TState> configurator,
        string connectionString)
        where TState : class, ISagaState, new()
    {
        // Register the factory-created repository
        configurator.UseRepository<PostgreSqlSagaRepositoryWrapper<TState>>();

        // Store the connection string for the wrapper to use
        PostgreSqlSagaConnectionStrings.Set<TState>(connectionString);

        return configurator;
    }
}

/// <summary>
/// Static registry for PostgreSQL connection strings keyed by saga state type.
/// Used to pass connection strings from configuration to DI-resolved repositories.
/// </summary>
internal static class PostgreSqlSagaConnectionStrings
{
    private static readonly Dictionary<Type, string> ConnectionStrings = [];

    internal static void Set<TState>(string connectionString) =>
        ConnectionStrings[typeof(TState)] = connectionString;

    internal static string Get<TState>() =>
        ConnectionStrings.TryGetValue(typeof(TState), out var cs)
            ? cs
            : throw new InvalidOperationException(
                $"No PostgreSQL connection string configured for saga {typeof(TState).Name}. Call UsePostgreSql() first.");
}

/// <summary>
/// DI-resolvable wrapper that creates the PostgreSqlSagaRepository with the correct connection string.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
internal sealed class PostgreSqlSagaRepositoryWrapper<TState> : ISagaRepository<TState>
    where TState : class, ISagaState, new()
{
    private readonly PostgreSqlSagaRepository<TState> _inner;

    public PostgreSqlSagaRepositoryWrapper()
    {
        var connectionString = PostgreSqlSagaConnectionStrings.Get<TState>();
        _inner = new PostgreSqlSagaRepository<TState>(connectionString);
    }

    public Task<VsaResult<TState>> GetAsync(Guid correlationId, CancellationToken ct = default) =>
        _inner.GetAsync(correlationId, ct);

    public Task<VsaResult<Unit>> SaveAsync(TState state, CancellationToken ct = default) =>
        _inner.SaveAsync(state, ct);

    public Task<VsaResult<Unit>> DeleteAsync(Guid correlationId, CancellationToken ct = default) =>
        _inner.DeleteAsync(correlationId, ct);

    public Task<VsaResult<IReadOnlyList<TState>>> QueryByStateAsync(string stateName, CancellationToken ct = default) =>
        _inner.QueryByStateAsync(stateName, ct);
}
