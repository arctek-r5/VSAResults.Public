using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using VsaResults.Features.Features;
using VsaResults.Messaging.ErrorOr;
using VsaResults.Messaging.Transports;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.RabbitMq;

/// <summary>
/// RabbitMQ transport implementation with full connection management.
/// </summary>
public class RabbitMqTransport : ITransport
{
    private readonly RabbitMqTransportOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RabbitMqTransport>? _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly List<RabbitMqReceiveEndpoint> _receiveEndpoints = [];

    private IConnection? _connection;
    private IChannel? _publishChannel;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitMqTransport"/> class.
    /// </summary>
    public RabbitMqTransport(
        RabbitMqTransportOptions options,
        IServiceProvider serviceProvider,
        ILogger<RabbitMqTransport>? logger = null)
    {
        _options = options;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Scheme => "rabbitmq";

    /// <summary>
    /// Gets the shared connection, or null if not connected.
    /// </summary>
    internal IConnection? Connection => _connection;

    /// <inheritdoc />
    public async Task<VsaResult<IReceiveEndpoint>> CreateReceiveEndpointAsync(
        EndpointAddress address,
        Action<IReceiveEndpointConfigurator> configure,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();

        var configurator = new RabbitMqReceiveEndpointConfigurator(address);
        configure(configurator);

        var endpoint = new RabbitMqReceiveEndpoint(
            address,
            this,
            _options,
            _serviceProvider,
            configurator,
            _logger);

        _receiveEndpoints.Add(endpoint);

        VsaResult<IReceiveEndpoint> result = endpoint;
        return result;
    }

    /// <inheritdoc />
    public async Task<VsaResult<ISendTransport>> GetSendTransportAsync(
        EndpointAddress address,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();

        var connectResult = await EnsureConnectedAsync(ct);
        if (connectResult.IsError)
        {
            return connectResult.Errors.ToResult<ISendTransport>();
        }

        var channel = await CreateChannelAsync(ct);
        if (channel.IsError)
        {
            return channel.Errors.ToResult<ISendTransport>();
        }

        var transport = new RabbitMqSendTransport(address, channel.Value, _options, _logger);
        VsaResult<ISendTransport> result = transport;
        return result;
    }

    /// <inheritdoc />
    public async Task<VsaResult<IPublishTransport>> GetPublishTransportAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        var connectResult = await EnsureConnectedAsync(ct);
        if (connectResult.IsError)
        {
            return connectResult.Errors.ToResult<IPublishTransport>();
        }

        // Use a shared channel for publishing
        if (_publishChannel is null || _publishChannel.IsClosed)
        {
            var channelResult = await CreateChannelAsync(ct);
            if (channelResult.IsError)
            {
                return channelResult.Errors.ToResult<IPublishTransport>();
            }

            _publishChannel = channelResult.Value;
        }

        var transport = new RabbitMqPublishTransport(_publishChannel, _options, _logger);
        VsaResult<IPublishTransport> result = transport;
        return result;
    }

    /// <summary>
    /// Creates a new channel from the connection.
    /// </summary>
    internal async Task<VsaResult<IChannel>> CreateChannelAsync(CancellationToken ct = default)
    {
        if (_connection is null)
        {
            return MessagingErrors.TransportNotConnected();
        }

        try
        {
            var channel = await _connection.CreateChannelAsync(cancellationToken: ct);
            return channel.ToResult();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create RabbitMQ channel");
            return MessagingErrors.TransportError(ex.Message);
        }
    }

    /// <summary>
    /// Ensures a connection to RabbitMQ is established.
    /// </summary>
    internal async Task<VsaResult<Unit>> EnsureConnectedAsync(CancellationToken ct = default)
    {
        if (_connection is not null && _connection.IsOpen)
        {
            return Unit.Value;
        }

        await _connectionLock.WaitAsync(ct);
        try
        {
            if (_connection is not null && _connection.IsOpen)
            {
                return Unit.Value;
            }

            _logger?.LogInformation(
                "Connecting to RabbitMQ at {Host}:{Port}",
                _options.Host,
                _options.Port);

            var factory = new ConnectionFactory
            {
                HostName = _options.Host,
                Port = _options.Port,
                UserName = _options.Username,
                Password = _options.Password
            };

            _connection = await factory.CreateConnectionAsync(ct);

            _logger?.LogInformation("Connected to RabbitMQ successfully");
            return Unit.Value;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to connect to RabbitMQ at {Host}:{Port}", _options.Host, _options.Port);
            return MessagingErrors.TransportConnectionFailed(_options.Host, _options.Port, ex.Message);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (_isDisposed)
        {
            return;
        }

        // Stop all receive endpoints
        foreach (var endpoint in _receiveEndpoints)
        {
            await endpoint.DisposeAsync();
        }

        _receiveEndpoints.Clear();

        // Close channels and connection
        if (_publishChannel is not null)
        {
            await _publishChannel.CloseAsync();
            _publishChannel.Dispose();
        }

        if (_connection is not null)
        {
            await _connection.CloseAsync();
            _connection.Dispose();
        }

        _connectionLock.Dispose();
        _isDisposed = true;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, nameof(RabbitMqTransport));
    }

}
