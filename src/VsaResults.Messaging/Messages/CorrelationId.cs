using VsaResults.Messaging.ErrorOr;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Messages;

/// <summary>
/// Strongly-typed correlation identifier for message tracing.
/// Correlation IDs link related messages across a distributed conversation.
/// </summary>
public readonly record struct CorrelationId
{
    private readonly Guid _value;

    private CorrelationId(Guid value) => _value = value;

    /// <summary>
    /// Creates a new unique correlation ID.
    /// </summary>
    public static CorrelationId New() => new(Guid.NewGuid());

    /// <summary>
    /// Creates a correlation ID from an existing GUID.
    /// </summary>
    /// <param name="value">The GUID value.</param>
    public static CorrelationId From(Guid value) => new(value);

    /// <summary>
    /// Parses a correlation ID from its string representation.
    /// </summary>
    /// <param name="value">The string to parse.</param>
    /// <returns>The parsed correlation ID or an error.</returns>
    public static VsaResult<CorrelationId> Parse(string value) =>
        Guid.TryParse(value, out var guid)
            ? new CorrelationId(guid)
            : MessagingErrors.InvalidCorrelationId(value);

    /// <summary>
    /// Returns the string representation of the correlation ID.
    /// </summary>
    public override string ToString() => _value.ToString();

    /// <summary>
    /// Implicit conversion to GUID.
    /// </summary>
    public static implicit operator Guid(CorrelationId id) => id._value;

    /// <summary>
    /// Gets the empty correlation ID.
    /// </summary>
    public static CorrelationId Empty => new(Guid.Empty);
}
