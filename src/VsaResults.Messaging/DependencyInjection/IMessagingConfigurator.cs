using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using VsaResults.Messaging.Consumers;
using VsaResults.Messaging.Retry;
using VsaResults.Messaging.Sagas;
using VsaResults.Messaging.Serialization;
using VsaResults.Messaging.Transports;
using VsaResults.Messaging.Transports.InMemory;

namespace VsaResults.Messaging.DependencyInjection;

/// <summary>
/// Configurator for VsaResults.Messaging services.
/// Provides fluent API for setting up message transports, consumers, and endpoints.
/// </summary>
public interface IMessagingConfigurator
{
    /// <summary>
    /// Configures the in-memory transport.
    /// Useful for testing and development scenarios.
    /// </summary>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The configurator for chaining.</returns>
    IMessagingConfigurator UseInMemoryTransport(Action<InMemoryTransportOptions>? configure = null);

    /// <summary>
    /// Registers a custom transport provider.
    /// This is an extension point for external transport packages (e.g., VsaResults.Messaging.RabbitMq).
    /// </summary>
    /// <param name="transportRegistration">Action that registers the transport with the service collection.</param>
    /// <returns>The configurator for chaining.</returns>
    IMessagingConfigurator RegisterTransport(Action<IServiceCollection> transportRegistration);

    /// <summary>
    /// Adds consumers from the specified assemblies.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan for consumers.</param>
    /// <returns>The configurator for chaining.</returns>
    IMessagingConfigurator AddConsumers(params Assembly[] assemblies);

    /// <summary>
    /// Adds consumers from the assembly containing the specified type.
    /// </summary>
    /// <typeparam name="T">A type in the assembly to scan.</typeparam>
    /// <returns>The configurator for chaining.</returns>
    IMessagingConfigurator AddConsumers<T>();

    /// <summary>
    /// Adds a specific consumer type.
    /// </summary>
    /// <typeparam name="TConsumer">The consumer type.</typeparam>
    /// <returns>The configurator for chaining.</returns>
    IMessagingConfigurator AddConsumer<TConsumer>()
        where TConsumer : class, IConsumer;

    /// <summary>
    /// Adds a specific consumer type with its definition.
    /// </summary>
    /// <typeparam name="TConsumer">The consumer type.</typeparam>
    /// <typeparam name="TDefinition">The consumer definition type.</typeparam>
    /// <returns>The configurator for chaining.</returns>
    IMessagingConfigurator AddConsumer<TConsumer, TDefinition>()
        where TConsumer : class, IConsumer
        where TDefinition : class, IConsumerDefinition<TConsumer>;

    /// <summary>
    /// Configures a receive endpoint.
    /// </summary>
    /// <param name="queueName">The queue name.</param>
    /// <param name="configure">The endpoint configuration action.</param>
    /// <returns>The configurator for chaining.</returns>
    IMessagingConfigurator ReceiveEndpoint(string queueName, Action<IReceiveEndpointConfigurator> configure);

    /// <summary>
    /// Configures a receive endpoint using the consumer's convention-based queue name.
    /// </summary>
    /// <typeparam name="TConsumer">The consumer type.</typeparam>
    /// <param name="configure">Optional endpoint configuration action.</param>
    /// <returns>The configurator for chaining.</returns>
    IMessagingConfigurator ReceiveEndpoint<TConsumer>(Action<IReceiveEndpointConfigurator>? configure = null)
        where TConsumer : class, IConsumer;

    /// <summary>
    /// Configures a global retry policy for all consumers.
    /// </summary>
    /// <param name="policy">The retry policy.</param>
    /// <returns>The configurator for chaining.</returns>
    IMessagingConfigurator UseRetry(IRetryPolicy policy);

    /// <summary>
    /// Configures a global message retry with simple options.
    /// </summary>
    /// <param name="configure">The retry configuration action.</param>
    /// <returns>The configurator for chaining.</returns>
    IMessagingConfigurator UseMessageRetry(Action<IRetryConfigurator> configure);

    /// <summary>
    /// Configures a global circuit breaker.
    /// </summary>
    /// <param name="failureThreshold">Number of failures before opening.</param>
    /// <param name="resetInterval">Time before attempting to close.</param>
    /// <returns>The configurator for chaining.</returns>
    IMessagingConfigurator UseCircuitBreaker(int failureThreshold, TimeSpan resetInterval);

    /// <summary>
    /// Configures global concurrency limit.
    /// </summary>
    /// <param name="limit">Maximum concurrent message processing.</param>
    /// <returns>The configurator for chaining.</returns>
    IMessagingConfigurator UseConcurrencyLimit(int limit);

    /// <summary>
    /// Enables WideEvents integration for message processing observability.
    /// </summary>
    /// <returns>The configurator for chaining.</returns>
    IMessagingConfigurator UseWideEvents();

    /// <summary>
    /// Adds a custom filter to the message processing pipeline.
    /// </summary>
    /// <typeparam name="TFilter">The filter type.</typeparam>
    /// <returns>The configurator for chaining.</returns>
    IMessagingConfigurator UseFilter<TFilter>()
        where TFilter : class;

    /// <summary>
    /// Configures the message serializer.
    /// </summary>
    /// <typeparam name="TSerializer">The serializer type.</typeparam>
    /// <returns>The configurator for chaining.</returns>
    IMessagingConfigurator UseSerializer<TSerializer>()
        where TSerializer : class, IMessageSerializer;

    /// <summary>
    /// Registers a saga with its state machine definition and repository.
    /// Automatically registers consumers and receive endpoints for all handled message types.
    /// </summary>
    /// <typeparam name="TState">The saga state type.</typeparam>
    /// <param name="configure">The saga configuration action.</param>
    /// <returns>The configurator for chaining.</returns>
    IMessagingConfigurator AddSaga<TState>(Action<ISagaConfigurator<TState>> configure)
        where TState : class, ISagaState, new();
}

/// <summary>
/// Configurator for retry behavior.
/// </summary>
public interface IRetryConfigurator
{
    /// <summary>
    /// Sets the maximum number of retry attempts.
    /// </summary>
    /// <param name="count">The retry count.</param>
    /// <returns>The configurator for chaining.</returns>
    IRetryConfigurator Limit(int count);

    /// <summary>
    /// Sets the interval between retries.
    /// </summary>
    /// <param name="interval">The interval.</param>
    /// <returns>The configurator for chaining.</returns>
    IRetryConfigurator Interval(TimeSpan interval);

    /// <summary>
    /// Uses exponential backoff for retry intervals.
    /// </summary>
    /// <param name="minInterval">Minimum interval.</param>
    /// <param name="maxInterval">Maximum interval.</param>
    /// <returns>The configurator for chaining.</returns>
    IRetryConfigurator Exponential(TimeSpan minInterval, TimeSpan maxInterval);

    /// <summary>
    /// Specifies which exceptions should trigger a retry.
    /// </summary>
    /// <typeparam name="TException">The exception type.</typeparam>
    /// <returns>The configurator for chaining.</returns>
    IRetryConfigurator Handle<TException>()
        where TException : Exception;

    /// <summary>
    /// Specifies which exceptions should not trigger a retry.
    /// </summary>
    /// <typeparam name="TException">The exception type.</typeparam>
    /// <returns>The configurator for chaining.</returns>
    IRetryConfigurator Ignore<TException>()
        where TException : Exception;
}
