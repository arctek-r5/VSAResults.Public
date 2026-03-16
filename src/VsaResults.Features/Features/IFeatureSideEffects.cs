using JetBrains.Annotations;
using VsaResults.VsaResult;

namespace VsaResults.Features.Features;

/// <summary>
/// Executes side effects after successful mutation.
/// Examples: sending notifications, publishing events, updating caches.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public interface IFeatureSideEffects<TRequest>
{
    /// <summary>
    /// Executes side effects.
    /// </summary>
    /// <param name="context">The feature context with request and loaded entities.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Unit on success or side effect errors.</returns>
    Task<VsaResult<Unit>> ExecuteAsync(FeatureContext<TRequest> context, CancellationToken ct = default);
}
