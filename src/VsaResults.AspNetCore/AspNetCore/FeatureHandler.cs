using Microsoft.AspNetCore.Http;
using VsaResults.AspNetCore.Binding;
using VsaResults.Features.Features;
using VsaResults.Features.WideEvents.Unified;
using VsaResults.VsaResult;

namespace VsaResults.AspNetCore.AspNetCore;

/// <summary>
/// Static handler factory for executing features from Minimal API endpoints.
/// Provides a clean, closure-free way to wire up feature execution.
/// </summary>
/// <example>
/// <code>
/// // Minimal API - simple GET
/// app.MapGet("/users/{id}", FeatureHandler.Query&lt;GetUser.Request, UserDto&gt;(
///     req => ApiResults.Ok(req)));
///
/// // Minimal API - POST with Created response
/// app.MapPost("/users", FeatureHandler.Mutation&lt;CreateUser.Request, UserDto&gt;(
///     (req, result) => ApiResults.Created(result, $"/users/{result.Value.Id}")));
///
/// // With manual binding (emits wide events for binding failures)
/// app.MapGet("/users/{id}", FeatureHandler.QueryOkBound&lt;GetUser.Request, UserDto&gt;());
/// </code>
/// </example>
public static class FeatureHandler
{
    /// <summary>
    /// Creates a delegate handler for a query feature that returns OK on success.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <returns>A delegate that can be used with MapGet/MapPost.</returns>
    public static Delegate QueryOk<TRequest, TResult>()
        where TRequest : notnull =>
        async (
            [AsParameters] TRequest request,
            IQueryFeature<TRequest, TResult> feature,
            IWideEventEmitter emitter,
            CancellationToken ct) =>
        {
            var result = await feature.ExecuteAsync(request, emitter, ct);
            return ApiResults.Ok(result);
        };

    /// <summary>
    /// Creates a delegate handler for a query feature with a custom result mapper.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="resultMapper">Function to map the successful result to an IResult.</param>
    /// <returns>A delegate that can be used with MapGet/MapPost.</returns>
    public static Delegate Query<TRequest, TResult>(Func<VsaResult<TResult>, IResult> resultMapper)
        where TRequest : notnull =>
        async (
            [AsParameters] TRequest request,
            IQueryFeature<TRequest, TResult> feature,
            IWideEventEmitter emitter,
            CancellationToken ct) =>
        {
            var result = await feature.ExecuteAsync(request, emitter, ct);
            return resultMapper(result);
        };

    /// <summary>
    /// Creates a delegate handler for a mutation feature that returns OK on success.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <returns>A delegate that can be used with MapPost/MapPut/MapDelete.</returns>
    public static Delegate MutationOk<TRequest, TResult>()
        where TRequest : notnull =>
        async (
            [AsParameters] TRequest request,
            IMutationFeature<TRequest, TResult> feature,
            IWideEventEmitter emitter,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var result = await ComposeMutationFeature(feature, httpContext.RequestServices).ExecuteAsync(request, emitter, ct);
            return ApiResults.Ok(result);
        };

    /// <summary>
    /// Creates a delegate handler for a mutation feature that returns Created on success.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="locationSelector">Function to generate the location URI from the result.</param>
    /// <returns>A delegate that can be used with MapPost.</returns>
    public static Delegate MutationCreated<TRequest, TResult>(Func<TResult, string> locationSelector)
        where TRequest : notnull =>
        async (
            [AsParameters] TRequest request,
            IMutationFeature<TRequest, TResult> feature,
            IWideEventEmitter emitter,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var result = await ComposeMutationFeature(feature, httpContext.RequestServices).ExecuteAsync(request, emitter, ct);
            return ApiResults.Created(result, locationSelector);
        };

    /// <summary>
    /// Creates a delegate handler for a mutation feature that returns NoContent on success.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <returns>A delegate that can be used with MapPut/MapDelete.</returns>
    public static Delegate MutationNoContent<TRequest>()
        where TRequest : notnull =>
        async (
            [AsParameters] TRequest request,
            IMutationFeature<TRequest, Unit> feature,
            IWideEventEmitter emitter,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var result = await ComposeMutationFeature(feature, httpContext.RequestServices).ExecuteAsync(request, emitter, ct);
            return ApiResults.NoContent(result);
        };

