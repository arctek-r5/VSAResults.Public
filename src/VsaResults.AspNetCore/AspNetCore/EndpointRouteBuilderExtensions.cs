using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using VsaResults.VsaResult;

namespace VsaResults.AspNetCore.AspNetCore;

/// <summary>
/// Extension methods for mapping feature endpoints with minimal boilerplate.
/// </summary>
/// <remarks>
/// <para>
/// All mutation methods automatically include OpenAPI metadata for 400 (Bad Request)
/// and 401 (Unauthorized) — the standard error responses every mutation can produce.
/// Features only need to chain additional metadata for special cases (e.g., 404, 403).
/// </para>
/// <para>
/// <b>[AsParameters] vs Bound:</b> Methods ending in <c>Bound</c> use manual model binding
/// from <c>HttpContext</c>, which emits wide events on binding failures. Use <c>Bound</c>
/// variants when the request is a <c>record</c> with <c>[property: FromRoute]</c> attributes.
/// Non-Bound methods use ASP.NET's built-in <c>[AsParameters]</c> binding.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // [AsParameters] binding — request is a class with [FromRoute] properties
/// app.MapGetFeature&lt;GetUser.Request, UserDto&gt;("/users/{id}");
/// app.MapPostFeature&lt;CreateUser.Request, UserDto&gt;("/users", result => $"/users/{result.Id}");
/// app.MapPutFeature&lt;UpdateUser.Request, UserDto&gt;("/users/{id}");
/// app.MapDeleteFeature&lt;DeleteUser.Request&gt;("/users/{id}");
///
/// // Bound (manual binding) — request is a record with [property: FromRoute]
/// app.MapPostFeatureOkBound&lt;BlockUser.Request, BlockUser.Response&gt;("/users/{id}/block");
/// app.MapGetFeatureBound&lt;GetTenant.Request, TenantDto&gt;("/tenants/{id}");
/// </code>
/// </example>
public static class EndpointRouteBuilderExtensions
{
    // ==========================================================================
    // [AsParameters] binding — ASP.NET auto-binds the request
    // ==========================================================================

    /// <summary>
    /// Maps a GET endpoint for a query feature. Returns OK (200) on success.
    /// </summary>
    public static RouteHandlerBuilder MapGetFeature<TRequest, TResult>(
        this IEndpointRouteBuilder endpoints,
        string pattern)
        where TRequest : notnull =>
        endpoints.MapGet(pattern, FeatureHandler.QueryOk<TRequest, TResult>())
            .WithQueryDefaults();

