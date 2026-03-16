using JetBrains.Annotations;
using VsaResults.Messaging.Messages;
using VsaResults.Messaging.Pipeline;
using VsaResults.Messaging.Retry;

namespace VsaResults.Messaging.Consumers;

/// <summary>
/// Defines configuration for a consumer separate from its implementation.
/// Allows declarative configuration of retry policies, concurrency limits, and more.
/// </summary>
/// <typeparam name="TConsumer">The consumer type.</typeparam>
/// <remarks>
/// <para>
/// Consumer definitions follow the MassTransit pattern of separating configuration
/// from business logic. This enables:
/// </para>
/// <list type="bullet">
/// <item><description>Declarative configuration via attributes or fluent API</description></item>
/// <item><description>Environment-specific configuration without code changes</description></item>
/// <item><description>Testable configuration logic</description></item>
/// </list>
/// </remarks>
[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public interface IConsumerDefinition<TConsumer>
    where TConsumer : class, IConsumer
{
    /// <summary>
    /// Gets the endpoint name for this consumer.
    /// Defaults to the consumer type name in kebab-case.
    /// </summary>
    string EndpointName => typeof(TConsumer).Name.ToKebabCase();

    /// <summary>
    /// Gets the retry policy for this consumer.
    /// If null, the default bus retry policy is used.
    /// </summary>
    IRetryPolicy? RetryPolicy => null;

    /// <summary>
    /// Gets the maximum concurrent messages this consumer can process.
    /// If null, uses the default concurrency limit.
    /// </summary>
    int? ConcurrencyLimit => null;

    /// <summary>
    /// Gets the prefetch count for this consumer.
    /// Prefetch determines how many messages are retrieved from the broker at once.
    /// </summary>
    int? PrefetchCount => null;

    /// <summary>
    /// Gets whether to use a dedicated error queue for this consumer.
    /// </summary>
    bool UseErrorQueue => true;

    /// <summary>
    /// Gets the error queue name. Defaults to "{EndpointName}_error".
    /// </summary>
    string? ErrorQueueName => null;

    /// <summary>
    /// Gets whether to use the message retry filter.
    /// </summary>
    bool UseMessageRetry => true;

    /// <summary>
    /// Configures the consumer endpoint.
    /// Override to add custom filters or configuration.
    /// </summary>
    /// <param name="configurator">The endpoint configurator.</param>
    void Configure(IEndpointConfigurator configurator)
    {
    }
}

/// <summary>
/// Base class for consumer definitions with fluent configuration API.
/// </summary>
/// <typeparam name="TConsumer">The consumer type.</typeparam>
/// <remarks>
/// <para>
/// Example:
/// <code>
/// public class OrderCreatedConsumerDefinition : ConsumerDefinition&lt;OrderCreatedConsumer&gt;
/// {
///     public OrderCreatedConsumerDefinition()
///     {
///         Endpoint("order-events");
///         UseRetry(RetryPolicy.Exponential(5, TimeSpan.FromMilliseconds(100)));
///         ConcurrentMessageLimit(10);
///         Prefetch(20);
///     }
/// }
/// </code>
/// </para>
/// </remarks>
public abstract class ConsumerDefinition<TConsumer> : IConsumerDefinition<TConsumer>
    where TConsumer : class, IConsumer
{
    private string? _endpointName;
    private IRetryPolicy? _retryPolicy;
    private int? _concurrencyLimit;
    private int? _prefetchCount;
    private bool _useErrorQueue = true;
    private string? _errorQueueName;
    private bool _useMessageRetry = true;

    /// <inheritdoc />
    public string EndpointName => _endpointName ?? typeof(TConsumer).Name.ToKebabCase();

    /// <inheritdoc />
    public IRetryPolicy? RetryPolicy => _retryPolicy;

    /// <inheritdoc />
    public int? ConcurrencyLimit => _concurrencyLimit;

    /// <inheritdoc />
    public int? PrefetchCount => _prefetchCount;

    /// <inheritdoc />
    public bool UseErrorQueue => _useErrorQueue;

    /// <inheritdoc />
    public string? ErrorQueueName => _errorQueueName;

    /// <inheritdoc />
    public bool UseMessageRetry => _useMessageRetry;

    /// <summary>
    /// Sets the endpoint name for this consumer.
    /// </summary>
    /// <param name="name">The endpoint name.</param>
    protected void Endpoint(string name) => _endpointName = name;

    /// <summary>
    /// Sets the retry policy for this consumer.
    /// </summary>
    /// <param name="policy">The retry policy.</param>
    protected void UseRetry(IRetryPolicy policy) => _retryPolicy = policy;

    /// <summary>
    /// Disables retry for this consumer.
    /// </summary>
    protected void DisableRetry() => _useMessageRetry = false;

    /// <summary>
    /// Sets the maximum concurrent message limit.
    /// </summary>
    /// <param name="limit">The concurrency limit.</param>
    protected void ConcurrentMessageLimit(int limit) => _concurrencyLimit = limit;

    /// <summary>
    /// Sets the prefetch count.
    /// </summary>
    /// <param name="count">The prefetch count.</param>
    protected void Prefetch(int count) => _prefetchCount = count;

    /// <summary>
    /// Disables the error queue for this consumer.
    /// Failed messages will be discarded instead of moved to an error queue.
    /// </summary>
    protected void DisableErrorQueue() => _useErrorQueue = false;

    /// <summary>
    /// Sets a custom error queue name.
    /// </summary>
    /// <param name="name">The error queue name.</param>
    protected void ErrorQueue(string name) => _errorQueueName = name;

    /// <inheritdoc />
    public virtual void Configure(IEndpointConfigurator configurator)
    {
    }
}

/// <summary>
/// Endpoint configurator for consumer definitions.
/// </summary>
public interface IEndpointConfigurator
{
    /// <summary>
    /// Adds a filter to the endpoint pipeline.
    /// </summary>
    /// <typeparam name="TFilter">The filter type.</typeparam>
    void UseFilter<TFilter>()
        where TFilter : class, IFilter<ConsumeContext>;

    /// <summary>
    /// Configures the retry policy for this endpoint.
    /// </summary>
    /// <param name="policy">The retry policy.</param>
    void UseRetry(IRetryPolicy policy);

    /// <summary>
    /// Configures the concurrency limit.
    /// </summary>
    /// <param name="limit">The maximum concurrent messages.</param>
    void UseConcurrencyLimit(int limit);

    /// <summary>
    /// Configures the circuit breaker.
    /// </summary>
    /// <param name="failureThreshold">Number of failures before opening.</param>
    /// <param name="resetInterval">Time to wait before attempting to close.</param>
    void UseCircuitBreaker(int failureThreshold, TimeSpan resetInterval);

    /// <summary>
    /// Configures the timeout for message processing.
    /// </summary>
    /// <param name="timeout">The timeout duration.</param>
    void UseTimeout(TimeSpan timeout);
}

/// <summary>
/// Base consume context type for filter interfaces.
/// Extends PipeContext to participate in the filter pipeline.
/// </summary>
public abstract class ConsumeContext : PipeContext
{
    /// <summary>Gets the message envelope.</summary>
    public abstract MessageEnvelope Envelope { get; }

    /// <summary>Gets the message object.</summary>
    public abstract object MessageObject { get; }

    /// <summary>Gets the wide event context.</summary>
    public abstract Dictionary<string, object?> WideEventContext { get; }
}
