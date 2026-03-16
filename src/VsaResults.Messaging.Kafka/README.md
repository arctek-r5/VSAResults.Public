# VsaResults.Messaging.Kafka

Apache Kafka transport implementation for `VsaResults.Messaging`. Provides the `UseKafka()` extension method to wire up Kafka as the message transport.

## What This Is

A transport plugin that connects the `VsaResults.Messaging` bus to Apache Kafka via the `Confluent.Kafka` library. Handles topic creation, producer/consumer setup, and message serialization.

## Key Types

| Type | Purpose |
|------|---------|
| `KafkaTransport` | `ITransport` implementation: manages producer/consumer instances. |
| `KafkaTransportOptions` | Configuration: bootstrap servers, group ID, acks, security, etc. |
| `KafkaPublishTransport` | Publishes events to Kafka topics. |
| `KafkaSendTransport` | Sends commands to specific topics. |
| `KafkaReceiveEndpoint` | Consumes messages from a topic partition. |
| `MessagingConfiguratorExtensions` | Provides `UseKafka()` on `IMessagingConfigurator`. |

## Usage

```csharp
services.AddVsaMessaging(cfg =>
{
    cfg.UseKafka(opt =>
    {
        opt.BootstrapServers = "localhost:9092";
        opt.GroupId = "my-consumer-group";
    });

    cfg.ReceiveEndpoint<OrderCreatedConsumer>();
});

// Shorthand:
services.AddVsaMessaging(cfg =>
{
    cfg.UseKafka("localhost:9092");
});
```

## Dependency Chain

```
VsaResults.Messaging
  ^-- VsaResults.Messaging.Kafka (this project)
```

**Depends on:** `VsaResults.Messaging`, `Confluent.Kafka`.

**Depended on by:** Any host or worker that chooses Kafka as its transport.

## Do NOT

- **Do not reference this project from Modules or Kernel.** Transport selection is a Host/Worker concern.
- **Do not mix Kafka and RabbitMQ transports in the same bus.** Choose one transport per bus instance.
- **Do not create raw Kafka producers/consumers.** Use the `IBus` abstractions.
