using VsaResults.VsaResult;

namespace VsaResults.Features.Features;

/// <summary>
/// Requirements enforcer that creates a basic context without loading any entities.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
public sealed class NoOpRequirements<TRequest> : IFeatureRequirements<TRequest>
{
    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static readonly NoOpRequirements<TRequest> Instance = new();

    private NoOpRequirements()
    {
    }

    /// <summary>
    /// Creates a basic feature context without loading any entities.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A feature context containing the request.</returns>
    public Task<VsaResult<FeatureContext<TRequest>>> EnforceAsync(TRequest request, CancellationToken ct = default)
        => Task.FromResult(new FeatureContext<TRequest> { Request = request }.ToResult());
}
