using VsaResults.Messaging.ErrorOr;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Messages;

/// <summary>
/// Strongly-typed conversation identifier for grouping related message exchanges.
/// A conversation groups multiple correlated message exchanges that form a logical unit.
/// </summary>
public readonly record struct ConversationId
{
    private readonly Guid _value;

    private ConversationId(Guid value) => _value = value;

    /// <summary>
    /// Creates a new unique conversation ID.
    /// </summary>
    public static ConversationId New() => new(Guid.NewGuid());

    /// <summary>
    /// Creates a conversation ID from an existing GUID.
    /// </summary>
    /// <param name="value">The GUID value.</param>
    public static ConversationId From(Guid value) => new(value);

    /// <summary>
    /// Parses a conversation ID from its string representation.
    /// </summary>
    /// <param name="value">The string to parse.</param>
    /// <returns>The parsed conversation ID or an error.</returns>
    public static VsaResult<ConversationId> Parse(string value) =>
        Guid.TryParse(value, out var guid)
            ? new ConversationId(guid)
            : MessagingErrors.InvalidConversationId(value);

    /// <summary>
    /// Returns the string representation of the conversation ID.
    /// </summary>
    public override string ToString() => _value.ToString();

    /// <summary>
    /// Implicit conversion to GUID.
    /// </summary>
    public static implicit operator Guid(ConversationId id) => id._value;

    /// <summary>
    /// Gets the empty conversation ID.
    /// </summary>
    public static ConversationId Empty => new(Guid.Empty);
}
