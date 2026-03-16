using VsaResults.VsaResult;

namespace VsaResults.Features.Features;

/// <summary>
/// A validator that performs no validation and passes the request through unchanged.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
public sealed class NoOpValidator<TRequest> : IFeatureValidator<TRequest>
{
    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static readonly NoOpValidator<TRequest> Instance = new();

    private NoOpValidator()
    {
    }

    /// <summary>
    /// Returns the request unchanged.
    /// </summary>
    /// <param name="request">The request to validate.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The request wrapped in an ErrorOr.</returns>
    public Task<VsaResult<TRequest>> ValidateAsync(TRequest request, CancellationToken ct = default)
        => Task.FromResult(request.ToResult());
}
