using VsaResults.Features.Features;
using VsaResults.Messaging.Consumers;
using VsaResults.Messaging.Messages;
using VsaResults.Messaging.Pipeline;
using VsaResults.Messaging.Retry;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Transports;

/// <summary>
/// Receive endpoint for consuming messages from a queue.
/// </summary>
public interface IReceiveEndpoint : IAsyncDisposable
{
    /// <summary>Gets the endpoint address.</summary>
    EndpointAddress Address { get; }

    /// <summary>Gets the endpoint name.</summary>
    string Name { get; }

    /// <summary>Gets whether the endpoint is running.</summary>
    bool IsRunning { get; }

    /// <summary>
    /// Starts receiving messages.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Unit on success, or an error.</returns>
    Task<VsaResult<Unit>> StartAsync(CancellationToken ct = default);

    /// <summary>
    /// Stops receiving messages.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Unit on success, or an error.</returns>
    Task<VsaResult<Unit>> StopAsync(CancellationToken ct = default);
}

/// <summary>
/// Configurator for receive endpoints.
/// </summary>
public interface IReceiveEndpointConfigurator
{
    /// <summary>Gets the endpoint name.</summary>
    string EndpointName { get; }

    /// <summary>
    /// Configures a consumer for this endpoint.
    /// </summary>
    /// <typeparam name="TConsumer">The consumer type.</typeparam>
    void Consumer<TConsumer>()
        where TConsumer : class, IConsumer;

    /// <summary>
    /// Configures a consumer with a factory.
    /// </summary>
    /// <typeparam name="TConsumer">The consumer type.</typeparam>
    /// <param name="factory">Factory to create consumer instances.</param>
    void Consumer<TConsumer>(Func<IServiceProvider, TConsumer> factory)
        where TConsumer : class, IConsumer;

    /// <summary>
    /// Configures the receive endpoint with a message handler.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="handler">The message handler.</param>
    void Handler<TMessage>(Func<ConsumeContext<TMessage>, CancellationToken, Task<VsaResult<Unit>>> handler)
        where TMessage : class, IMessage;

    /// <summary>
    /// Configures the retry policy for this endpoint.
    /// </summary>
    /// <param name="policy">The retry policy.</param>
    void UseRetry(IRetryPolicy policy);

    /// <summary>
    /// Configures the concurrency limit.
    /// </summary>
    /// <param name="limit">Maximum concurrent messages.</param>
    void UseConcurrencyLimit(int limit);

    /// <summary>
    /// Configures the prefetch count.
    /// </summary>
    /// <param name="count">Number of messages to prefetch.</param>
    void UsePrefetch(int count);

    /// <summary>
    /// Configures a circuit breaker.
    /// </summary>
    /// <param name="failureThreshold">Failures before opening.</param>
    /// <param name="resetInterval">Time before attempting to close.</param>
    void UseCircuitBreaker(int failureThreshold, TimeSpan resetInterval);

    /// <summary>
    /// Configures message timeout.
    /// </summary>
    /// <param name="timeout">The timeout duration.</param>
    void UseTimeout(TimeSpan timeout);

    /// <summary>
    /// Adds a filter to the receive pipeline.
    /// </summary>
    /// <typeparam name="TFilter">The filter type.</typeparam>
    void UseFilter<TFilter>()
        where TFilter : class;

    /// <summary>
    /// Adds a filter instance to the receive pipeline.
    /// </summary>
    /// <param name="filter">The filter instance.</param>
    void UseFilter<TContext>(IFilter<TContext> filter)
        where TContext : PipeContext;
}
