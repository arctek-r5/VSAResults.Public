# VsaResults.Messaging.AzureServiceBus

Azure Service Bus transport implementation for `VsaResults.Messaging`. Provides the `UseAzureServiceBus()` extension method to wire up Azure Service Bus as the message transport.

## What This Is

A transport plugin that connects the `VsaResults.Messaging` bus to Azure Service Bus via the `Azure.Messaging.ServiceBus` SDK. Supports both connection string and managed identity authentication.

## Key Types

| Type | Purpose |
|------|---------|
| `AzureServiceBusTransport` | `ITransport` implementation: manages `ServiceBusClient` lifecycle. |
| `AzureServiceBusTransportOptions` | Configuration: connection string, namespace, credential, concurrency, lock duration, etc. |
| `AzureServiceBusPublishTransport` | Publishes events to Service Bus topics. |
| `AzureServiceBusSendTransport` | Sends commands to Service Bus queues. |
| `AzureServiceBusReceiveEndpoint` | Consumes messages from a queue or subscription. |
| `MessagingConfiguratorExtensions` | Provides `UseAzureServiceBus()` on `IMessagingConfigurator`. |

## Usage

```csharp
// Connection string auth:
services.AddVsaMessaging(cfg =>
{
    cfg.UseAzureServiceBus("Endpoint=sb://mybus.servicebus.windows.net;SharedAccessKey=...");
    cfg.ReceiveEndpoint<OrderCreatedConsumer>();
});

// Managed identity auth:
services.AddVsaMessaging(cfg =>
{
    cfg.UseAzureServiceBus(
        "mybus.servicebus.windows.net",
        new DefaultAzureCredential());
    cfg.ReceiveEndpoint<OrderCreatedConsumer>();
});

// Full options:
services.AddVsaMessaging(cfg =>
{
    cfg.UseAzureServiceBus(opt =>
    {
        opt.ConnectionString = "Endpoint=sb://...";
        opt.MaxConcurrentCalls = 10;
    });
});
```

## Dependency Chain

```
VsaResults.Messaging
  ^-- VsaResults.Messaging.AzureServiceBus (this project)
```

**Depends on:** `VsaResults.Messaging`, `Azure.Messaging.ServiceBus`, `Azure.Identity`.

**Depended on by:** Any host or worker that chooses Azure Service Bus as its transport.

## Do NOT

- **Do not reference this project from Modules or Kernel.** Transport selection is a Host/Worker concern.
- **Do not create raw `ServiceBusClient` or `ServiceBusSender` instances.** Use the `IBus` abstractions.
- **Do not store connection strings in source code.** Use Azure Key Vault, environment variables, or Aspire configuration.
