using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using VsaResults.Features.Features;
using VsaResults.Features.WideEvents.Unified;
using VsaResults.VsaResult;

namespace VsaResults.AspNetCore.AspNetCore;

/// <summary>
/// Base controller class that provides streamlined feature execution methods.
/// Inherit from this to get clean, one-liner feature execution in your controllers.
/// </summary>
/// <example>
/// <code>
/// public class UsersController : FeatureController
/// {
///     [HttpGet("{id}")]
///     public Task&lt;ActionResult&lt;UserDto&gt;&gt; GetUser(int id)
///         => QueryOk&lt;GetUser.Request, UserDto&gt;(new(id));
///
///     [HttpPost]
///     public Task&lt;ActionResult&lt;UserDto&gt;&gt; CreateUser(CreateUser.Request request)
///         => MutationCreated&lt;CreateUser.Request, UserDto&gt;(request, u => $"/users/{u.Id}");
///
///     [HttpDelete("{id}")]
///     public Task&lt;IActionResult&gt; DeleteUser(int id)
///         => MutationNoContentAsync&lt;DeleteUser.Request&gt;(new(id));
/// }
/// </code>
/// </example>
public abstract class FeatureController : ControllerBase
{
    /// <summary>
    /// Executes a query feature and returns OK (200) on success.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="request">The request to execute.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>ActionResult with the result or problem details.</returns>
    protected async Task<ActionResult<TResult>> QueryOk<TRequest, TResult>(TRequest request, CancellationToken ct = default)
    {
        var feature = HttpContext.RequestServices.GetRequiredService<IQueryFeature<TRequest, TResult>>();
        var emitter = HttpContext.RequestServices.GetRequiredService<IWideEventEmitter>();

        var result = await feature.ExecuteAsync(request, emitter, ct);
        return result.ToOkResult();
    }

    /// <summary>
    /// Executes a mutation feature and returns OK (200) on success.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="request">The request to execute.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>ActionResult with the result or problem details.</returns>
    protected async Task<ActionResult<TResult>> MutationOk<TRequest, TResult>(TRequest request, CancellationToken ct = default)
        where TRequest : notnull
    {
        var feature = HttpContext.RequestServices.GetRequiredService<IMutationFeature<TRequest, TResult>>();
        var emitter = HttpContext.RequestServices.GetRequiredService<IWideEventEmitter>();

        var result = await FeatureHandler.ComposeMutationFeature(feature, HttpContext.RequestServices)
            .ExecuteAsync(request, emitter, ct);
        return result.ToOkResult();
    }

    /// <summary>
    /// Executes a mutation feature and returns Created (201) on success.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="request">The request to execute.</param>
    /// <param name="locationSelector">Function to generate the location URI from the result.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>ActionResult with the result or problem details.</returns>
    protected async Task<ActionResult<TResult>> MutationCreated<TRequest, TResult>(
        TRequest request,
        Func<TResult, string> locationSelector,
        CancellationToken ct = default)
        where TRequest : notnull
    {
        var feature = HttpContext.RequestServices.GetRequiredService<IMutationFeature<TRequest, TResult>>();
        var emitter = HttpContext.RequestServices.GetRequiredService<IWideEventEmitter>();

        var result = await FeatureHandler.ComposeMutationFeature(feature, HttpContext.RequestServices)
            .ExecuteAsync(request, emitter, ct);
        return result.ToCreatedResult(locationSelector);
    }

    /// <summary>
    /// Executes a mutation feature and returns NoContent (204) on success.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <param name="request">The request to execute.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>IActionResult (NoContent or problem details).</returns>
    protected async Task<IActionResult> MutationNoContentAsync<TRequest>(TRequest request, CancellationToken ct = default)
        where TRequest : notnull
    {
        var feature = HttpContext.RequestServices.GetRequiredService<IMutationFeature<TRequest, Unit>>();
        var emitter = HttpContext.RequestServices.GetRequiredService<IWideEventEmitter>();

        var result = await FeatureHandler.ComposeMutationFeature(feature, HttpContext.RequestServices)
            .ExecuteAsync(request, emitter, ct);
        return result.ToNoContentResult();
    }

    /// <summary>
    /// Executes a query feature with a custom result mapper.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="request">The request to execute.</param>
    /// <param name="resultMapper">Function to map the result to an ActionResult.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>ActionResult mapped from the result.</returns>
    protected async Task<TResponse> QueryAsync<TRequest, TResult, TResponse>(
        TRequest request,
        Func<VsaResult<TResult>, TResponse> resultMapper,
        CancellationToken ct = default)
    {
        var feature = HttpContext.RequestServices.GetRequiredService<IQueryFeature<TRequest, TResult>>();
        var emitter = HttpContext.RequestServices.GetRequiredService<IWideEventEmitter>();

        var result = await feature.ExecuteAsync(request, emitter, ct);
        return resultMapper(result);
    }

    /// <summary>
    /// Executes a mutation feature with a custom result mapper.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="request">The request to execute.</param>
    /// <param name="resultMapper">Function to map the result to an ActionResult.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>ActionResult mapped from the result.</returns>
    protected async Task<TResponse> MutationAsync<TRequest, TResult, TResponse>(
        TRequest request,
        Func<VsaResult<TResult>, TResponse> resultMapper,
        CancellationToken ct = default)
        where TRequest : notnull
    {
        var feature = HttpContext.RequestServices.GetRequiredService<IMutationFeature<TRequest, TResult>>();
        var emitter = HttpContext.RequestServices.GetRequiredService<IWideEventEmitter>();

        var result = await FeatureHandler.ComposeMutationFeature(feature, HttpContext.RequestServices)
            .ExecuteAsync(request, emitter, ct);
        return resultMapper(result);
    }
}
