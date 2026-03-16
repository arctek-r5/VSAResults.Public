using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VsaResults.Features.Features;
using VsaResults.Messaging.Bus;
using VsaResults.Messaging.Consumers;
using VsaResults.Messaging.Messages;
using VsaResults.Messaging.Pipeline;
using VsaResults.Messaging.Retry;
using VsaResults.Messaging.Serialization;
using VsaResults.Messaging.WideEvents;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Transports.InMemory;

/// <summary>
/// In-memory receive endpoint.
/// Processes messages from an in-memory queue.
/// </summary>
internal sealed class InMemoryReceiveEndpoint : IReceiveEndpoint
{
    private readonly InMemoryQueue _queue;
    private readonly InMemoryReceiveEndpointConfigurator _configurator;
    private readonly IServiceProvider _serviceProvider;
    private readonly List<Task> _workerTasks = [];
    private CancellationTokenSource? _cts;

    public InMemoryReceiveEndpoint(
        EndpointAddress address,
        InMemoryQueue queue,
        InMemoryReceiveEndpointConfigurator configurator,
        IServiceProvider serviceProvider)
    {
        Address = address;
        _queue = queue;
        _configurator = configurator;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public EndpointAddress Address { get; }

    /// <inheritdoc />
    public string Name => Address.Name;

    /// <inheritdoc />
    public bool IsRunning => _cts is not null && !_cts.IsCancellationRequested;

    /// <inheritdoc />
    public Task<VsaResult<Unit>> StartAsync(CancellationToken ct = default)
    {
        if (IsRunning)
        {
            return Task.FromResult<VsaResult<Unit>>(Unit.Value);
        }

        _cts = new CancellationTokenSource();

        // Start worker tasks based on concurrency limit
        var concurrency = _configurator.ConcurrencyLimit ?? Environment.ProcessorCount;
        for (var i = 0; i < concurrency; i++)
        {
            _workerTasks.Add(ProcessMessagesAsync(_cts.Token));
        }

        return Task.FromResult<VsaResult<Unit>>(Unit.Value);
    }

    /// <inheritdoc />
    public async Task<VsaResult<Unit>> StopAsync(CancellationToken ct = default)
    {
        if (_cts is null)
        {
            return Unit.Value;
        }

        _cts.Cancel();

        try
        {
            await Task.WhenAll(_workerTasks);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        _workerTasks.Clear();
        _cts.Dispose();
        _cts = null;

        return Unit.Value;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    private async Task ProcessMessagesAsync(CancellationToken ct)
    {
        await foreach (var envelope in _queue.ReadAllAsync(ct))
        {
            try
            {
                await ProcessEnvelopeAsync(envelope, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Emit wide event so the exception appears in the canonical
                // observability stream, not just silently swallowed.
                try
                {
                    using var wideScope = _serviceProvider.CreateScope();
                    var emitter = wideScope.ServiceProvider.GetService<IMessageWideEventEmitter>();
                    if (emitter is not null)
                    {
                        var primaryType = envelope.MessageTypes.Count > 0 ? envelope.MessageTypes[0] : "unknown";
                        var builder = new MessageWideEventBuilder(
                            envelope.MessageId.ToString(),
                            envelope.CorrelationId.ToString(),
                            primaryType,
                            "InMemory",
                            "consumer");
                        emitter.Emit(builder.Exception(ex));
                    }
                }
                catch
                {
                    // Wide event emission must not mask the original exception handling.
                }
            }
        }
    }

    private async Task ProcessEnvelopeAsync(MessageEnvelope envelope, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();

        foreach (var registration in _configurator.GetConsumerRegistrations())
        {
            // Check if this consumer handles this message type
            var primaryType = envelope.MessageTypes.Count > 0 ? envelope.MessageTypes[0] : null;
            if (primaryType is null)
            {
                continue;
            }

            if (!registration.HandlesMessageType(primaryType))
            {
                continue;
            }

            await registration.InvokeAsync(scope.ServiceProvider, envelope, ct);
        }
    }
}

/// <summary>
/// Configurator for in-memory receive endpoints.
/// </summary>
internal sealed class InMemoryReceiveEndpointConfigurator : IReceiveEndpointConfigurator
{
    private readonly List<ConsumerRegistration> _consumers = [];
    private readonly List<string> _messageTypes = [];

    public InMemoryReceiveEndpointConfigurator(string endpointName)
    {
        EndpointName = endpointName;
    }

    /// <inheritdoc />
    public string EndpointName { get; }

    /// <summary>Gets or sets the retry policy.</summary>
    public IRetryPolicy? RetryPolicy { get; private set; }

    /// <summary>Gets or sets the concurrency limit.</summary>
    public int? ConcurrencyLimit { get; private set; }

    /// <summary>Gets or sets the prefetch count.</summary>
    public int? PrefetchCount { get; private set; }

    /// <inheritdoc />
    public void Consumer<TConsumer>()
        where TConsumer : class, IConsumer
    {
        var registration = new ConsumerRegistration(typeof(TConsumer));
        _consumers.Add(registration);

        // Register message types this consumer handles
        foreach (var messageType in registration.GetMessageTypeNames())
        {
            _messageTypes.Add(messageType);
        }
    }

    /// <inheritdoc />
    public void Consumer<TConsumer>(Func<IServiceProvider, TConsumer> factory)
        where TConsumer : class, IConsumer
    {
        var registration = new ConsumerRegistration(typeof(TConsumer), factory);
        _consumers.Add(registration);

        foreach (var messageType in registration.GetMessageTypeNames())
        {
            _messageTypes.Add(messageType);
        }
    }

    /// <inheritdoc />
    public void Handler<TMessage>(Func<ConsumeContext<TMessage>, CancellationToken, Task<VsaResult<Unit>>> handler)
        where TMessage : class, IMessage
    {
        var registration = new HandlerRegistration<TMessage>(handler);
        _consumers.Add(registration);

        var resolver = new MessageTypeResolver();
        foreach (var typeName in resolver.GetMessageTypes<TMessage>())
        {
            _messageTypes.Add(typeName);
        }
    }

    /// <inheritdoc />
    public void UseRetry(IRetryPolicy policy) => RetryPolicy = policy;

    /// <inheritdoc />
    public void UseConcurrencyLimit(int limit) => ConcurrencyLimit = limit;

    /// <inheritdoc />
    public void UsePrefetch(int count) => PrefetchCount = count;

    /// <inheritdoc />
    public void UseCircuitBreaker(int failureThreshold, TimeSpan resetInterval)
    {
        // Stored for pipeline configuration
    }

    /// <inheritdoc />
    public void UseTimeout(TimeSpan timeout)
    {
        // Stored for pipeline configuration
    }

    /// <inheritdoc />
    public void UseFilter<TFilter>()
        where TFilter : class
    {
        // Stored for pipeline configuration
    }

    /// <inheritdoc />
    public void UseFilter<TContext>(IFilter<TContext> filter)
        where TContext : PipeContext
    {
        // Stored for pipeline configuration
    }

    /// <summary>Gets the registered message types.</summary>
    public IEnumerable<string> GetMessageTypes() => _messageTypes;

    /// <summary>Gets the consumer registrations.</summary>
    public IEnumerable<ConsumerRegistration> GetConsumerRegistrations() => _consumers;
}

/// <summary>
/// Registration for a consumer.
/// </summary>
internal class ConsumerRegistration
{
    private readonly Type _consumerType;
    private readonly Func<IServiceProvider, object>? _factory;
    private readonly HashSet<string> _messageTypeNames;

    public ConsumerRegistration(
        Type consumerType,
        Func<IServiceProvider, object>? factory = null)
    {
        _consumerType = consumerType;
        _factory = factory;
        _messageTypeNames = [.. ResolveMessageTypeNames(consumerType)];
    }

    private static IEnumerable<string> ResolveMessageTypeNames(Type consumerType)
    {
        var typeResolver = new MessageTypeResolver();

        foreach (var iface in consumerType.GetInterfaces())
        {
            if (!iface.IsGenericType)
            {
                continue;
            }

            var genericDef = iface.GetGenericTypeDefinition();
            if (genericDef == typeof(IConsumer<>))
            {
                var messageType = iface.GetGenericArguments()[0];
                foreach (var typeName in typeResolver.GetMessageTypes(messageType))
                {
                    yield return typeName;
                }
            }
        }
    }

    public Type ConsumerType => _consumerType;

    public virtual bool HandlesMessageType(string messageTypeName)
    {
        return _messageTypeNames.Contains(messageTypeName);
    }

    public virtual IEnumerable<string> GetMessageTypeNames()
    {
        return ResolveMessageTypeNames(_consumerType);
    }

    public virtual async Task InvokeAsync(
        IServiceProvider scopedProvider,
        MessageEnvelope envelope,
        CancellationToken ct)
    {
        var consumer = _factory?.Invoke(scopedProvider)
            ?? scopedProvider.GetRequiredService(_consumerType);

        // Find and invoke the appropriate Consume method
        foreach (var iface in _consumerType.GetInterfaces())
        {
            if (!iface.IsGenericType || iface.GetGenericTypeDefinition() != typeof(IConsumer<>))
            {
                continue;
            }

            var messageType = iface.GetGenericArguments()[0];
            var typeName = MessageTypeResolver.GetPrimaryIdentifier(messageType);

            if (!envelope.MessageTypes.Contains(typeName))
            {
                continue;
            }

            // Deserialize the message
            var serializer = scopedProvider.GetRequiredService<IMessageSerializer>();
            var messageResult = serializer.Deserialize(envelope.Body, messageType);

            if (messageResult.IsError)
            {
                var errorDescription = string.Join("; ", messageResult.Errors.Select(e => $"{e.Code}: {e.Description}"));
                throw new InvalidOperationException(
                    $"Failed to deserialize message {envelope.MessageId} for consumer {_consumerType.Name}: {errorDescription}");
            }

            // Create consume context
            var contextType = typeof(ConsumeContext<>).MakeGenericType(messageType);
            var bus = scopedProvider.GetRequiredService<IBus>();

            var context = Activator.CreateInstance(contextType);
            if (context is null)
            {
                continue;
            }

            // Set properties via reflection (simplified)
            contextType.GetProperty("Message")!.SetValue(context, messageResult.Value);
            contextType.GetProperty("Envelope")!.SetValue(context, envelope);
            contextType.GetProperty("PublishEndpoint")!.SetValue(context, bus);
            contextType.GetProperty("SendEndpointProvider")!.SetValue(context, bus);

            // Invoke ConsumeAsync
            var method = iface.GetMethod("ConsumeAsync");
            if (method is null)
            {
                continue;
            }

            var task = method.Invoke(consumer, [context, ct]) as Task;
            if (task is null)
            {
                continue;
            }

            await task;

            // Extract VsaResult<Unit> from the completed task to check for errors
            var resultProperty = task.GetType().GetProperty("Result");
            if (resultProperty?.GetValue(task) is VsaResult<Unit> result && result.IsError)
            {
                // Emit wide event for the consumer error
                var emitter = scopedProvider.GetService<IMessageWideEventEmitter>();
                if (emitter is not null)
                {
                    var wideEventBuilder = new MessageWideEventBuilder(
                        envelope.MessageId.ToString(),
                        envelope.CorrelationId.ToString(),
                        typeName,
                        _consumerType.Name,
                        "consumer");

                    // Merge wide event context from the consume context via reflection
                    var wideEventCtxProp = contextType.GetProperty("WideEventContext");
                    if (wideEventCtxProp?.GetValue(context) is Dictionary<string, object?> wideCtx)
                    {
                        foreach (var (k, v) in wideCtx)
                        {
                            wideEventBuilder.WithContext(k, v);
                        }
                    }

                    emitter.Emit(wideEventBuilder.ConsumerError(result.Errors));
                }

                var logger = scopedProvider.GetService<ILogger<ConsumerRegistration>>();
                var errorDesc = string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
                logger?.LogError(
                    "Consumer {Consumer} returned error for message {MessageId} (correlation {CorrelationId}): {Error}",
                    _consumerType.Name,
                    envelope.MessageId,
                    envelope.CorrelationId,
                    errorDesc);

                throw new ConsumerErrorException(
                    $"Consumer {_consumerType.Name} returned error: {errorDesc}",
                    result.Errors);
            }
        }
    }
}

/// <summary>
/// Registration for a handler delegate.
/// </summary>
internal sealed class HandlerRegistration<TMessage> : ConsumerRegistration
    where TMessage : class, IMessage
{
    private readonly Func<ConsumeContext<TMessage>, CancellationToken, Task<VsaResult<Unit>>> _handler;
    private readonly MessageTypeResolver _typeResolver = new();
    private readonly HashSet<string> _handlerMessageTypes;

    public HandlerRegistration(
        Func<ConsumeContext<TMessage>, CancellationToken, Task<VsaResult<Unit>>> handler)
        : base(typeof(HandlerRegistration<TMessage>))
    {
        _handler = handler;
        _handlerMessageTypes = [.. _typeResolver.GetMessageTypes<TMessage>()];
    }

    public override bool HandlesMessageType(string messageTypeName)
    {
        return _handlerMessageTypes.Contains(messageTypeName);
    }

    public override IEnumerable<string> GetMessageTypeNames()
    {
        return _typeResolver.GetMessageTypes<TMessage>();
    }

    public override async Task InvokeAsync(
        IServiceProvider scopedProvider,
        MessageEnvelope envelope,
        CancellationToken ct)
    {
        var serializer = scopedProvider.GetRequiredService<IMessageSerializer>();
        var messageResult = serializer.Deserialize<TMessage>(envelope.Body);

        if (messageResult.IsError)
        {
            return;
        }

        var bus = scopedProvider.GetRequiredService<IBus>();
        var context = new ConsumeContext<TMessage>
        {
            Message = messageResult.Value,
            Envelope = envelope,
            PublishEndpoint = bus,
            SendEndpointProvider = bus
        };

        var result = await _handler(context, ct);

        if (result.IsError)
        {
            var emitter = scopedProvider.GetService<IMessageWideEventEmitter>();
            if (emitter is not null)
            {
                var wideEventBuilder = new MessageWideEventBuilder(
                    envelope.MessageId.ToString(),
                    envelope.CorrelationId.ToString(),
                    typeof(TMessage).Name,
                    "Handler",
                    "consumer");

                foreach (var (k, v) in context.WideEventContext)
                {
                    wideEventBuilder.WithContext(k, v);
                }

                emitter.Emit(wideEventBuilder.ConsumerError(result.Errors));
            }

            var logger = scopedProvider.GetService<ILogger<HandlerRegistration<TMessage>>>();
            var errorDesc = string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
            logger?.LogError(
                "Handler for {MessageType} returned error for message {MessageId} (correlation {CorrelationId}): {Error}",
                typeof(TMessage).Name,
                envelope.MessageId,
                envelope.CorrelationId,
                errorDesc);

            throw new ConsumerErrorException(
                $"Handler for {typeof(TMessage).Name} returned error: {errorDesc}",
                result.Errors);
        }
    }
}
