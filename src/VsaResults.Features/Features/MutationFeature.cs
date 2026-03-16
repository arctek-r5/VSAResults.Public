namespace VsaResults.Features.Features;

/// <summary>
/// Base class for mutation feature orchestrators that eliminates boilerplate wiring.
/// Inheritors need only call the base constructor with injected dependencies.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <typeparam name="TResult">The type of the result.</typeparam>
/// <example>
/// <code>
/// public sealed class Feature(
///     IFeatureValidator&lt;Request&gt; validator,
///     IFeatureRequirements&lt;Request&gt; requirements,
///     IFeatureMutator&lt;Request, Response&gt; mutator)
///     : MutationFeature&lt;Request, Response&gt;(validator, requirements, mutator), IScopedDependency;
/// </code>
/// </example>
public abstract class MutationFeature<TRequest, TResult>(
    IFeatureValidator<TRequest>? validator = null,
    IFeatureRequirements<TRequest>? requirements = null,
    IFeatureMutator<TRequest, TResult>? mutator = null,
    IFeatureSideEffects<TRequest>? sideEffects = null)
    : IMutationFeature<TRequest, TResult>
{
    /// <summary>
    /// Eagerly validated mutator reference — fails at DI resolution (construction) time
    /// rather than at feature execution time if mutator is not provided.
    /// </summary>
    private readonly IFeatureMutator<TRequest, TResult> _mutator = mutator
        ?? throw new ArgumentNullException(nameof(mutator), "Mutator is required for mutation features. Ensure it is registered in DI and injected into the feature constructor.");

    IFeatureValidator<TRequest> IMutationFeature<TRequest, TResult>.Validator =>
        validator ?? NoOpValidator<TRequest>.Instance;

    IFeatureRequirements<TRequest> IMutationFeature<TRequest, TResult>.Requirements =>
        requirements ?? NoOpRequirements<TRequest>.Instance;

    IFeatureMutator<TRequest, TResult> IMutationFeature<TRequest, TResult>.Mutator => _mutator;

    IFeatureSideEffects<TRequest> IMutationFeature<TRequest, TResult>.SideEffects =>
        sideEffects ?? NoOpSideEffects<TRequest>.Instance;
}
