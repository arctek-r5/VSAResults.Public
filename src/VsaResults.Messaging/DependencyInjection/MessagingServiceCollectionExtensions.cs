using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VsaResults.Messaging.Consumers;
using VsaResults.Messaging.Faults;
using VsaResults.Messaging.Serialization;

namespace VsaResults.Messaging.DependencyInjection;

/// <summary>
/// Extension methods for registering VsaResults.Messaging services with dependency injection.
/// </summary>
public static class MessagingServiceCollectionExtensions
{
    /// <summary>
    /// Adds VsaResults.Messaging services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddVsaMessaging(cfg =>
    /// {
    ///     cfg.UseInMemoryTransport();
    ///     cfg.AddConsumers&lt;Program&gt;();
    ///     cfg.UseRetry(RetryPolicy.Exponential(3, TimeSpan.FromSeconds(1)));
    ///     cfg.ReceiveEndpoint("order-queue", e => e.Consumer&lt;OrderConsumer&gt;());
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddVsaMessaging(
        this IServiceCollection services,
        Action<IMessagingConfigurator> configure)
    {
        // Register core services
        services.TryAddSingleton<IMessageSerializer, JsonMessageSerializer>();
        services.TryAddSingleton<MessageTypeResolver>();

        // Create and configure the builder
        var configurator = new MessagingConfigurator(services);
        configure(configurator);

        // Build and register the bus
        configurator.Build();

        return services;
    }

    /// <summary>
    /// Adds VsaResults.Messaging services with default in-memory transport.
    /// Useful for testing scenarios.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVsaMessaging(this IServiceCollection services)
    {
        return services.AddVsaMessaging(cfg =>
        {
            cfg.UseInMemoryTransport();
        });
    }

    /// <summary>
    /// Adds consumers from the specified assemblies.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">The assemblies to scan.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMessagingConsumers(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        foreach (var assembly in assemblies)
        {
            RegisterConsumersFromAssembly(services, assembly);
        }

        return services;
    }

    /// <summary>
    /// Adds consumers from the assembly containing the specified type.
    /// </summary>
    /// <typeparam name="T">A type in the assembly to scan.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMessagingConsumers<T>(this IServiceCollection services) =>
        services.AddMessagingConsumers(typeof(T).Assembly);

    private static void RegisterConsumersFromAssembly(IServiceCollection services, Assembly assembly)
    {
        var types = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false, IsClass: true });

        foreach (var type in types)
        {
            RegisterIfImplementsConsumerInterface(services, type);
        }
    }

    private static void RegisterIfImplementsConsumerInterface(IServiceCollection services, Type type)
    {
        var interfaces = type.GetInterfaces();

        foreach (var @interface in interfaces)
        {
            if (!@interface.IsGenericType)
            {
                continue;
            }

            var genericDef = @interface.GetGenericTypeDefinition();

            // Register consumers
            if (genericDef == typeof(IConsumer<>))
            {
                services.TryAddScoped(type);
                services.TryAddScoped(@interface, type);
            }

            // Register batch consumers
            if (genericDef == typeof(IBatchConsumer<>))
            {
                services.TryAddScoped(type);
                services.TryAddScoped(@interface, type);
            }

            // Register fault consumers
            if (genericDef == typeof(IFaultConsumer<>))
            {
                services.TryAddScoped(type);
                services.TryAddScoped(@interface, type);
            }
        }
    }
}
