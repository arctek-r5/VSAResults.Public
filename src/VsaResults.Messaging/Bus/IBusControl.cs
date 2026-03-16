using VsaResults.Features.Features;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Bus;

/// <summary>
/// Controls the bus lifecycle.
/// </summary>
public interface IBusControl
{
    /// <summary>
    /// Starts the bus and begins receiving messages.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Unit on success, or an error.</returns>
    Task<VsaResult<Unit>> StartAsync(CancellationToken ct = default);

    /// <summary>
    /// Stops the bus gracefully, allowing in-flight messages to complete.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Unit on success, or an error.</returns>
    Task<VsaResult<Unit>> StopAsync(CancellationToken ct = default);
}
