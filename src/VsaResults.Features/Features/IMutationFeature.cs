namespace VsaResults.Features.Features;

/// <summary>
/// Defines a mutation feature with validation, requirements, execution, and optional side effects.
/// Use for operations that modify state (create, update, delete).
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <typeparam name="TResult">The type of the result.</typeparam>
public interface IMutationFeature<TRequest, TResult>
{
    /// <summary>
    /// Gets the validator for the incoming request. Defaults to no-op validation.
    /// </summary>
    IFeatureValidator<TRequest> Validator => NoOpValidator<TRequest>.Instance;

    /// <summary>
    /// Gets the requirements enforcer that loads entities. Defaults to no-op requirements.
    /// </summary>
    IFeatureRequirements<TRequest> Requirements => NoOpRequirements<TRequest>.Instance;

    /// <summary>
    /// Gets the mutator that executes the core mutation logic.
    /// </summary>
    IFeatureMutator<TRequest, TResult> Mutator { get; }

    /// <summary>
    /// Gets the side effects handler that runs after successful mutation. Defaults to no-op.
    /// </summary>
    IFeatureSideEffects<TRequest> SideEffects => NoOpSideEffects<TRequest>.Instance;
}
