using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using VsaResults.Messaging.DependencyInjection;
using VsaResults.Messaging.Transports;

namespace VsaResults.Messaging.Kafka;

/// <summary>
/// Extension methods for adding Apache Kafka transport to VsaResults.Messaging.
/// </summary>
public static class MessagingConfiguratorExtensions
{
    /// <summary>
    /// Configures the messaging system to use Apache Kafka as the transport.
    /// </summary>
    /// <param name="configurator">The messaging configurator.</param>
    /// <param name="configure">Action to configure Kafka options.</param>
    /// <returns>The configurator for method chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddVsaMessaging(cfg =>
    /// {
    ///     cfg.UseKafka(opt =>
    ///     {
    ///         opt.BootstrapServers = "localhost:9092";
    ///         opt.GroupId = "my-consumer-group";
    ///         opt.Acks = Acks.All;
    ///     });
    ///
    ///     cfg.ReceiveEndpoint&lt;OrderCreatedConsumer&gt;();
    /// });
    /// </code>
    /// </example>
    public static IMessagingConfigurator UseKafka(
        this IMessagingConfigurator configurator,
        Action<KafkaTransportOptions> configure)
    {
        var options = new KafkaTransportOptions();
        configure(options);

        // Register the transport through the internal extension point
        configurator.RegisterTransport(services =>
        {
            services.AddSingleton(options);
            services.TryAddSingleton<ITransport>(sp =>
            {
                var opts = sp.GetRequiredService<KafkaTransportOptions>();
                var logger = sp.GetService<ILogger<KafkaTransport>>();
                return new KafkaTransport(opts, sp, logger);
            });
        });

        return configurator;
    }

    /// <summary>
    /// Configures the messaging system to use Apache Kafka as the transport with a bootstrap servers string.
    /// </summary>
    /// <param name="configurator">The messaging configurator.</param>
    /// <param name="bootstrapServers">The Kafka bootstrap servers (e.g., "localhost:9092").</param>
    /// <param name="configure">Optional action to configure additional Kafka options.</param>
    /// <returns>The configurator for method chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddVsaMessaging(cfg =>
    /// {
    ///     cfg.UseKafka("localhost:9092");
    ///     cfg.ReceiveEndpoint&lt;OrderCreatedConsumer&gt;();
    /// });
    /// </code>
    /// </example>
    public static IMessagingConfigurator UseKafka(
        this IMessagingConfigurator configurator,
        string bootstrapServers,
        Action<KafkaTransportOptions>? configure = null)
    {
        return configurator.UseKafka(opt =>
        {
            opt.BootstrapServers = bootstrapServers;
            configure?.Invoke(opt);
        });
    }
}
