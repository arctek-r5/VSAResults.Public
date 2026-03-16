using VsaResults.VsaResult;

namespace VsaResults.Features.Features;

/// <summary>
/// No-op permission check that always allows access.
/// Used as the default when no permission check is specified.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
public sealed class NoOpPermission<TRequest> : IFeaturePermission<TRequest>
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static readonly NoOpPermission<TRequest> Instance = new();

    /// <inheritdoc />
    public string Resource => "none";

    /// <inheritdoc />
    public string Action => "none";

    /// <inheritdoc />
    public Task<VsaResult<TRequest>> CheckAsync(TRequest request, CancellationToken ct = default)
        => Task.FromResult(request.ToResult());
}