    /// <summary>
    /// Creates a delegate handler for a mutation feature with a custom result mapper.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="resultMapper">Function to map the successful result to an IResult.</param>
    /// <returns>A delegate that can be used with MapPost/MapPut/MapDelete.</returns>
    public static Delegate Mutation<TRequest, TResult>(Func<VsaResult<TResult>, IResult> resultMapper)
        where TRequest : notnull =>
        async (
            [AsParameters] TRequest request,
            IMutationFeature<TRequest, TResult> feature,
            IWideEventEmitter emitter,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var result = await ComposeMutationFeature(feature, httpContext.RequestServices).ExecuteAsync(request, emitter, ct);
            return resultMapper(result);
        };

    // ==========================================================================
    // Bound handlers - use manual binding for full wide event coverage
    // ==========================================================================

    /// <summary>
    /// Creates a delegate handler for a query feature with manual binding.
    /// Emits wide events for binding failures, providing full observability.
    /// </summary>
    public static Delegate QueryOkBound<TRequest, TResult>()
        where TRequest : notnull =>
        async (
            HttpContext httpContext,
            IQueryFeature<TRequest, TResult> feature,
            IWideEventEmitter emitter,
            CancellationToken ct) =>
        {
            var featureName = ResolveFeatureName(feature.GetType());
            return await BindAndExecuteAsync<TRequest, TResult>(
                httpContext, emitter, featureName, "Query",
                async request =>
                {
                    var result = await feature.ExecuteAsync(request, emitter, ct);
                    return ApiResults.Ok(result);
                }, ct);
        };

    /// <summary>
    /// Creates a delegate handler for a mutation feature with manual binding.
    /// Returns a <see cref="MutationPayload{T}"/> envelope on success.
    /// Emits wide events for binding failures, providing full observability.
    /// </summary>
    public static Delegate MutationOkBound<TRequest, TResult>()
        where TRequest : notnull =>
        async (
            HttpContext httpContext,
            IMutationFeature<TRequest, TResult> feature,
            IWideEventEmitter emitter,
            CancellationToken ct) =>
        {
            var featureName = ResolveFeatureName(feature.GetType());
            var action = MutationPayload.ToKebabCase(featureName);
            return await BindAndExecuteAsync<TRequest, TResult>(
                httpContext, emitter, featureName, "Mutation",
                async request =>
                {
                    var result = await ComposeMutationFeature(feature, httpContext.RequestServices).ExecuteAsync(request, emitter, ct);
                    return ApiResults.MutationOk(result, action);
                }, ct);
        };

    /// <summary>
    /// Creates a delegate handler for a mutation feature with manual binding that returns Created (201).
    /// Returns a <see cref="MutationPayload{T}"/> envelope on success.
    /// </summary>
    public static Delegate MutationCreatedBound<TRequest, TResult>(Func<TResult, string> locationSelector)
        where TRequest : notnull =>
        async (
            HttpContext httpContext,
            IMutationFeature<TRequest, TResult> feature,
            IWideEventEmitter emitter,
            CancellationToken ct) =>
        {
            var featureName = ResolveFeatureName(feature.GetType());
            var action = MutationPayload.ToKebabCase(featureName);
            return await BindAndExecuteAsync<TRequest, TResult>(
                httpContext, emitter, featureName, "Mutation",
                async request =>
                {
                    var result = await ComposeMutationFeature(feature, httpContext.RequestServices).ExecuteAsync(request, emitter, ct);
                    return ApiResults.MutationCreated(result, action);
                }, ct);
        };

    /// <summary>
    /// Creates a delegate handler for a mutation feature with manual binding that returns Accepted (202).
    /// Used for async operations where the work is dispatched to a saga.
    /// Accepted responses are intentionally NOT wrapped in the mutation envelope —
    /// the operation is still in progress and the receipt will come later.
    /// </summary>
    public static Delegate MutationAcceptedBound<TRequest, TResult>()
        where TRequest : notnull =>
        async (
            HttpContext httpContext,
            IMutationFeature<TRequest, TResult> feature,
            IWideEventEmitter emitter,
            CancellationToken ct) =>
        {
            var featureName = ResolveFeatureName(feature.GetType());
            return await BindAndExecuteAsync<TRequest, TResult>(
                httpContext, emitter, featureName, "Mutation",
                async request =>
                {
                    var result = await ComposeMutationFeature(feature, httpContext.RequestServices).ExecuteAsync(request, emitter, ct);
                    return ApiResults.Accepted(result);
                }, ct);
        };

