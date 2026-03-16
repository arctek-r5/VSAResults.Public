namespace VsaResults.Features.Features;

/// <summary>
/// Defines a query feature with validation, requirements, and execution.
/// Use for read-only operations that don't modify state.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <typeparam name="TResult">The type of the result.</typeparam>
public interface IQueryFeature<TRequest, TResult>
{
    /// <summary>
    /// Gets the validator for the incoming request. Defaults to no-op validation.
    /// </summary>
    IFeatureValidator<TRequest> Validator => NoOpValidator<TRequest>.Instance;

    /// <summary>
    /// Gets the requirements enforcer for authorization and entity loading. Defaults to no-op.
    /// </summary>
    IFeatureRequirements<TRequest> Requirements => NoOpRequirements<TRequest>.Instance;

    /// <summary>
    /// Gets the query executor.
    /// </summary>
    IFeatureQuery<TRequest, TResult> Query { get; }
}
