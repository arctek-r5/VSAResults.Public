using VsaResults.Features.Features;
using VsaResults.Messaging.Messages;
using VsaResults.Messaging.Transports;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Bus;

/// <summary>
/// Interface for sending commands to a specific endpoint.
/// Commands are delivered to exactly one consumer.
/// </summary>
public interface ISendEndpoint
{
    /// <summary>Gets the endpoint address.</summary>
    EndpointAddress Address { get; }

    /// <summary>
    /// Sends a command to this endpoint.
    /// </summary>
    /// <typeparam name="TMessage">The command type.</typeparam>
    /// <param name="message">The command to send.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Unit on success, or errors on failure.</returns>
    Task<VsaResult<Unit>> SendAsync<TMessage>(
        TMessage message,
        CancellationToken ct = default)
        where TMessage : class, ICommand;

    /// <summary>
    /// Sends a command with custom header configuration.
    /// </summary>
    /// <typeparam name="TMessage">The command type.</typeparam>
    /// <param name="message">The command to send.</param>
    /// <param name="configureHeaders">Header configuration callback.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Unit on success, or errors on failure.</returns>
    Task<VsaResult<Unit>> SendAsync<TMessage>(
        TMessage message,
        Action<MessageHeaders> configureHeaders,
        CancellationToken ct = default)
        where TMessage : class, ICommand;

    /// <summary>
    /// Sends a command with a specific correlation ID.
    /// </summary>
    /// <typeparam name="TMessage">The command type.</typeparam>
    /// <param name="message">The command to send.</param>
    /// <param name="correlationId">The correlation ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Unit on success, or errors on failure.</returns>
    Task<VsaResult<Unit>> SendAsync<TMessage>(
        TMessage message,
        CorrelationId correlationId,
        CancellationToken ct = default)
        where TMessage : class, ICommand;
}

/// <summary>
/// Provider for obtaining send endpoints.
/// </summary>
public interface ISendEndpointProvider
{
    /// <summary>
    /// Gets a send endpoint for the specified address.
    /// </summary>
    /// <param name="address">The destination address.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The send endpoint or an error.</returns>
    Task<VsaResult<ISendEndpoint>> GetSendEndpointAsync(
        EndpointAddress address,
        CancellationToken ct = default);
}
