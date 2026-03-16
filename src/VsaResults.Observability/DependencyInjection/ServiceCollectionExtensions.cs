using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VsaResults.Features.WideEvents.Unified;
using VsaResults.Observability.Interceptors;
using VsaResults.Observability.Masking;

namespace VsaResults.Observability.DependencyInjection;

/// <summary>
/// Extension methods for registering PII masking services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the default PII masking services with default options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPiiMasking(this IServiceCollection services)
    {
        return services.AddPiiMasking(_ => { });
    }

    /// <summary>
    /// Registers the default PII masking services with custom options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPiiMasking(
        this IServiceCollection services,
        Action<PiiMaskerOptions> configure)
    {
        services.Configure(configure);
        services.TryAddSingleton<IPiiMasker, DefaultPiiMasker>();

        return services;
    }

    /// <summary>
    /// Registers a custom PII masker implementation.
    /// </summary>
    /// <typeparam name="TMasker">The custom masker type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPiiMasking<TMasker>(this IServiceCollection services)
        where TMasker : class, IPiiMasker
    {
        services.AddSingleton<IPiiMasker, TMasker>();
        return services;
    }

    /// <summary>
    /// Registers a PII masker instance.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="masker">The masker instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPiiMasking(this IServiceCollection services, IPiiMasker masker)
    {
        services.AddSingleton(masker);
        return services;
    }

    /// <summary>
    /// Registers the null (no-op) PII masker. Use this when PII masking should be disabled.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddNullPiiMasking(this IServiceCollection services)
    {
        services.AddSingleton<IPiiMasker>(NullPiiMasker.Instance);
        return services;
    }

    /// <summary>
    /// Adds the PII masking interceptor to the wide events pipeline.
    /// Call this after <see cref="AddPiiMasking(IServiceCollection)"/> and
    /// <c>AddWideEvents()</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPiiMaskingInterceptor(this IServiceCollection services)
    {
        // Register the interceptor
        services.AddSingleton<PiiMaskingInterceptor>();
        services.AddSingleton<IWideEventInterceptor>(sp => sp.GetRequiredService<PiiMaskingInterceptor>());

        return services;
    }
}
