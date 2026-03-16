using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VsaResults.Messaging.Sagas;
using VsaResults.Messaging.StateMachine;

namespace VsaResults.Messaging.DependencyInjection;

/// <summary>
/// Internal implementation of the saga configurator.
/// Collects configuration and applies it during Build.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
internal sealed class SagaConfigurator<TState> : ISagaConfigurator<TState>
    where TState : class, ISagaState, new()
{
    private readonly IServiceCollection _services;
    private IStateMachine<TState>? _stateMachine;
    private Type? _repositoryType;
    private bool _useInMemory;

    public SagaConfigurator(IServiceCollection services)
    {
        _services = services;
    }

    /// <inheritdoc />
    public ISagaConfigurator<TState> UseStateMachine(Func<StateMachineBuilder<TState>, IStateMachine<TState>> configure)
    {
        var builder = new StateMachineBuilder<TState>();
        _stateMachine = configure(builder);
        return this;
    }

    /// <inheritdoc />
    public ISagaConfigurator<TState> UseRepository<TRepo>()
        where TRepo : class, ISagaRepository<TState>
    {
        _repositoryType = typeof(TRepo);
        _useInMemory = false;
        return this;
    }

    /// <inheritdoc />
    public ISagaConfigurator<TState> UseInMemoryRepository()
    {
        _useInMemory = true;
        _repositoryType = null;
        return this;
    }

    /// <summary>
    /// Applies the saga configuration to the service collection.
    /// </summary>
    internal void Build()
    {
        if (_stateMachine is null)
        {
            throw new InvalidOperationException(
                $"State machine must be configured for saga {typeof(TState).Name}. Call UseStateMachine().");
        }

        // Register the state machine as singleton (immutable after build)
        _services.AddSingleton<IStateMachine<TState>>(_stateMachine);

        // Register the repository
        if (_repositoryType is not null)
        {
            _services.TryAddScoped(typeof(ISagaRepository<TState>), _repositoryType);
        }
        else if (_useInMemory)
        {
            // Singleton for in-memory to share state across scopes
            _services.TryAddSingleton<ISagaRepository<TState>, InMemorySagaRepository<TState>>();
        }
        else
        {
            // Default to in-memory
            _services.TryAddSingleton<ISagaRepository<TState>, InMemorySagaRepository<TState>>();
        }

        // Register the dispatcher as scoped (needs per-request repository scope)
        _services.TryAddScoped<SagaDispatcher<TState>>();

        // Register a SagaMessageConsumer<TState, TMessage> for each message type
        foreach (var messageType in _stateMachine.Events)
        {
            var consumerType = typeof(SagaMessageConsumer<,>).MakeGenericType(typeof(TState), messageType);
            var consumerInterface = typeof(Consumers.IConsumer<>).MakeGenericType(messageType);

            _services.TryAddScoped(consumerType);
            _services.TryAddScoped(consumerInterface, consumerType);
        }
    }

    /// <summary>
    /// Gets the built state machine. Used by MessagingConfigurator to auto-register endpoints.
    /// </summary>
    internal IStateMachine<TState>? StateMachine => _stateMachine;
}
