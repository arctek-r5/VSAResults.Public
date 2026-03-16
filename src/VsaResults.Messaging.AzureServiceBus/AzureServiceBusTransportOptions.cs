using Azure.Core;
using Azure.Messaging.ServiceBus;

namespace VsaResults.Messaging.AzureServiceBus;

/// <summary>
/// Configuration options for Azure Service Bus transport.
/// </summary>
public sealed class AzureServiceBusTransportOptions
{
    /// <summary>
    /// Gets or sets the Azure Service Bus connection string.
    /// Use this or FullyQualifiedNamespace with Credential for managed identity.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the fully qualified Service Bus namespace (e.g., "mybus.servicebus.windows.net").
    /// Use with Credential for managed identity authentication.
    /// </summary>
    public string? FullyQualifiedNamespace { get; set; }

    /// <summary>
    /// Gets or sets the token credential for managed identity authentication.
    /// Use with FullyQualifiedNamespace.
    /// </summary>
    public TokenCredential? Credential { get; set; }

    /// <summary>
    /// Gets or sets the receive mode for messages.
    /// Default: PeekLock (at-least-once delivery with explicit completion)
    /// </summary>
    public ServiceBusReceiveMode ReceiveMode { get; set; } = ServiceBusReceiveMode.PeekLock;

    /// <summary>
    /// Gets or sets the maximum number of concurrent calls to the message handler.
    /// Default: 1
    /// </summary>
    public int MaxConcurrentCalls { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of messages to prefetch.
    /// Default: 0 (no prefetch)
    /// </summary>
    public int PrefetchCount { get; set; } = 0;

    /// <summary>
    /// Gets or sets the maximum duration for automatic lock renewal.
    /// Default: 5 minutes
    /// </summary>
    public TimeSpan MaxAutoLockRenewalDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the default time-to-live for messages.
    /// Default: null (use Service Bus default)
    /// </summary>
    public TimeSpan? DefaultMessageTimeToLive { get; set; }

    /// <summary>
    /// Gets or sets whether to automatically create queues if they don't exist.
    /// Default: true (for development convenience; disable in production)
    /// </summary>
    public bool AutoCreateQueues { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to automatically create topics if they don't exist.
    /// Default: true (for development convenience; disable in production)
    /// </summary>
    public bool AutoCreateTopics { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to automatically create subscriptions if they don't exist.
    /// Default: true (for development convenience; disable in production)
    /// </summary>
    public bool AutoCreateSubscriptions { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of retry attempts for failed operations.
    /// Default: 5
    /// </summary>
    public int RetryCount { get; set; } = 5;

    /// <summary>
    /// Gets or sets the delay between retry attempts.
    /// Default: 5 seconds
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the maximum retry delay.
    /// Default: 30 seconds
    /// </summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the subscription name suffix for topic subscriptions.
    /// The full subscription name will be: {EndpointName}{SubscriptionSuffix}
    /// Default: "-sub"
    /// </summary>
    public string SubscriptionSuffix { get; set; } = "-sub";

    /// <summary>
    /// Gets or sets whether to enable sessions for FIFO message ordering.
    /// Default: false
    /// </summary>
    public bool EnableSessions { get; set; } = false;

    /// <summary>
    /// Gets or sets the identifier for this transport instance.
    /// Used for logging and diagnostics.
    /// </summary>
    public string? TransportIdentifier { get; set; }

    /// <summary>
    /// Builds ServiceBusClientOptions from these settings.
    /// </summary>
    internal ServiceBusClientOptions BuildClientOptions()
    {
        return new ServiceBusClientOptions
        {
            RetryOptions = new ServiceBusRetryOptions
            {
                MaxRetries = RetryCount,
                Delay = RetryDelay,
                MaxDelay = MaxRetryDelay,
                Mode = ServiceBusRetryMode.Exponential
            },
            TransportType = ServiceBusTransportType.AmqpTcp,
            Identifier = TransportIdentifier
        };
    }

    /// <summary>
    /// Builds ServiceBusProcessorOptions from these settings.
    /// </summary>
    internal ServiceBusProcessorOptions BuildProcessorOptions()
    {
        return new ServiceBusProcessorOptions
        {
            ReceiveMode = ReceiveMode,
            MaxConcurrentCalls = MaxConcurrentCalls,
            PrefetchCount = PrefetchCount,
            MaxAutoLockRenewalDuration = MaxAutoLockRenewalDuration,
            AutoCompleteMessages = false // We handle completion manually
        };
    }

    /// <summary>
    /// Gets a safe representation of the configuration for logging (without credentials).
    /// </summary>
    public string SafeConnectionString
    {
        get
        {
            if (!string.IsNullOrEmpty(FullyQualifiedNamespace))
            {
                return $"{FullyQualifiedNamespace} (Managed Identity)";
            }

            if (!string.IsNullOrEmpty(ConnectionString))
            {
                // Extract just the Endpoint from the connection string
                var parts = ConnectionString.Split(';');
                var endpoint = parts.FirstOrDefault(p => p.StartsWith("Endpoint=", StringComparison.OrdinalIgnoreCase));
                return endpoint ?? "***";
            }

            return "Not configured";
        }
    }

    /// <summary>
    /// Validates the options and throws if invalid.
    /// </summary>
    internal void Validate()
    {
        var hasConnectionString = !string.IsNullOrEmpty(ConnectionString);
        var hasManagedIdentity = !string.IsNullOrEmpty(FullyQualifiedNamespace) && Credential is not null;

        if (!hasConnectionString && !hasManagedIdentity)
        {
            throw new InvalidOperationException(
                "Azure Service Bus transport requires either a ConnectionString or " +
                "FullyQualifiedNamespace with Credential for authentication.");
        }

        if (hasConnectionString && hasManagedIdentity)
        {
            throw new InvalidOperationException(
                "Specify either ConnectionString or FullyQualifiedNamespace with Credential, not both.");
        }
    }
}
