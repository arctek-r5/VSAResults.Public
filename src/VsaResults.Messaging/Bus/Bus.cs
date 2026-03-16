using System.Collections.Concurrent;
using VsaResults.Features.Features;
using VsaResults.Messaging.ErrorOr;
using VsaResults.Messaging.Messages;
using VsaResults.Messaging.Serialization;
using VsaResults.Messaging.Transports;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Bus;

/// <summary>
/// Default bus implementation.
/// </summary>
internal sealed class Bus : IBus
{
    private readonly ITransport _transport;
    private readonly IMessageSerializer _serializer;
    private readonly MessageTypeResolver _typeResolver;
    private readonly ConcurrentDictionary<string, IReceiveEndpoint> _endpoints = new();
    private readonly ConcurrentDictionary<string, ISendEndpoint> _sendEndpoints = new();

    private IPublishTransport? _publishTransport;
    private bool _isStarted;

    public Bus(
        ITransport transport,
        IMessageSerializer serializer,
        MessageTypeResolver typeResolver)
    {
        _transport = transport;
        _serializer = serializer;
        _typeResolver = typeResolver;

        Address = EndpointAddress.FromUri(new Uri($"{transport.Scheme}://localhost/bus"));
    }

    /// <inheritdoc />
    public EndpointAddress Address { get; }

    /// <inheritdoc />
    public async Task<VsaResult<Unit>> StartAsync(CancellationToken ct = default)
    {
        if (_isStarted)
        {
            return Unit.Value;
        }

        var publishResult = await _transport.GetPublishTransportAsync(ct);
        if (publishResult.IsError)
        {
            return publishResult.Errors.ToResult<Unit>();
        }

        _publishTransport = publishResult.Value;

        foreach (var endpoint in _endpoints.Values)
        {
            var startResult = await endpoint.StartAsync(ct);
            if (startResult.IsError)
            {
                return startResult.Errors.ToResult<Unit>();
            }
        }

        _isStarted = true;
        return Unit.Value;
    }

    /// <inheritdoc />
    public async Task<VsaResult<Unit>> StopAsync(CancellationToken ct = default)
    {
        if (!_isStarted)
        {
            return Unit.Value;
        }

        foreach (var endpoint in _endpoints.Values)
        {
            await endpoint.StopAsync(ct);
        }

        _isStarted = false;
        return Unit.Value;
    }

    /// <inheritdoc />
    public Task<VsaResult<Unit>> PublishAsync<TMessage>(
        TMessage message,
        CancellationToken ct = default)
        where TMessage : class, IEvent
        => PublishAsync(message, _ => { }, ct);

    /// <inheritdoc />
    public async Task<VsaResult<Unit>> PublishAsync<TMessage>(
        TMessage message,
        Action<MessageHeaders> configureHeaders,
        CancellationToken ct = default)
        where TMessage : class, IEvent
    {
        if (_publishTransport is null)
        {
            return MessagingErrors.BusNotStarted();
        }

        var serializeResult = _serializer.Serialize(message);
        if (serializeResult.IsError)
        {
            return serializeResult.Errors.ToResult<Unit>();
        }

        var headers = new MessageHeaders();
        configureHeaders(headers);

        // Parse InitiatorId from headers if present
        MessageId? initiatorId = null;
        if (headers.InitiatorId is not null)
        {
            var parseResult = MessageId.Parse(headers.InitiatorId);
            if (!parseResult.IsError)
            {
                initiatorId = parseResult.Value;
            }
        }

        var envelope = new MessageEnvelope
        {
            MessageId = MessageId.New(),
            CorrelationId = CorrelationId.New(),
            InitiatorId = initiatorId,
            MessageTypes = _typeResolver.GetMessageTypes<TMessage>(),
            Body = serializeResult.Value,
            Headers = headers,
            Host = HostInfo.Current
        };

        return await _publishTransport.PublishAsync<TMessage>(envelope, ct);
    }

    /// <inheritdoc />
    public Task<VsaResult<Unit>> PublishAsync<TMessage>(
        TMessage message,
        CorrelationId correlationId,
        CancellationToken ct = default)
        where TMessage : class, IEvent
    {
        return PublishAsync(message, _ => { }, ct);
    }

    /// <inheritdoc />
    public async Task<VsaResult<ISendEndpoint>> GetSendEndpointAsync(
        EndpointAddress address,
        CancellationToken ct = default)
    {
        var key = address.ToString();

        if (_sendEndpoints.TryGetValue(key, out var cached))
        {
            return cached.ToResult();
        }

        var transportResult = await _transport.GetSendTransportAsync(address, ct);
        if (transportResult.IsError)
        {
            return transportResult.Errors.ToResult<ISendEndpoint>();
        }

        var endpoint = new SendEndpoint(
            transportResult.Value,
            _serializer,
            _typeResolver);

        _sendEndpoints.TryAdd(key, endpoint);
        return endpoint;
    }

    internal void AddReceiveEndpoint(IReceiveEndpoint endpoint)
    {
        _endpoints.TryAdd(endpoint.Name, endpoint);
    }
}

/// <summary>
/// Send endpoint implementation.
/// </summary>
internal sealed class SendEndpoint : ISendEndpoint
{
    private readonly ISendTransport _transport;
    private readonly IMessageSerializer _serializer;
    private readonly MessageTypeResolver _typeResolver;

    public SendEndpoint(
        ISendTransport transport,
        IMessageSerializer serializer,
        MessageTypeResolver typeResolver)
    {
        _transport = transport;
        _serializer = serializer;
        _typeResolver = typeResolver;
    }

    /// <inheritdoc />
    public EndpointAddress Address => _transport.Address;

    /// <inheritdoc />
    public Task<VsaResult<Unit>> SendAsync<TMessage>(
        TMessage message,
        CancellationToken ct = default)
        where TMessage : class, ICommand
        => SendAsync(message, _ => { }, ct);

    /// <inheritdoc />
    public async Task<VsaResult<Unit>> SendAsync<TMessage>(
        TMessage message,
        Action<MessageHeaders> configureHeaders,
        CancellationToken ct = default)
        where TMessage : class, ICommand
    {
        var serializeResult = _serializer.Serialize(message);
        if (serializeResult.IsError)
        {
            return serializeResult.Errors.ToResult<Unit>();
        }

        var headers = new MessageHeaders();
        configureHeaders(headers);

        // Parse InitiatorId from headers if present
        MessageId? initiatorId = null;
        if (headers.InitiatorId is not null)
        {
            var parseResult = MessageId.Parse(headers.InitiatorId);
            if (!parseResult.IsError)
            {
                initiatorId = parseResult.Value;
            }
        }

        var envelope = new MessageEnvelope
        {
            MessageId = MessageId.New(),
            CorrelationId = CorrelationId.New(),
            InitiatorId = initiatorId,
            MessageTypes = _typeResolver.GetMessageTypes<TMessage>(),
            Body = serializeResult.Value,
            Headers = headers,
            DestinationAddress = Address,
            Host = HostInfo.Current
        };

        return await _transport.SendAsync<TMessage>(envelope, ct);
    }

    /// <inheritdoc />
    public Task<VsaResult<Unit>> SendAsync<TMessage>(
        TMessage message,
        CorrelationId correlationId,
        CancellationToken ct = default)
        where TMessage : class, ICommand
    {
        return SendAsync(message, _ => { }, ct);
    }
}
