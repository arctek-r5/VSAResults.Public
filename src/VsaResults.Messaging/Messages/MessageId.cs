using VsaResults.Messaging.ErrorOr;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Messages;

/// <summary>
/// Strongly-typed unique identifier for messages.
/// Provides type safety and prevents mixing message IDs with other GUIDs.
/// </summary>
public readonly record struct MessageId
{
    private readonly Guid _value;

    private MessageId(Guid value) => _value = value;

    /// <summary>
    /// Creates a new unique message ID.
    /// </summary>
    public static MessageId New() => new(Guid.NewGuid());

    /// <summary>
    /// Creates a message ID from an existing GUID.
    /// </summary>
    /// <param name="value">The GUID value.</param>
    public static MessageId From(Guid value) => new(value);

    /// <summary>
    /// Parses a message ID from its string representation.
    /// </summary>
    /// <param name="value">The string to parse.</param>
    /// <returns>The parsed message ID or an error.</returns>
    public static VsaResult<MessageId> Parse(string value) =>
        Guid.TryParse(value, out var guid)
            ? new MessageId(guid)
            : MessagingErrors.InvalidMessageId(value);

    /// <summary>
    /// Returns the string representation of the message ID.
    /// </summary>
    public override string ToString() => _value.ToString();

    /// <summary>
    /// Implicit conversion to GUID.
    /// </summary>
    public static implicit operator Guid(MessageId id) => id._value;

    /// <summary>
    /// Gets the empty message ID.
    /// </summary>
    public static MessageId Empty => new(Guid.Empty);
}
