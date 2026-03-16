namespace VsaResults.Messaging.Messages;

/// <summary>
/// Collection of message headers for extensible metadata.
/// Headers are used to pass contextual information through the messaging pipeline.
/// </summary>
public sealed class MessageHeaders : Dictionary<string, object?>
{
    /// <summary>
    /// Standard header key names.
    /// </summary>
    public static new class Keys
    {
        /// <summary>Distributed tracing trace ID.</summary>
        public const string TraceId = "x-trace-id";

        /// <summary>Distributed tracing span ID.</summary>
        public const string SpanId = "x-span-id";

        /// <summary>Distributed tracing parent span ID.</summary>
        public const string ParentSpanId = "x-parent-span-id";

        /// <summary>Multi-tenant tenant identifier.</summary>
        public const string TenantId = "x-tenant-id";

        /// <summary>Message ID that initiated this message.</summary>
        public const string InitiatorId = "x-initiator-id";

        /// <summary>Message priority (higher = more urgent).</summary>
        public const string Priority = "x-priority";

        /// <summary>Time-to-live in seconds.</summary>
        public const string TimeToLive = "x-ttl";

        /// <summary>Scheduled delivery time.</summary>
        public const string ScheduledTime = "x-scheduled-time";

        /// <summary>Retry attempt number.</summary>
        public const string RetryAttempt = "x-retry-attempt";

        /// <summary>Maximum retry attempts.</summary>
        public const string MaxRetries = "x-max-retries";

        /// <summary>Original exception type on fault.</summary>
        public const string FaultExceptionType = "x-fault-exception-type";

        /// <summary>Fault reason.</summary>
        public const string FaultReason = "x-fault-reason";
    }

    /// <summary>
    /// Creates a new empty headers collection.
    /// </summary>
    public MessageHeaders()
    {
    }

    /// <summary>
    /// Creates headers from an existing dictionary.
    /// </summary>
    /// <param name="headers">The headers to copy.</param>
    public MessageHeaders(IDictionary<string, object?> headers) : base(headers)
    {
    }

    /// <summary>Gets or sets the trace ID for distributed tracing.</summary>
    public string? TraceId
    {
        get => GetString(Keys.TraceId);
        set => this[Keys.TraceId] = value;
    }

    /// <summary>Gets or sets the span ID.</summary>
    public string? SpanId
    {
        get => GetString(Keys.SpanId);
        set => this[Keys.SpanId] = value;
    }

    /// <summary>Gets or sets the parent span ID.</summary>
    public string? ParentSpanId
    {
        get => GetString(Keys.ParentSpanId);
        set => this[Keys.ParentSpanId] = value;
    }

    /// <summary>Gets or sets the tenant ID for multi-tenancy.</summary>
    public string? TenantId
    {
        get => GetString(Keys.TenantId);
        set => this[Keys.TenantId] = value;
    }

    /// <summary>Gets or sets the initiator message ID.</summary>
    public string? InitiatorId
    {
        get => GetString(Keys.InitiatorId);
        set => this[Keys.InitiatorId] = value;
    }

    /// <summary>Gets or sets the message priority.</summary>
    public int? Priority
    {
        get => GetInt(Keys.Priority);
        set => this[Keys.Priority] = value;
    }

    /// <summary>Gets or sets the retry attempt number.</summary>
    public int? RetryAttempt
    {
        get => GetInt(Keys.RetryAttempt);
        set => this[Keys.RetryAttempt] = value;
    }

    /// <summary>Gets or sets the maximum retries.</summary>
    public int? MaxRetries
    {
        get => GetInt(Keys.MaxRetries);
        set => this[Keys.MaxRetries] = value;
    }

    /// <summary>Gets or sets the scheduled delivery time.</summary>
    public DateTimeOffset? ScheduledTime
    {
        get => GetDateTimeOffset(Keys.ScheduledTime);
        set => this[Keys.ScheduledTime] = value?.ToString("O");
    }

    /// <summary>
    /// Creates a copy of the headers.
    /// </summary>
    public MessageHeaders Clone() => new(this);

    private string? GetString(string key) =>
        TryGetValue(key, out var value) ? value?.ToString() : null;

    private int? GetInt(string key)
    {
        if (!TryGetValue(key, out var value))
        {
            return null;
        }

        return value switch
        {
            int i => i,
            long l => (int)l,
            string s when int.TryParse(s, out var parsed) => parsed,
            _ => null
        };
    }

    private DateTimeOffset? GetDateTimeOffset(string key)
    {
        if (!TryGetValue(key, out var value))
        {
            return null;
        }

        return value switch
        {
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(dt),
            string s when DateTimeOffset.TryParse(s, out var parsed) => parsed,
            _ => null
        };
    }
}
