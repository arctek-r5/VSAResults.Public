namespace VsaResults.Messaging.Transports.InMemory;

/// <summary>
/// Configuration options for the in-memory transport.
/// </summary>
public sealed class InMemoryTransportOptions
{
    /// <summary>
    /// Gets or sets the host name used in addresses.
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the default prefetch count for receive endpoints.
    /// </summary>
    public int DefaultPrefetchCount { get; set; } = 16;

    /// <summary>
    /// Gets or sets the default concurrency limit for receive endpoints.
    /// </summary>
    public int DefaultConcurrencyLimit { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Gets or sets whether to auto-start endpoints when created.
    /// </summary>
    public bool AutoStart { get; set; } = false;
}
