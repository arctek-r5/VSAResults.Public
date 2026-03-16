using Confluent.Kafka;

namespace VsaResults.Messaging.Kafka;

/// <summary>
/// Configuration options for Apache Kafka transport.
/// </summary>
public sealed class KafkaTransportOptions
{
    /// <summary>
    /// Gets or sets the Kafka bootstrap servers.
    /// Default: localhost:9092
    /// </summary>
    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>
    /// Gets or sets the consumer group ID.
    /// Default: vsa-consumer-group
    /// </summary>
    public string GroupId { get; set; } = "vsa-consumer-group";

    /// <summary>
    /// Gets or sets the auto offset reset behavior for new consumer groups.
    /// Default: Earliest
    /// </summary>
    public AutoOffsetReset AutoOffsetReset { get; set; } = AutoOffsetReset.Earliest;

    /// <summary>
    /// Gets or sets whether to enable auto-commit of offsets.
    /// Default: false (manual commit for at-least-once delivery)
    /// </summary>
    public bool EnableAutoCommit { get; set; } = false;

    /// <summary>
    /// Gets or sets the session timeout in milliseconds.
    /// Default: 45000 (45 seconds)
    /// </summary>
    public int SessionTimeoutMs { get; set; } = 45000;

    /// <summary>
    /// Gets or sets the maximum poll interval in milliseconds.
    /// Default: 300000 (5 minutes)
    /// </summary>
    public int MaxPollIntervalMs { get; set; } = 300000;

    /// <summary>
    /// Gets or sets the producer acknowledgment mode.
    /// Default: All (wait for all replicas)
    /// </summary>
    public Acks Acks { get; set; } = Acks.All;

    /// <summary>
    /// Gets or sets the producer linger time in milliseconds.
    /// Batches messages for the specified time to improve throughput.
    /// Default: 5ms
    /// </summary>
    public int LingerMs { get; set; } = 5;

    /// <summary>
    /// Gets or sets the producer batch size in bytes.
    /// Default: 16384 (16KB)
    /// </summary>
    public int BatchSize { get; set; } = 16384;

    /// <summary>
    /// Gets or sets the compression type for produced messages.
    /// Default: None
    /// </summary>
    public CompressionType CompressionType { get; set; } = CompressionType.None;

    /// <summary>
    /// Gets or sets the security protocol.
    /// Default: null (Plaintext)
    /// </summary>
    public SecurityProtocol? SecurityProtocol { get; set; }

    /// <summary>
    /// Gets or sets the SASL mechanism for authentication.
    /// Default: null
    /// </summary>
    public SaslMechanism? SaslMechanism { get; set; }

    /// <summary>
    /// Gets or sets the SASL username.
    /// </summary>
    public string? SaslUsername { get; set; }

    /// <summary>
    /// Gets or sets the SASL password.
    /// </summary>
    public string? SaslPassword { get; set; }

    /// <summary>
    /// Gets or sets the path to the SSL CA certificate file.
    /// </summary>
    public string? SslCaLocation { get; set; }

    /// <summary>
    /// Gets or sets the path to the SSL client certificate file.
    /// </summary>
    public string? SslCertificateLocation { get; set; }

    /// <summary>
    /// Gets or sets the path to the SSL client key file.
    /// </summary>
    public string? SslKeyLocation { get; set; }

    /// <summary>
    /// Gets or sets the number of concurrent consumers per endpoint.
    /// Default: 1
    /// </summary>
    public int ConcurrentConsumers { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of retry attempts for failed operations.
    /// Default: 5
    /// </summary>
    public int RetryCount { get; set; } = 5;

    /// <summary>
    /// Gets or sets the delay between retry attempts.
    /// Default: 5 seconds
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the default message timeout for producer operations.
    /// Default: 30 seconds
    /// </summary>
    public TimeSpan MessageTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets whether to enable idempotent producer.
    /// Default: true (prevents duplicate messages on retries)
    /// </summary>
    public bool EnableIdempotence { get; set; } = true;

    /// <summary>
    /// Builds a ProducerConfig from these options.
    /// </summary>
    internal ProducerConfig BuildProducerConfig()
    {
        var config = new ProducerConfig
        {
            BootstrapServers = BootstrapServers,
            Acks = Acks,
            LingerMs = LingerMs,
            BatchSize = BatchSize,
            CompressionType = CompressionType,
            MessageTimeoutMs = (int)MessageTimeout.TotalMilliseconds,
            EnableIdempotence = EnableIdempotence
        };

        ApplySecuritySettings(config);
        return config;
    }

    /// <summary>
    /// Builds a ConsumerConfig from these options.
    /// </summary>
    internal ConsumerConfig BuildConsumerConfig()
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = BootstrapServers,
            GroupId = GroupId,
            AutoOffsetReset = AutoOffsetReset,
            EnableAutoCommit = EnableAutoCommit,
            SessionTimeoutMs = SessionTimeoutMs,
            MaxPollIntervalMs = MaxPollIntervalMs
        };

        ApplySecuritySettings(config);
        return config;
    }

    private void ApplySecuritySettings(ClientConfig config)
    {
        if (SecurityProtocol.HasValue)
        {
            config.SecurityProtocol = SecurityProtocol.Value;
        }

        if (SaslMechanism.HasValue)
        {
            config.SaslMechanism = SaslMechanism.Value;
            config.SaslUsername = SaslUsername;
            config.SaslPassword = SaslPassword;
        }

        if (!string.IsNullOrEmpty(SslCaLocation))
        {
            config.SslCaLocation = SslCaLocation;
        }

        if (!string.IsNullOrEmpty(SslCertificateLocation))
        {
            config.SslCertificateLocation = SslCertificateLocation;
        }

        if (!string.IsNullOrEmpty(SslKeyLocation))
        {
            config.SslKeyLocation = SslKeyLocation;
        }
    }

    /// <summary>
    /// Gets a safe representation of the configuration for logging (without credentials).
    /// </summary>
    public string SafeConnectionString => SecurityProtocol.HasValue
        ? $"{BootstrapServers} (Protocol: {SecurityProtocol.Value}, SASL: {SaslMechanism?.ToString() ?? "None"})"
        : BootstrapServers;
}
