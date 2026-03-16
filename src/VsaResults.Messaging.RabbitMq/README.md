# VsaResults.Messaging.RabbitMq

RabbitMQ transport implementation for `VsaResults.Messaging`. Provides the `UseRabbitMq()` extension method to wire up RabbitMQ as the message transport.

## What This Is

A transport plugin that connects the `VsaResults.Messaging` bus to RabbitMQ via the `RabbitMQ.Client` library. Handles exchange/queue declaration, message serialization, consumer dispatch, and publisher confirms.

## Key Types

| Type | Purpose |
|------|---------|
| `RabbitMqTransport` | `ITransport` implementation: manages connection, creates send/receive endpoints. |
| `RabbitMqTransportOptions` | Configuration: host, port, virtual host, credentials, SSL, prefetch, durability, exchange type, publisher confirms. |
| `RabbitMqPublishTransport` | Publishes events to fanout exchanges. |
| `RabbitMqSendTransport` | Sends commands to specific queues. |
| `RabbitMqReceiveEndpoint` | Consumes messages from a queue with acknowledgment. |
| `RabbitMqDiagnostics` | Diagnostic logging for connection events. |
| `MessagingConfiguratorExtensions` | Provides `UseRabbitMq()` on `IMessagingConfigurator`. |

## Usage

```csharp
services.AddVsaMessaging(cfg =>
{
    cfg.UseRabbitMq(opt =>
    {
        opt.Host = "localhost";
        opt.Port = 5672;
        opt.Username = "guest";
        opt.Password = "guest";
        opt.PrefetchCount = 16;
        opt.PersistentMessages = true;
        opt.UsePublisherConfirms = true;
    });

    cfg.ReceiveEndpoint<OrderCreatedConsumer>();
});
```

## Configuration Defaults

| Option | Default |
|--------|---------|
| Host | `localhost` |
| Port | `5672` |
| VirtualHost | `/` |
| Username | `guest` |
| Password | `guest` |
| PrefetchCount | `16` |
| ConcurrentConsumers | `1` |
| PersistentMessages | `true` |
| Durable | `true` |
| AutoDelete | `false` |
| ExchangeType | `fanout` |
| UsePublisherConfirms | `true` |
| RetryCount | `5` |
| RetryDelay | `5s` |
| ConnectionTimeout | `30s` |

## Dependency Chain

```
VsaResults.Messaging
  ^-- VsaResults.Messaging.RabbitMq (this project)
        ^-- Host
        ^-- ProvisioningWorker
```

**Depends on:** `VsaResults.Messaging`, `RabbitMQ.Client`.

**Depended on by:** `Host`, `ProvisioningWorker`.

## Do NOT

- **Do not reference this project from Modules or Kernel.** Transport selection is a Host/Worker concern. Modules depend only on `VsaResults.Messaging`.
- **Do not hardcode connection strings.** Use configuration or Aspire-injected connection strings.
- **Do not create raw RabbitMQ channels or connections.** Use the `IBus` / `IPublishEndpoint` / `ISendEndpoint` abstractions from `VsaResults.Messaging`.
