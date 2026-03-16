using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VsaResults.Messaging.Bus;
using VsaResults.Messaging.Consumers;
using VsaResults.Messaging.Retry;
using VsaResults.Messaging.Sagas;
using VsaResults.Messaging.Serialization;
using VsaResults.Messaging.Transports;
using VsaResults.Messaging.Transports.InMemory;

namespace VsaResults.Messaging.DependencyInjection;

/// <summary>
/// Internal implementation of the messaging configurator.
/// </summary>
internal sealed class MessagingConfigurator : IMessagingConfigurator
{
    private readonly IServiceCollection _services;
    private readonly List<Assembly> _consumerAssemblies = [];
    private readonly List<EndpointRegistration> _endpoints = [];
    private readonly List<Action> _sagaBuilders = [];

    private InMemoryTransportOptions? _inMemoryOptions;
    private Action<IServiceCollection>? _customTransportRegistration;
    private IRetryPolicy? _globalRetryPolicy;
    private int? _globalConcurrencyLimit;
    private (int Threshold, TimeSpan ResetInterval)? _circuitBreaker;
    private Type? _serializerType;

    public MessagingConfigurator(IServiceCollection services)
    {
        _services = services;
    }

    /// <inheritdoc />
    public IMessagingConfigurator UseInMemoryTransport(Action<InMemoryTransportOptions>? configure = null)
    {
        _inMemoryOptions = new InMemoryTransportOptions();
        configure?.Invoke(_inMemoryOptions);
        return this;
    }

    /// <inheritdoc />
    public IMessagingConfigurator RegisterTransport(Action<IServiceCollection> transportRegistration)
    {
        _customTransportRegistration = transportRegistration;
        return this;
    }

    /// <inheritdoc />
    public IMessagingConfigurator AddConsumers(params Assembly[] assemblies)
    {
        _consumerAssemblies.AddRange(assemblies);
        return this;
    }

    /// <inheritdoc />
    public IMessagingConfigurator AddConsumers<T>()
    {
        return AddConsumers(typeof(T).Assembly);
    }

    /// <inheritdoc />
    public IMessagingConfigurator AddConsumer<TConsumer>()
        where TConsumer : class, IConsumer
    {
        _services.TryAddScoped<TConsumer>();
        return this;
    }

    /// <inheritdoc />
    public IMessagingConfigurator AddConsumer<TConsumer, TDefinition>()
        where TConsumer : class, IConsumer
        where TDefinition : class, IConsumerDefinition<TConsumer>
    {
        _services.TryAddScoped<TConsumer>();
        _services.TryAddSingleton<IConsumerDefinition<TConsumer>, TDefinition>();
        return this;
    }

    /// <inheritdoc />
    public IMessagingConfigurator ReceiveEndpoint(string queueName, Action<IReceiveEndpointConfigurator> configure)
    {
        _endpoints.Add(new EndpointRegistration(queueName, configure));
        return this;
    }

    /// <inheritdoc />
    public IMessagingConfigurator ReceiveEndpoint<TConsumer>(Action<IReceiveEndpointConfigurator>? configure = null)
        where TConsumer : class, IConsumer
    {
        var queueName = GetQueueName<TConsumer>();
        _endpoints.Add(new EndpointRegistration(queueName, cfg =>
        {
            cfg.Consumer<TConsumer>();
            configure?.Invoke(cfg);
        }));

        _services.TryAddScoped<TConsumer>();
        return this;
    }

    /// <inheritdoc />
    public IMessagingConfigurator UseRetry(IRetryPolicy policy)
    {
        _globalRetryPolicy = policy;
        return this;
    }

    /// <inheritdoc />
    public IMessagingConfigurator UseMessageRetry(Action<IRetryConfigurator> configure)
    {
        var retryConfig = new RetryConfigurator();
        configure(retryConfig);
        _globalRetryPolicy = retryConfig.Build();
        return this;
    }

    /// <inheritdoc />
    public IMessagingConfigurator UseCircuitBreaker(int failureThreshold, TimeSpan resetInterval)
    {
        _circuitBreaker = (failureThreshold, resetInterval);
        return this;
    }

