using VsaResults.Messaging.ErrorOr;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Transports;

/// <summary>
/// Represents an endpoint address in the messaging system.
/// Addresses use URI format: scheme://host/queue-name
/// </summary>
public sealed record EndpointAddress
{
    /// <summary>Gets the full URI.</summary>
    public required Uri Uri { get; init; }

    /// <summary>Gets the address scheme (e.g., "inmemory", "rabbitmq").</summary>
    public string Scheme => Uri.Scheme;

    /// <summary>Gets the host name.</summary>
    public string Host => Uri.Host;

    /// <summary>Gets the port (if specified).</summary>
    public int? Port => Uri.Port > 0 ? Uri.Port : null;

    /// <summary>Gets the queue/exchange name.</summary>
    public string Name => Uri.AbsolutePath.TrimStart('/');

    /// <summary>Gets the virtual host (for RabbitMQ).</summary>
    public string? VirtualHost => Uri.Segments.Length > 1 ? Uri.Segments[1].TrimEnd('/') : null;

    /// <summary>
    /// Creates an endpoint address from a URI string.
    /// </summary>
    /// <param name="uri">The URI string.</param>
    /// <returns>The endpoint address or an error.</returns>
    public static VsaResult<EndpointAddress> Parse(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
        {
            return MessagingErrors.InvalidEndpointAddress(uri);
        }

        return new EndpointAddress { Uri = parsed };
    }

    /// <summary>
    /// Creates an endpoint address from a URI.
    /// </summary>
    /// <param name="uri">The URI.</param>
    public static EndpointAddress FromUri(Uri uri) => new() { Uri = uri };

    /// <summary>
    /// Creates an in-memory endpoint address.
    /// </summary>
    /// <param name="queueName">The queue name.</param>
    public static EndpointAddress InMemory(string queueName) =>
        new() { Uri = new Uri($"inmemory://localhost/{queueName}") };

    /// <summary>
    /// Creates a RabbitMQ endpoint address.
    /// </summary>
    /// <param name="host">The host name.</param>
    /// <param name="queueName">The queue name.</param>
    /// <param name="virtualHost">Optional virtual host.</param>
    public static EndpointAddress RabbitMq(string host, string queueName, string? virtualHost = null) =>
        virtualHost is not null
            ? new() { Uri = new Uri($"rabbitmq://{host}/{virtualHost}/{queueName}") }
            : new() { Uri = new Uri($"rabbitmq://{host}/{queueName}") };

    /// <summary>
    /// Creates a RabbitMQ endpoint address with port.
    /// </summary>
    /// <param name="host">The host name.</param>
    /// <param name="port">The port number.</param>
    /// <param name="queueName">The queue name.</param>
    /// <param name="virtualHost">Optional virtual host.</param>
    public static EndpointAddress RabbitMq(string host, int port, string queueName, string? virtualHost = null) =>
        virtualHost is not null
            ? new() { Uri = new Uri($"rabbitmq://{host}:{port}/{virtualHost}/{queueName}") }
            : new() { Uri = new Uri($"rabbitmq://{host}:{port}/{queueName}") };

    /// <summary>
    /// Returns the string representation of the address.
    /// </summary>
    public override string ToString() => Uri.ToString();

    /// <summary>
    /// Implicit conversion to URI.
    /// </summary>
    public static implicit operator Uri(EndpointAddress address) => address.Uri;
}
