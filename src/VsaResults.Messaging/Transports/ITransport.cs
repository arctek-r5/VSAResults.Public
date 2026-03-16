using VsaResults.VsaResult;

namespace VsaResults.Messaging.Transports;

/// <summary>
/// Transport abstraction for messaging infrastructure.
/// Provides a consistent interface for different messaging backends.
/// </summary>
public interface ITransport : IAsyncDisposable
{
    /// <summary>Gets the transport scheme (e.g., "inmemory", "rabbitmq").</summary>
    string Scheme { get; }

    /// <summary>
    /// Creates a receive endpoint for consuming messages.
    /// </summary>
    /// <param name="address">The endpoint address.</param>
    /// <param name="configure">Configuration callback.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The receive endpoint or an error.</returns>
    Task<VsaResult<IReceiveEndpoint>> CreateReceiveEndpointAsync(
        EndpointAddress address,
        Action<IReceiveEndpointConfigurator> configure,
        CancellationToken ct = default);

    /// <summary>
    /// Gets a send transport for an address.
    /// </summary>
    /// <param name="address">The destination address.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The send transport or an error.</returns>
    Task<VsaResult<ISendTransport>> GetSendTransportAsync(
        EndpointAddress address,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the publish transport for broadcasting events.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The publish transport or an error.</returns>
    Task<VsaResult<IPublishTransport>> GetPublishTransportAsync(
        CancellationToken ct = default);
}
