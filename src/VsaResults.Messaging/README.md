# VsaResults.Messaging

Transport-agnostic messaging library with VsaResult integration, consumer pipelines, sagas, and wide event observability. Inspired by MassTransit's API surface but built on VsaResult error handling instead of exceptions.

## What This Is

A messaging abstraction layer that provides:

- **Bus** -- Publish events, send commands
- **Consumers** -- Process messages with `VsaResult<Unit>` returns (not exceptions)
- **Sagas** -- Stateful orchestration of long-running processes
- **Pipeline/Filters** -- Middleware for cross-cutting concerns (retry, circuit breaker, timeout, concurrency)
- **Wide Events** -- One structured event per message consumption
- **In-Memory Transport** -- Built-in for testing

For production use, add a transport package: `VsaResults.Messaging.RabbitMq`, `.Kafka`, or `.AzureServiceBus`.

## Key Types

### Messages

| Type | Purpose |
|------|---------|
| `IMessage` | Marker interface for all messages. |
| `ICommand` | Point-to-point: sent to one endpoint, processed by one consumer. |
| `IEvent` | Publish-subscribe: published to all subscribers, processed by zero or more. |
| `MessageEnvelope` | Wraps a message with headers, correlation IDs, timestamps. |
| `MessageHeaders` | Standard headers (message type, content type, correlation). |
| `CorrelationId`, `ConversationId`, `MessageId` | Typed IDs for distributed tracing. |

### Bus

| Type | Purpose |
|------|---------|
| `IBus` | Main bus interface: publish, send, control. |
| `IPublishEndpoint` | Publish events (fan-out). |
| `ISendEndpoint` | Send commands (point-to-point). |
| `IBusControl` | Start/stop the bus. |

### Consumers

| Type | Purpose |
|------|---------|
| `IConsumer<TMessage>` | Consume a message, return `VsaResult<Unit>`. On error, a `Fault<TMessage>` is auto-published. |
| `IConsumer<TMessage, TResult>` | Consume and return a typed result (request-response). |
| `IBatchConsumer<TMessage>` | Process messages in batches. |
| `IFaultConsumer<TMessage>` | Handle faults (dead letter processing). |
| `ConsumeContext<TMessage>` | Provides the message, headers, and publish/send capabilities. |
| `IConsumerDefinition` | Optional: define endpoint name, retry, concurrency per consumer. |

### Sagas

| Type | Purpose |
|------|---------|
| `ISaga<TState, TMessage>` | Handle a message within saga state context. |
| `IInitiatedBy<TState, TMessage>` | Start a new saga instance from a message. |
| `IOrchestrates<TState, TEvent>` | Handle events in a saga. |
| `IObserves<TState, TEvent>` | Observe events without modifying state. |
| `ISagaState` | Marker for saga state objects. |
| `ISagaRepository<TState>` | Persistence for saga state. |
| `InMemorySagaRepository<TState>` | Built-in for testing. |
| `SagaContext<TState>` | Provides state + send/publish within saga handlers. |

### Pipeline

| Type | Purpose |
|------|---------|
| `IFilter<TContext>` | Middleware that intercepts message processing. |
| `IPipe<TContext>` | Next stage in the filter chain. |
| `PipeBuilder` | Composes filters into a pipeline. |
| `RetryFilter` | Retry failed messages. |
| `CircuitBreakerFilter` | Circuit breaker pattern. |
| `TimeoutFilter` | Timeout enforcement. |
| `ConcurrencyLimitFilter` | Limit concurrent processing. |
| `ExceptionFilter` | Catch and convert exceptions. |

### Transports

| Type | Purpose |
|------|---------|
| `ITransport` | Transport abstraction (start/stop, create endpoints). |
| `IReceiveEndpoint` | Receives messages from a queue/topic. |
| `ISendTransport` | Sends to a specific endpoint. |
| `IPublishTransport` | Publishes to all subscribers. |
| `InMemoryTransport` | Built-in in-memory transport for testing. |

### Serialization

| Type | Purpose |
|------|---------|
| `IMessageSerializer` | Serialize/deserialize messages. |
| `JsonMessageSerializer` | Default JSON serializer. |
| `MessageTypeResolver` | Resolves message types from type names. |

### Wide Events

| Type | Purpose |
|------|---------|
| `IMessageWideEventEmitter` | Emits wide events for message processing. |
| `MessageWideEvent` | Wide event data for message consumption. |
| `MessageWideEventBuilder` | Builder for constructing message wide events. |

## DI Registration

```csharp
services.AddVsaMessaging(cfg =>
{
    cfg.UseRabbitMq(opt => { opt.Host = "localhost"; });  // or UseKafka, UseAzureServiceBus
    cfg.AddConsumers<Program>();
    cfg.UseRetry(RetryPolicy.Exponential(3, TimeSpan.FromSeconds(1)));
    cfg.ReceiveEndpoint("order-queue", e => e.Consumer<OrderConsumer>());
});
```

For testing:
```csharp
services.AddVsaMessaging(); // Uses in-memory transport
```

## Dependency Chain

```
VsaResults
  ^-- VsaResults.Features
        ^-- VsaResults.Messaging (this project)
              ^-- VsaResults.Messaging.RabbitMq
              ^-- VsaResults.Messaging.Kafka
              ^-- VsaResults.Messaging.AzureServiceBus
              ^-- Modules.Provisioning
```

**Depends on:** `VsaResults`, `VsaResults.Features`, Microsoft.Extensions.DI/Hosting/Logging abstractions.

**Depended on by:** Transport packages, `Modules.Provisioning`, `Host` (transitively via transport packages).

## InternalsVisibleTo

- `VsaResults.Messaging.RabbitMq`
- `VsaResults.Messaging.Kafka`
- `VsaResults.Messaging.AzureServiceBus`

## Do NOT

- **Do not throw exceptions in consumers.** Return `VsaResult<Unit>` errors instead. The framework auto-publishes `Fault<TMessage>` on error.
- **Do not use `Task.Run` for background work.** Publish a command or event to the bus and let a consumer handle it.
- **Do not reference a specific transport package from this project.** This is the abstraction layer; transport packages depend on it, not the other way around.
- **Do not create consumers that depend on other consumers.** Each consumer is independent.
- **Do not store secrets in `RabbitMqTransportOptions` or similar in source code.** Use configuration/environment variables.