    /// <inheritdoc />
    public IMessagingConfigurator UseConcurrencyLimit(int limit)
    {
        _globalConcurrencyLimit = limit;
        return this;
    }

    /// <inheritdoc />
    public IMessagingConfigurator UseWideEvents()
    {
        return this;
    }

    /// <inheritdoc />
    public IMessagingConfigurator UseFilter<TFilter>()
        where TFilter : class
    {
        _services.TryAddScoped<TFilter>();
        return this;
    }

    /// <inheritdoc />
    public IMessagingConfigurator UseSerializer<TSerializer>()
        where TSerializer : class, IMessageSerializer
    {
        _serializerType = typeof(TSerializer);
        return this;
    }

    /// <inheritdoc />
    public IMessagingConfigurator AddSaga<TState>(Action<ISagaConfigurator<TState>> configure)
        where TState : class, ISagaState, new()
    {
        var sagaConfigurator = new SagaConfigurator<TState>(_services);
        configure(sagaConfigurator);

        _sagaBuilders.Add(() =>
        {
            sagaConfigurator.Build();

            // Auto-register a receive endpoint for the saga
            if (sagaConfigurator.StateMachine is not null)
            {
                var queueName = $"saga-{ToKebabCase(typeof(TState).Name)}";
                _endpoints.Add(new EndpointRegistration(queueName, cfg =>
                {
                    foreach (var messageType in sagaConfigurator.StateMachine.Events)
                    {
                        var consumerType = typeof(SagaMessageConsumer<,>).MakeGenericType(typeof(TState), messageType);

                        // Use reflection to call Consumer<T> with the concrete consumer type
                        var consumerMethod = typeof(IReceiveEndpointConfigurator)
                            .GetMethods()
                            .First(m => m.Name == "Consumer" && m.IsGenericMethod);

                        consumerMethod.MakeGenericMethod(consumerType).Invoke(cfg, null);
                    }
                }));
            }
        });

        return this;
    }

    /// <summary>
    /// Builds and registers all messaging services.
    /// </summary>
    internal void Build()
    {
        // Register serializer
        if (_serializerType is not null)
        {
            _services.AddSingleton(typeof(IMessageSerializer), _serializerType);
        }

        // Build saga registrations (must happen before endpoint creation)
        foreach (var sagaBuilder in _sagaBuilders)
        {
            sagaBuilder();
        }

        // Register consumers from assemblies
        foreach (var assembly in _consumerAssemblies)
        {
            RegisterConsumersFromAssembly(assembly);
        }

        // Register transport
        if (_customTransportRegistration is not null)
        {
            _customTransportRegistration(_services);
        }
        else if (_inMemoryOptions is not null)
        {
            RegisterInMemoryTransport();
        }
        else
        {
            // Default to in-memory if no transport configured
            _inMemoryOptions = new InMemoryTransportOptions();
            RegisterInMemoryTransport();
        }

        // Register bus factory - IBus is the primary registration
        _services.AddSingleton<IBus>(sp =>
        {
            var transport = sp.GetRequiredService<ITransport>();
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            var typeResolver = sp.GetRequiredService<MessageTypeResolver>();

            var bus = new Bus.Bus(transport, serializer, typeResolver);

            // Configure endpoints
            foreach (var endpoint in _endpoints)
            {
                ConfigureEndpoint(bus, transport, sp, endpoint);
            }

            return bus;
        });

        // Register other interfaces to the same instance
        _services.AddSingleton<IBusControl>(sp => sp.GetRequiredService<IBus>());
        _services.AddSingleton<IPublishEndpoint>(sp => sp.GetRequiredService<IBus>());
        _services.AddSingleton<ISendEndpointProvider>(sp => sp.GetRequiredService<IBus>());

        // Register hosted service for bus lifecycle
        _services.AddHostedService<BusHostedService>();
    }

    private void RegisterInMemoryTransport()
    {
        _services.AddSingleton<ITransport>(sp =>
        {
            return new InMemoryTransport(sp);
        });
    }

