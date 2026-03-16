using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using VsaResults.Messaging.DependencyInjection;
using VsaResults.Messaging.Transports;

namespace VsaResults.Messaging.RabbitMq;

/// <summary>
/// Extension methods for adding RabbitMQ transport to VsaResults.Messaging.
/// </summary>
public static class MessagingConfiguratorExtensions
{
    /// <summary>
    /// Configures the messaging system to use RabbitMQ as the transport.
    /// </summary>
    /// <param name="configurator">The messaging configurator.</param>
    /// <param name="configure">Action to configure RabbitMQ options.</param>
    /// <returns>The configurator for method chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddVsaMessaging(cfg =>
    /// {
    ///     cfg.UseRabbitMq(opt =>
    ///     {
    ///         opt.Host = "localhost";
    ///         opt.Port = 5672;
    ///         opt.Username = "guest";
    ///         opt.Password = "guest";
    ///     });
    ///
    ///     cfg.ReceiveEndpoint&lt;OrderCreatedConsumer&gt;();
    /// });
    /// </code>
    /// </example>
    public static IMessagingConfigurator UseRabbitMq(
        this IMessagingConfigurator configurator,
        Action<RabbitMqTransportOptions> configure)
    {
        var options = new RabbitMqTransportOptions();
        configure(options);

        // Register the transport through the internal extension point
        configurator.RegisterTransport(services =>
        {
            services.AddSingleton(options);
            services.TryAddSingleton<ITransport>(sp =>
            {
                var opts = sp.GetRequiredService<RabbitMqTransportOptions>();
                var logger = sp.GetService<ILogger<RabbitMqTransport>>();
                return new RabbitMqTransport(opts, sp, logger);
            });
        });

        return configurator;
    }
}
