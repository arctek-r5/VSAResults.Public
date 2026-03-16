using VsaResults.Messaging.Sagas;
using VsaResults.Messaging.StateMachine;

namespace VsaResults.Messaging.DependencyInjection;

/// <summary>
/// Configurator for setting up a saga with its state machine and repository.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
public interface ISagaConfigurator<TState>
    where TState : class, ISagaState, new()
{
    /// <summary>
    /// Configures the state machine for this saga.
    /// </summary>
    /// <param name="configure">A function that builds the state machine using a <see cref="StateMachineBuilder{TState}"/>.</param>
    /// <returns>The configurator for chaining.</returns>
    ISagaConfigurator<TState> UseStateMachine(Func<StateMachineBuilder<TState>, IStateMachine<TState>> configure);

    /// <summary>
    /// Configures a custom saga repository implementation.
    /// </summary>
    /// <typeparam name="TRepo">The repository type.</typeparam>
    /// <returns>The configurator for chaining.</returns>
    ISagaConfigurator<TState> UseRepository<TRepo>()
        where TRepo : class, ISagaRepository<TState>;

    /// <summary>
    /// Configures the in-memory saga repository. Suitable for testing and single-instance deployments.
    /// </summary>
    /// <returns>The configurator for chaining.</returns>
    ISagaConfigurator<TState> UseInMemoryRepository();
}
