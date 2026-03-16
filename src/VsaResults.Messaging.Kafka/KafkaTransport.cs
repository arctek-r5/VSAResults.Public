using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using VsaResults.Features.Features;
using VsaResults.Messaging.ErrorOr;
using VsaResults.Messaging.Transports;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Kafka;

/// <summary>
/// Apache Kafka transport implementation.
/// </summary>
public class KafkaTransport : ITransport
{
    private readonly KafkaTransportOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<KafkaTransport>? _logger;
    private readonly SemaphoreSlim _producerLock = new(1, 1);
    private readonly List<KafkaReceiveEndpoint> _receiveEndpoints = [];

    private IProducer<string, byte[]>? _producer;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="KafkaTransport"/> class.
    /// </summary>
    public KafkaTransport(
        KafkaTransportOptions options,
        IServiceProvider serviceProvider,
        ILogger<KafkaTransport>? logger = null)
    {
        _options = options;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Scheme => "kafka";

    /// <summary>
    /// Gets the shared producer instance.
    /// </summary>
    internal IProducer<string, byte[]>? Producer => _producer;

    /// <inheritdoc />
    public async Task<VsaResult<IReceiveEndpoint>> CreateReceiveEndpointAsync(
        EndpointAddress address,
        Action<IReceiveEndpointConfigurator> configure,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();

        var configurator = new KafkaReceiveEndpointConfigurator(address);
        configure(configurator);

        var endpoint = new KafkaReceiveEndpoint(
            address,
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

        var producerResult = await EnsureProducerAsync(ct);
        if (producerResult.IsError)
        {
            return producerResult.Errors.ToResult<ISendTransport>();
        }

        var transport = new KafkaSendTransport(address, _producer!, _logger);
        VsaResult<ISendTransport> result = transport;
        return result;
    }

    /// <inheritdoc />
    public async Task<VsaResult<IPublishTransport>> GetPublishTransportAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        var producerResult = await EnsureProducerAsync(ct);
        if (producerResult.IsError)
        {
            return producerResult.Errors.ToResult<IPublishTransport>();
        }

        var transport = new KafkaPublishTransport(_producer!, _logger);
        VsaResult<IPublishTransport> result = transport;
        return result;
    }

    /// <summary>
    /// Ensures the producer is initialized.
    /// </summary>
    internal async Task<VsaResult<Unit>> EnsureProducerAsync(CancellationToken ct = default)
    {
        if (_producer is not null)
        {
            return Unit.Value;
        }

        await _producerLock.WaitAsync(ct);
        try
        {
            if (_producer is not null)
            {
                return Unit.Value;
            }

            _logger?.LogInformation(
                "Creating Kafka producer for {BootstrapServers}",
                _options.SafeConnectionString);

            var config = _options.BuildProducerConfig();
            _producer = new ProducerBuilder<string, byte[]>(config)
                .SetErrorHandler((_, error) =>
                {
                    _logger?.LogError(
                        "Kafka producer error: {ErrorCode} - {Reason}",
                        error.Code,
                        error.Reason);
                })
                .SetLogHandler((_, log) =>
                {
                    _logger?.LogDebug(
                        "Kafka producer log: [{Level}] {Message}",
                        log.Level,
                        log.Message);
                })
                .Build();

            _logger?.LogInformation("Kafka producer created successfully");
            return Unit.Value;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create Kafka producer");
            return MessagingErrors.TransportError($"Failed to create Kafka producer: {ex.Message}");
        }
        finally
        {
            _producerLock.Release();
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

        // Dispose producer
        if (_producer is not null)
        {
            try
            {
                // Flush any pending messages before disposing
                _producer.Flush(TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error flushing Kafka producer during dispose");
            }

            _producer.Dispose();
            _producer = null;
        }

        _producerLock.Dispose();
        _isDisposed = true;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, nameof(KafkaTransport));
    }
}
