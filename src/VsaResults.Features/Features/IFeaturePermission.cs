using VsaResults.VsaResult;

namespace VsaResults.Features.Features;

/// <summary>
/// Checks permission/authorization before processing a request.
/// This is the first stage in the pipeline, before validation.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
public interface IFeaturePermission<TRequest>
{
    /// <summary>
    /// Gets the resource kind being accessed (e.g., "kubernetes", "billing", "tenant").
    /// </summary>
    string Resource { get; }

    /// <summary>
    /// Gets the action being performed (e.g., "view", "edit", "delete", "restart_pod").
    /// </summary>
    string Action { get; }

    /// <summary>
    /// Checks if the current user has permission to perform this action.
    /// </summary>
    /// <param name="request">The request (may contain resource ID).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The request if authorized, or a forbidden error.</returns>
    Task<VsaResult<TRequest>> CheckAsync(TRequest request, CancellationToken ct = default);
}
