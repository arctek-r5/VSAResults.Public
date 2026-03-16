namespace VsaResults.Messaging.RabbitMq;

/// <summary>
/// Configuration options for RabbitMQ transport.
/// </summary>
public sealed class RabbitMqTransportOptions
{
    /// <summary>
    /// Gets or sets the RabbitMQ host name.
    /// Default: localhost
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the RabbitMQ port.
    /// Default: 5672
    /// </summary>
    public int Port { get; set; } = 5672;

    /// <summary>
    /// Gets or sets the virtual host.
    /// Default: /
    /// </summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Gets or sets the username for authentication.
    /// Default: guest
    /// </summary>
    public string Username { get; set; } = "guest";

    /// <summary>
    /// Gets or sets the password for authentication.
    /// Default: guest
    /// </summary>
    public string Password { get; set; } = "guest";

    /// <summary>
    /// Gets or sets a value indicating whether to use SSL/TLS.
    /// Default: false
    /// </summary>
    public bool UseSsl { get; set; }

    /// <summary>
    /// Gets or sets the connection timeout.
    /// Default: 30 seconds
    /// </summary>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the heartbeat interval.
    /// Default: 60 seconds
    /// </summary>
    public TimeSpan Heartbeat { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets or sets the prefetch count for consumers.
    /// Default: 16
    /// </summary>
    public ushort PrefetchCount { get; set; } = 16;

    /// <summary>
    /// Gets or sets the number of concurrent consumers per endpoint.
    /// Default: 1
    /// </summary>
    public int ConcurrentConsumers { get; set; } = 1;

    /// <summary>
    /// Gets or sets a value indicating whether messages should be persistent.
    /// Default: true
    /// </summary>
    public bool PersistentMessages { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to auto-delete queues when not in use.
    /// Default: false
    /// </summary>
    public bool AutoDelete { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether queues should be durable.
    /// Default: true
    /// </summary>
    public bool Durable { get; set; } = true;

    /// <summary>
    /// Gets or sets the exchange type for publishing.
    /// Default: fanout
    /// </summary>
    public string ExchangeType { get; set; } = "fanout";

    /// <summary>
    /// Gets or sets the number of connection retry attempts.
    /// Default: 5
    /// </summary>
    public int RetryCount { get; set; } = 5;

    /// <summary>
    /// Gets or sets the delay between retry attempts.
    /// Default: 5 seconds
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets a value indicating whether failed messages should be
    /// dead-lettered to an error queue instead of requeued indefinitely.
    /// When enabled, each queue gets a companion {queue}_error queue via a
    /// dead letter exchange. Failed messages go there immediately on first
    /// failure rather than requeuing forever.
    /// Default: true
    /// </summary>
    public bool DeadLetterOnFailure { get; set; } = true;

    /// <summary>
    /// Gets or sets the publisher confirmation timeout.
    /// Default: 30 seconds
    /// </summary>
    public TimeSpan PublisherConfirmationTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets a value indicating whether to use publisher confirms.
    /// Default: true
    /// </summary>
    public bool UsePublisherConfirms { get; set; } = true;

    /// <summary>
    /// Gets the AMQP connection URI.
    /// </summary>
    public Uri ConnectionUri
    {
        get
        {
            var scheme = UseSsl ? "amqps" : "amqp";
            return new Uri($"{scheme}://{Username}:{Password}@{Host}:{Port}{VirtualHost}");
        }
    }

    /// <summary>
    /// Gets the safe connection URI (without password) for logging.
    /// </summary>
    public string SafeConnectionUri
    {
        get
        {
            var scheme = UseSsl ? "amqps" : "amqp";
            return $"{scheme}://{Username}:***@{Host}:{Port}{VirtualHost}";
        }
    }
}
