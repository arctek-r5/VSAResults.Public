using JetBrains.Annotations;
using VsaResults.VsaResult;

namespace VsaResults.Features.Features;

/// <summary>
/// Validates a request before processing.
/// Returns the validated request or validation errors.
/// </summary>
/// <typeparam name="TRequest">The type of the request to validate.</typeparam>
[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public interface IFeatureValidator<TRequest>
{
    /// <summary>
    /// Validates the request.
    /// </summary>
    /// <param name="request">The request to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The validated request or validation errors.</returns>
    Task<VsaResult<TRequest>> ValidateAsync(TRequest request, CancellationToken ct = default);
}
