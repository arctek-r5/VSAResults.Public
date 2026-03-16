using VsaResults.Messaging.Transports;

namespace VsaResults.Messaging.Messages;

/// <summary>
/// Wire format envelope wrapping messages with metadata.
/// The envelope contains all the information needed to route, trace, and deliver a message.
/// </summary>
public sealed record MessageEnvelope
{
    /// <summary>Gets the unique message identifier.</summary>
    public required MessageId MessageId { get; init; }

    /// <summary>Gets the correlation identifier for tracing related messages.</summary>
    public required CorrelationId CorrelationId { get; init; }

    /// <summary>Gets the conversation identifier grouping message exchanges.</summary>
    public ConversationId? ConversationId { get; init; }

    /// <summary>Gets the message ID that initiated this message.</summary>
    public MessageId? InitiatorId { get; init; }

    /// <summary>Gets the source address where the message originated.</summary>
    public EndpointAddress? SourceAddress { get; init; }

    /// <summary>Gets the destination address for the message.</summary>
    public EndpointAddress? DestinationAddress { get; init; }

    /// <summary>Gets the response address for request-response patterns.</summary>
    public EndpointAddress? ResponseAddress { get; init; }

    /// <summary>Gets the fault address for error handling.</summary>
    public EndpointAddress? FaultAddress { get; init; }

    /// <summary>Gets the message type identifiers (URN format).</summary>
    public required IReadOnlyList<string> MessageTypes { get; init; }

    /// <summary>Gets the message body as serialized content.</summary>
    public required byte[] Body { get; init; }

    /// <summary>Gets the message headers.</summary>
    public MessageHeaders Headers { get; init; } = [];

    /// <summary>Gets the timestamp when the message was sent.</summary>
    public DateTimeOffset SentTime { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Gets the message expiration time.</summary>
    public DateTimeOffset? ExpirationTime { get; init; }

    /// <summary>Gets the content type of the body.</summary>
    public string ContentType { get; init; } = "application/json";

    /// <summary>Gets the host information where the message was created.</summary>
    public HostInfo? Host { get; init; }

    /// <summary>
    /// Creates a new envelope for a message.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="message">The message body.</param>
    /// <param name="messageTypes">The message type identifiers.</param>
    /// <param name="serializedBody">The serialized message body.</param>
    /// <param name="correlationId">Optional correlation ID (generates new if not provided).</param>
    /// <returns>A new message envelope.</returns>
    public static MessageEnvelope Create<TMessage>(
        TMessage message,
        IReadOnlyList<string> messageTypes,
        byte[] serializedBody,
        CorrelationId? correlationId = null)
        where TMessage : class, IMessage
    {
        return new MessageEnvelope
        {
            MessageId = MessageId.New(),
            CorrelationId = correlationId ?? CorrelationId.New(),
            MessageTypes = messageTypes,
            Body = serializedBody,
            Host = HostInfo.Current
        };
    }

    /// <summary>
    /// Creates a follow-up envelope that preserves correlation.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="messageTypes">The message type identifiers.</param>
    /// <param name="serializedBody">The serialized message body.</param>
    /// <returns>A new message envelope with preserved correlation.</returns>
    public MessageEnvelope CreateFollowUp<TMessage>(
        IReadOnlyList<string> messageTypes,
        byte[] serializedBody)
        where TMessage : class, IMessage
    {
        return new MessageEnvelope
        {
            MessageId = MessageId.New(),
            CorrelationId = CorrelationId,
            ConversationId = ConversationId,
            InitiatorId = MessageId,
            MessageTypes = messageTypes,
            Body = serializedBody,
            Headers = new MessageHeaders
            {
                TraceId = Headers.TraceId,
                TenantId = Headers.TenantId,
                [MessageHeaders.Keys.InitiatorId] = MessageId.ToString()
            },
            Host = HostInfo.Current
        };
    }
}

/// <summary>
/// Information about the host that sent a message.
/// </summary>
public sealed record HostInfo
{
    /// <summary>Gets the machine name.</summary>
    public required string MachineName { get; init; }

    /// <summary>Gets the process name.</summary>
    public string? ProcessName { get; init; }

    /// <summary>Gets the process ID.</summary>
    public int? ProcessId { get; init; }

    /// <summary>Gets the assembly name.</summary>
    public string? Assembly { get; init; }

    /// <summary>Gets the assembly version.</summary>
    public string? AssemblyVersion { get; init; }

    /// <summary>Gets the framework version.</summary>
    public string? FrameworkVersion { get; init; }

    /// <summary>Gets the operating system description.</summary>
    public string? OperatingSystem { get; init; }

    /// <summary>
    /// Gets the current host information.
    /// </summary>
    public static HostInfo Current { get; } = CreateCurrent();

    private static HostInfo CreateCurrent()
    {
        var assembly = System.Reflection.Assembly.GetEntryAssembly();

        return new HostInfo
        {
            MachineName = Environment.MachineName,
            ProcessName = Environment.ProcessPath is not null
                ? Path.GetFileNameWithoutExtension(Environment.ProcessPath)
                : null,
            ProcessId = Environment.ProcessId,
            Assembly = assembly?.GetName().Name,
            AssemblyVersion = assembly?.GetName().Version?.ToString(),
            FrameworkVersion = Environment.Version.ToString(),
            OperatingSystem = Environment.OSVersion.ToString()
        };
    }
}
