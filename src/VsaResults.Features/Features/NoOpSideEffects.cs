using VsaResults.VsaResult;

namespace VsaResults.Features.Features;

/// <summary>
/// Side effects executor that does nothing.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
public sealed class NoOpSideEffects<TRequest> : IFeatureSideEffects<TRequest>
{
    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static readonly NoOpSideEffects<TRequest> Instance = new();

    private NoOpSideEffects()
    {
    }

    /// <summary>
    /// Returns success without executing any side effects.
    /// </summary>
    /// <param name="context">The feature context.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A successful Unit result.</returns>
    public Task<VsaResult<Unit>> ExecuteAsync(FeatureContext<TRequest> context, CancellationToken ct = default)
        => Task.FromResult(Unit.Value.ToResult());
}