    // ReSharper disable once UnusedParameter.Local
    private void ConfigureEndpoint(Bus.Bus bus, ITransport transport, IServiceProvider sp, EndpointRegistration registration)
    {
        var address = EndpointAddress.FromUri(new Uri($"{transport.Scheme}://localhost/{registration.QueueName}"));

        var endpointResult = transport.CreateReceiveEndpointAsync(address, cfg =>
        {
            // Apply global settings
            if (_globalRetryPolicy is not null)
            {
                cfg.UseRetry(_globalRetryPolicy);
            }

            if (_globalConcurrencyLimit.HasValue)
            {
                cfg.UseConcurrencyLimit(_globalConcurrencyLimit.Value);
            }

            if (_circuitBreaker.HasValue)
            {
                cfg.UseCircuitBreaker(_circuitBreaker.Value.Threshold, _circuitBreaker.Value.ResetInterval);
            }

            // Apply endpoint-specific configuration
            registration.Configure(cfg);
        }).GetAwaiter().GetResult();

        if (endpointResult.IsError)
        {
            throw new InvalidOperationException(
                $"Failed to create receive endpoint '{registration.QueueName}': {string.Join(", ", endpointResult.Errors.Select(e => e.Description))}");
        }

        bus.AddReceiveEndpoint(endpointResult.Value);
    }

    private void RegisterConsumersFromAssembly(Assembly assembly)
    {
        var consumerInterface = typeof(IConsumer);
        var types = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false, IsClass: true })
            .Where(t => consumerInterface.IsAssignableFrom(t));

        foreach (var type in types)
        {
            _services.TryAddScoped(type);

            // Also register for generic interfaces
            foreach (var iface in type.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IConsumer<>))
                {
                    _services.TryAddScoped(iface, type);
                }
            }
        }
    }

    private static string GetQueueName<TConsumer>()
    {
        var name = typeof(TConsumer).Name;

        // Remove common suffixes
        if (name.EndsWith("Consumer", StringComparison.Ordinal))
        {
            name = name[..^8];
        }

        // Convert to kebab-case
        return ToKebabCase(name);
    }

    private static string ToKebabCase(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var result = new System.Text.StringBuilder();
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    result.Append('-');
                }

                result.Append(char.ToLowerInvariant(c));
            }
            else
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }

    private sealed record EndpointRegistration(string QueueName, Action<IReceiveEndpointConfigurator> Configure);
}

/// <summary>
/// Internal retry configurator implementation.
/// </summary>
internal sealed class RetryConfigurator : IRetryConfigurator
{
    private int _limit = 3;
    private TimeSpan _interval = TimeSpan.FromSeconds(1);
    private TimeSpan? _minInterval;
    private TimeSpan? _maxInterval;
    private bool _useExponential;
    // ReSharper disable CollectionNeverQueried.Local — exception filtering will be implemented in a future iteration
    private readonly List<Type> _handledExceptions = [];
    private readonly List<Type> _ignoredExceptions = [];

    // ReSharper restore CollectionNeverQueried.Local
    public IRetryConfigurator Limit(int count)
    {
        _limit = count;
        return this;
    }

    public IRetryConfigurator Interval(TimeSpan interval)
    {
        _interval = interval;
        _useExponential = false;
        return this;
    }

    public IRetryConfigurator Exponential(TimeSpan minInterval, TimeSpan maxInterval)
    {
        _minInterval = minInterval;
        _maxInterval = maxInterval;
        _useExponential = true;
        return this;
    }

    public IRetryConfigurator Handle<TException>()
        where TException : Exception
    {
        _handledExceptions.Add(typeof(TException));
        return this;
    }

    public IRetryConfigurator Ignore<TException>()
        where TException : Exception
    {
        _ignoredExceptions.Add(typeof(TException));
        return this;
    }

    internal IRetryPolicy Build()
    {
        if (_useExponential && _minInterval.HasValue && _maxInterval.HasValue)
        {
            return RetryPolicy.Exponential(_limit, _minInterval.Value, _maxInterval.Value);
        }

        return RetryPolicy.Interval(_limit, _interval);
    }
}
