using Azure.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using VsaResults.Messaging.DependencyInjection;
using VsaResults.Messaging.Transports;

namespace VsaResults.Messaging.AzureServiceBus;

/// <summary>
/// Extension methods for adding Azure Service Bus transport to VsaResults.Messaging.
/// </summary>
public static class MessagingConfiguratorExtensions
{
    /// <summary>
    /// Configures the messaging system to use Azure Service Bus as the transport.
    /// </summary>
    /// <param name="configurator">The messaging configurator.</param>
    /// <param name="configure">Action to configure Azure Service Bus options.</param>
    /// <returns>The configurator for method chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddVsaMessaging(cfg =>
    /// {
    ///     cfg.UseAzureServiceBus(opt =>
    ///     {
    ///         opt.ConnectionString = "Endpoint=sb://...;SharedAccessKey=...";
    ///         opt.MaxConcurrentCalls = 10;
    ///     });
    ///
    ///     cfg.ReceiveEndpoint&lt;OrderCreatedConsumer&gt;();
    /// });
    /// </code>
    /// </example>
    public static IMessagingConfigurator UseAzureServiceBus(
        this IMessagingConfigurator configurator,
        Action<AzureServiceBusTransportOptions> configure)
    {
        var options = new AzureServiceBusTransportOptions();
        configure(options);

        // Register the transport through the internal extension point
        configurator.RegisterTransport(services =>
        {
            services.AddSingleton(options);
            services.TryAddSingleton<ITransport>(sp =>
            {
                var opts = sp.GetRequiredService<AzureServiceBusTransportOptions>();
                var logger = sp.GetService<ILogger<AzureServiceBusTransport>>();
                return new AzureServiceBusTransport(opts, sp, logger);
            });
        });

        return configurator;
    }

    /// <summary>
    /// Configures the messaging system to use Azure Service Bus with a connection string.
    /// </summary>
    /// <param name="configurator">The messaging configurator.</param>
    /// <param name="connectionString">The Azure Service Bus connection string.</param>
    /// <param name="configure">Optional action to configure additional options.</param>
    /// <returns>The configurator for method chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddVsaMessaging(cfg =>
    /// {
    ///     cfg.UseAzureServiceBus("Endpoint=sb://mybus.servicebus.windows.net;SharedAccessKeyName=...;SharedAccessKey=...");
    ///     cfg.ReceiveEndpoint&lt;OrderCreatedConsumer&gt;();
    /// });
    /// </code>
    /// </example>
    public static IMessagingConfigurator UseAzureServiceBus(
        this IMessagingConfigurator configurator,
        string connectionString,
        Action<AzureServiceBusTransportOptions>? configure = null)
    {
        return configurator.UseAzureServiceBus(opt =>
        {
            opt.ConnectionString = connectionString;
            configure?.Invoke(opt);
        });
    }

    /// <summary>
    /// Configures the messaging system to use Azure Service Bus with managed identity.
    /// </summary>
    /// <param name="configurator">The messaging configurator.</param>
    /// <param name="fullyQualifiedNamespace">The fully qualified Service Bus namespace (e.g., "mybus.servicebus.windows.net").</param>
    /// <param name="credential">The token credential for authentication (e.g., DefaultAzureCredential).</param>
    /// <param name="configure">Optional action to configure additional options.</param>
    /// <returns>The configurator for method chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddVsaMessaging(cfg =>
    /// {
    ///     cfg.UseAzureServiceBus(
    ///         "mybus.servicebus.windows.net",
    ///         new DefaultAzureCredential());
    ///     cfg.ReceiveEndpoint&lt;OrderCreatedConsumer&gt;();
    /// });
    /// </code>
    /// </example>
    public static IMessagingConfigurator UseAzureServiceBus(
        this IMessagingConfigurator configurator,
        string fullyQualifiedNamespace,
        TokenCredential credential,
        Action<AzureServiceBusTransportOptions>? configure = null)
    {
        return configurator.UseAzureServiceBus(opt =>
        {
            opt.FullyQualifiedNamespace = fullyQualifiedNamespace;
            opt.Credential = credential;
            configure?.Invoke(opt);
        });
    }
}
