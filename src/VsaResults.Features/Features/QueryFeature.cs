namespace VsaResults.Features.Features;

/// <summary>
/// Base class for query feature orchestrators that eliminates boilerplate wiring.
/// Inheritors need only call the base constructor with injected dependencies.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <typeparam name="TResult">The type of the result.</typeparam>
/// <example>
/// <code>
/// public sealed class Feature(
///     IFeatureValidator&lt;Request&gt; validator,
///     IFeatureQuery&lt;Request, Response&gt; query)
///     : QueryFeature&lt;Request, Response&gt;(validator, query: query), IScopedDependency;
/// </code>
/// </example>
public abstract class QueryFeature<TRequest, TResult>(
    IFeatureValidator<TRequest>? validator = null,
    IFeatureRequirements<TRequest>? requirements = null,
    IFeatureQuery<TRequest, TResult>? query = null)
    : IQueryFeature<TRequest, TResult>
{
    IFeatureValidator<TRequest> IQueryFeature<TRequest, TResult>.Validator =>
        validator ?? NoOpValidator<TRequest>.Instance;

    IFeatureRequirements<TRequest> IQueryFeature<TRequest, TResult>.Requirements =>
        requirements ?? NoOpRequirements<TRequest>.Instance;

    IFeatureQuery<TRequest, TResult> IQueryFeature<TRequest, TResult>.Query =>
        query ?? throw new InvalidOperationException("Query is required for query features.");
}
