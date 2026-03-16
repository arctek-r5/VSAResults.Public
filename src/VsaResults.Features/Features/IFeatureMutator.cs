using JetBrains.Annotations;
using VsaResults.VsaResult;

namespace VsaResults.Features.Features;

/// <summary>
/// Executes the core mutation logic.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <typeparam name="TResult">The type of the result.</typeparam>
[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public interface IFeatureMutator<TRequest, TResult>
{
    /// <summary>
    /// Executes the mutation.
    /// </summary>
    /// <param name="context">The feature context with request and loaded entities.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result or execution errors.</returns>
    Task<VsaResult<TResult>> ExecuteAsync(FeatureContext<TRequest> context, CancellationToken ct = default);
}
