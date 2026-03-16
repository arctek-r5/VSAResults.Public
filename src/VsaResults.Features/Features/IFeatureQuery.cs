using JetBrains.Annotations;
using VsaResults.VsaResult;

namespace VsaResults.Features.Features;

/// <summary>
/// Executes a read-only query.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <typeparam name="TResult">The type of the result.</typeparam>
[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public interface IFeatureQuery<TRequest, TResult>
{
    /// <summary>
    /// Executes the query.
    /// </summary>
    /// <param name="context">The feature context containing the validated request and authorization state.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result or execution errors.</returns>
    Task<VsaResult<TResult>> ExecuteAsync(FeatureContext<TRequest> context, CancellationToken ct = default);
}