    /// <summary>
    /// Maps a GET endpoint for a query feature with a custom result mapper.
    /// </summary>
    public static RouteHandlerBuilder MapGetFeature<TRequest, TResult>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<VsaResult<TResult>, IResult> resultMapper)
        where TRequest : notnull =>
        endpoints.MapGet(pattern, FeatureHandler.Query<TRequest, TResult>(resultMapper))
            .WithQueryDefaults();

    /// <summary>
    /// Maps a POST endpoint for a mutation feature. Returns Created (201) on success.
    /// </summary>
    public static RouteHandlerBuilder MapPostFeature<TRequest, TResult>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<TResult, string> locationSelector)
        where TRequest : notnull =>
        endpoints.MapPost(pattern, FeatureHandler.MutationCreated<TRequest, TResult>(locationSelector))
            .WithMutationDefaults();

    /// <summary>
    /// Maps a POST endpoint for a mutation feature. Returns OK (200) on success.
    /// </summary>
    public static RouteHandlerBuilder MapPostFeatureOk<TRequest, TResult>(
        this IEndpointRouteBuilder endpoints,
        string pattern)
        where TRequest : notnull =>
        endpoints.MapPost(pattern, FeatureHandler.MutationOk<TRequest, TResult>())
            .WithMutationDefaults();

    /// <summary>
    /// Maps a PUT endpoint for a mutation feature. Returns OK (200) on success.
    /// </summary>
    public static RouteHandlerBuilder MapPutFeature<TRequest, TResult>(
        this IEndpointRouteBuilder endpoints,
        string pattern)
        where TRequest : notnull =>
        endpoints.MapPut(pattern, FeatureHandler.MutationOk<TRequest, TResult>())
            .WithMutationDefaults();

    /// <summary>
    /// Maps a PUT endpoint for a mutation feature with a custom result mapper.
    /// </summary>
    public static RouteHandlerBuilder MapPutFeature<TRequest, TResult>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<VsaResult<TResult>, IResult> resultMapper)
        where TRequest : notnull =>
        endpoints.MapPut(pattern, FeatureHandler.Mutation<TRequest, TResult>(resultMapper))
            .WithMutationDefaults();

    /// <summary>
    /// Maps a DELETE endpoint for a mutation feature. Returns NoContent (204) on success.
    /// </summary>
    public static RouteHandlerBuilder MapDeleteFeature<TRequest>(
        this IEndpointRouteBuilder endpoints,
        string pattern)
        where TRequest : notnull =>
        endpoints.MapDelete(pattern, FeatureHandler.MutationNoContent<TRequest>())
            .WithMutationDefaults();

    /// <summary>
    /// Maps a DELETE endpoint for a mutation feature that returns a result. Returns OK (200) on success.
    /// </summary>
    public static RouteHandlerBuilder MapDeleteFeature<TRequest, TResult>(
        this IEndpointRouteBuilder endpoints,
        string pattern)
        where TRequest : notnull =>
        endpoints.MapDelete(pattern, FeatureHandler.MutationOk<TRequest, TResult>())
            .WithMutationDefaults();

    /// <summary>
    /// Maps a PATCH endpoint for a mutation feature. Returns OK (200) on success.
    /// </summary>
    public static RouteHandlerBuilder MapPatchFeature<TRequest, TResult>(
        this IEndpointRouteBuilder endpoints,
        string pattern)
        where TRequest : notnull =>
        endpoints.MapPatch(pattern, FeatureHandler.MutationOk<TRequest, TResult>())
            .WithMutationDefaults();

    // ==========================================================================
    // Bound (manual binding) — emits wide events on binding failures
    // ==========================================================================

    /// <summary>
    /// Maps a GET endpoint for a query feature with manual binding. Returns OK (200) on success.
    /// </summary>
    public static RouteHandlerBuilder MapGetFeatureBound<TRequest, TResult>(
        this IEndpointRouteBuilder endpoints,
        string pattern)
        where TRequest : notnull =>
        endpoints.MapGet(pattern, FeatureHandler.QueryOkBound<TRequest, TResult>())
            .WithQueryDefaults();

    /// <summary>
    /// Maps a POST endpoint for a mutation feature with manual binding. Returns OK (200) on success.
    /// </summary>
    public static RouteHandlerBuilder MapPostFeatureOkBound<TRequest, TResult>(
        this IEndpointRouteBuilder endpoints,
        string pattern)
        where TRequest : notnull =>
        endpoints.MapPost(pattern, FeatureHandler.MutationOkBound<TRequest, TResult>())
            .WithMutationDefaults();

    /// <summary>
    /// Maps a POST endpoint for a mutation feature with manual binding. Returns Created (201) on success.
    /// </summary>
    public static RouteHandlerBuilder MapPostFeatureCreatedBound<TRequest, TResult>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<TResult, string> locationSelector)
        where TRequest : notnull =>
        endpoints.MapPost(pattern, FeatureHandler.MutationCreatedBound<TRequest, TResult>(locationSelector))
            .WithMutationDefaults();

    /// <summary>
    /// Maps a POST endpoint for a mutation feature with manual binding. Returns Accepted (202) on success.
    /// Used for async operations dispatched to sagas.
    /// </summary>
    public static RouteHandlerBuilder MapPostFeatureAcceptedBound<TRequest, TResult>(
        this IEndpointRouteBuilder endpoints,
        string pattern)
        where TRequest : notnull =>
        endpoints.MapPost(pattern, FeatureHandler.MutationAcceptedBound<TRequest, TResult>())
            .WithMutationDefaults();

    /// <summary>
    /// Maps a POST endpoint for a mutation feature with manual binding. Returns NoContent (204) on success.
    /// </summary>
    public static RouteHandlerBuilder MapPostFeatureNoContentBound<TRequest>(
        this IEndpointRouteBuilder endpoints,
        string pattern)
        where TRequest : notnull =>
        endpoints.MapPost(pattern, FeatureHandler.MutationNoContentBound<TRequest>())
            .WithMutationDefaults();

    /// <summary>
    /// Maps a PUT endpoint for a mutation feature with manual binding. Returns OK (200) on success.
    /// </summary>
    public static RouteHandlerBuilder MapPutFeatureBound<TRequest, TResult>(
        this IEndpointRouteBuilder endpoints,
        string pattern)
        where TRequest : notnull =>
        endpoints.MapPut(pattern, FeatureHandler.MutationOkBound<TRequest, TResult>())
            .WithMutationDefaults();

    /// <summary>
    /// Maps a DELETE endpoint for a mutation feature with manual binding. Returns OK (200) on success.
    /// </summary>
    public static RouteHandlerBuilder MapDeleteFeatureBound<TRequest, TResult>(
        this IEndpointRouteBuilder endpoints,
        string pattern)
        where TRequest : notnull =>
        endpoints.MapDelete(pattern, FeatureHandler.MutationOkBound<TRequest, TResult>())
            .WithMutationDefaults();

    /// <summary>
    /// Maps a DELETE endpoint for a mutation feature with manual binding. Returns NoContent (204) on success.
    /// </summary>
    public static RouteHandlerBuilder MapDeleteFeatureNoContentBound<TRequest>(
        this IEndpointRouteBuilder endpoints,
        string pattern)
        where TRequest : notnull =>
        endpoints.MapDelete(pattern, FeatureHandler.MutationNoContentBound<TRequest>())
            .WithMutationDefaults();

    /// <summary>
    /// Maps a PATCH endpoint for a mutation feature with manual binding. Returns OK (200) on success.
    /// </summary>
    public static RouteHandlerBuilder MapPatchFeatureBound<TRequest, TResult>(
        this IEndpointRouteBuilder endpoints,
        string pattern)
        where TRequest : notnull =>
        endpoints.MapPatch(pattern, FeatureHandler.MutationOkBound<TRequest, TResult>())
            .WithMutationDefaults();

    // ==========================================================================
    // Default OpenAPI metadata — applied automatically by the DSL methods above.
    // Features only need to chain additional metadata for special cases.
    // ==========================================================================

    /// <summary>
    /// Applies default OpenAPI metadata for mutation endpoints: 400 (validation) and 401 (unauthorized).
    /// </summary>
    private static RouteHandlerBuilder WithMutationDefaults(this RouteHandlerBuilder builder) =>
        builder
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

    /// <summary>
    /// Applies default OpenAPI metadata for query endpoints: 401 (unauthorized).
    /// </summary>
    private static RouteHandlerBuilder WithQueryDefaults(this RouteHandlerBuilder builder) =>
        builder
            .ProducesProblem(StatusCodes.Status401Unauthorized);
}
