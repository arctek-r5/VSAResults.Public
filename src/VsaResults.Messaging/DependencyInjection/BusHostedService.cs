using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VsaResults.Messaging.Bus;

namespace VsaResults.Messaging.DependencyInjection;

/// <summary>
/// Hosted service that manages the bus lifecycle.
/// Starts the bus when the application starts and stops it on shutdown.
/// </summary>
internal sealed class BusHostedService : IHostedService
{
    private readonly IBus _bus;
    private readonly ILogger<BusHostedService>? _logger;

    public BusHostedService(
        IBus bus,
        ILogger<BusHostedService>? logger = null)
    {
        _bus = bus;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Starting message bus...");

        var result = await _bus.StartAsync(cancellationToken);

        if (result.IsError)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger?.LogError("Failed to start message bus: {Errors}", errors);
            throw new InvalidOperationException($"Failed to start message bus: {errors}");
        }

        _logger?.LogInformation("Message bus started successfully at {Address}", _bus.Address);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Stopping message bus...");

        var result = await _bus.StopAsync(cancellationToken);

        if (result.IsError)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger?.LogWarning("Errors while stopping message bus: {Errors}", errors);
        }
        else
        {
            _logger?.LogInformation("Message bus stopped successfully");
        }
    }
}