    /// <summary>
    /// Creates a delegate handler for a mutation feature with manual binding.
    /// Returns a <see cref="MutationPayload{T}"/> envelope with no data on success
    /// (replaces 204 NoContent with 200 OK + receipt body).
    /// </summary>
    public static Delegate MutationNoContentBound<TRequest>()
        where TRequest : notnull =>
        async (
            HttpContext httpContext,
            IMutationFeature<TRequest, Unit> feature,
            IWideEventEmitter emitter,
            CancellationToken ct) =>
        {
            var featureName = ResolveFeatureName(feature.GetType());
            var action = MutationPayload.ToKebabCase(featureName);
            return await BindAndExecuteAsync<TRequest, Unit>(
                httpContext, emitter, featureName, "Mutation",
                async request =>
                {
                    var result = await ComposeMutationFeature(feature, httpContext.RequestServices).ExecuteAsync(request, emitter, ct);
                    return ApiResults.MutationNoContent(result, action);
                }, ct);
        };

    internal static IMutationFeature<TRequest, TResult> ComposeMutationFeature<TRequest, TResult>(
        IMutationFeature<TRequest, TResult> feature,
        IServiceProvider services)
        where TRequest : notnull
    {
        var defaultSideEffects = services.GetService(typeof(IFeatureSideEffects<TRequest>)) as IFeatureSideEffects<TRequest>;
        if (defaultSideEffects is null)
        {
            return feature;
        }

        var featureSideEffects = feature.SideEffects;
        IFeatureSideEffects<TRequest> effectiveSideEffects = featureSideEffects switch
        {
            NoOpSideEffects<TRequest> => defaultSideEffects,
            _ when featureSideEffects.GetType() == defaultSideEffects.GetType() => featureSideEffects,
            _ => new CompositeSideEffects<TRequest>(defaultSideEffects, featureSideEffects),
        };

        return new MutationFeatureWithSideEffects<TRequest, TResult>(feature, effectiveSideEffects);
    }

    private sealed class MutationFeatureWithSideEffects<TRequest, TResult>(
        IMutationFeature<TRequest, TResult> inner,
        IFeatureSideEffects<TRequest> sideEffects)
        : IMutationFeature<TRequest, TResult>
    {
        public IFeatureValidator<TRequest> Validator => inner.Validator;
        public IFeatureRequirements<TRequest> Requirements => inner.Requirements;
        public IFeatureMutator<TRequest, TResult> Mutator => inner.Mutator;
        public IFeatureSideEffects<TRequest> SideEffects => sideEffects;
    }

    private sealed class CompositeSideEffects<TRequest>(
        IFeatureSideEffects<TRequest> defaultSideEffects,
        IFeatureSideEffects<TRequest> customSideEffects)
        : IFeatureSideEffects<TRequest>
    {
        public async Task<VsaResult<Unit>> ExecuteAsync(FeatureContext<TRequest> context, CancellationToken ct = default)
        {
            var defaultResult = await defaultSideEffects.ExecuteAsync(context, ct);
            if (defaultResult.IsError)
            {
                return defaultResult;
            }

            return await customSideEffects.ExecuteAsync(context, ct);
        }
    }

    /// <summary>
    /// Handles request binding with wide event observability, then delegates to the feature execution.
    /// </summary>
    private static async Task<IResult> BindAndExecuteAsync<TRequest, TResult>(
        HttpContext httpContext,
        IWideEventEmitter emitter,
        string featureName,
        string featureType,
        Func<TRequest, Task<IResult>> execute,
        CancellationToken ct)
        where TRequest : notnull
    {
        var wideEventBuilder = WideEvent.StartFeature(featureName, featureType)
            .WithTypes<TRequest, TResult>();

        var bindingContext = RequestBinder.GetBindingContext<TRequest>(httpContext);
        foreach (var (key, value) in bindingContext)
        {
            wideEventBuilder.WithContext(key, value);
        }

        wideEventBuilder.StartStage("binding", typeof(RequestBinder), "BindAsync");
        var bindingResult = await RequestBinder.BindAsync<TRequest>(httpContext, ct);
        wideEventBuilder.RecordBinding();

        if (bindingResult.IsError)
        {
            var wideEvent = wideEventBuilder.BindingFailure(bindingResult.Errors);
            await emitter.EmitAsync(wideEvent, ct).ConfigureAwait(false);
            var action = featureType == "Mutation" ? MutationPayload.ToKebabCase(featureName) : null;
            return action is not null
                ? ApiResults.ToProblem(bindingResult.Errors, action)
                : ApiResults.ToProblem(bindingResult.Errors);
        }

        wideEventBuilder.WithRequestContext(bindingResult.Value);
        return await execute(bindingResult.Value);
    }

    private static string ResolveFeatureName(Type featureType) =>
        featureType.Name == "Feature" && featureType.DeclaringType is not null
            ? featureType.DeclaringType.Name
            : featureType.Name;
}
