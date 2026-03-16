using VsaResults.Messaging.Consumers;
using VsaResults.Messaging.Messages;

namespace VsaResults.Messaging.Pipeline;

/// <summary>
/// Base context for pipeline processing.
/// Provides a payload system for passing data between filters.
/// </summary>
public abstract class PipeContext
{
    /// <summary>Gets the cancellation token for this context.</summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// Gets the payload dictionary for storing arbitrary data during processing.
    /// </summary>
    public Dictionary<string, object> Payload { get; } = [];

    /// <summary>
    /// Gets or sets whether the pipeline should be considered complete.
    /// When true, subsequent filters may skip processing.
    /// </summary>
    public bool IsCompleted { get; set; }

    /// <summary>
    /// Gets a payload value by key.
    /// </summary>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <param name="key">The payload key.</param>
    /// <returns>The value cast to the specified type.</returns>
    /// <exception cref="KeyNotFoundException">The key was not found.</exception>
    /// <exception cref="InvalidCastException">The value is not of the expected type.</exception>
    public T GetPayload<T>(string key) => (T)Payload[key];

    /// <summary>
    /// Tries to get a payload value by key.
    /// </summary>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <param name="key">The payload key.</param>
    /// <param name="value">The value if found.</param>
    /// <returns>True if the value was found and is of the correct type.</returns>
    public bool TryGetPayload<T>(string key, out T? value)
    {
        if (Payload.TryGetValue(key, out var obj) && obj is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Gets a payload value or a default value.
    /// </summary>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <param name="key">The payload key.</param>
    /// <param name="defaultValue">The default value if not found.</param>
    /// <returns>The value or default.</returns>
    public T GetPayloadOrDefault<T>(string key, T defaultValue = default!)
    {
        if (Payload.TryGetValue(key, out var obj) && obj is T typed)
        {
            return typed;
        }

        return defaultValue;
    }

    /// <summary>
    /// Sets a payload value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="key">The payload key.</param>
    /// <param name="value">The value to store.</param>
    public void SetPayload<T>(string key, T value)
        where T : notnull
        => Payload[key] = value;

    /// <summary>
    /// Gets or adds a payload value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="key">The payload key.</param>
    /// <param name="factory">Factory to create the value if not found.</param>
    /// <returns>The existing or newly created value.</returns>
    public T GetOrAddPayload<T>(string key, Func<T> factory)
        where T : notnull
    {
        if (Payload.TryGetValue(key, out var obj) && obj is T typed)
        {
            return typed;
        }

        var value = factory();
        Payload[key] = value;
        return value;
    }

    /// <summary>
    /// Removes a payload value.
    /// </summary>
    /// <param name="key">The payload key.</param>
    /// <returns>True if the value was removed.</returns>
    public bool RemovePayload(string key) => Payload.Remove(key);
}

/// <summary>
/// Context for message consumption in the pipeline.
/// </summary>
/// <typeparam name="TMessage">The message type.</typeparam>
public sealed class MessagePipeContext<TMessage> : PipeContext
    where TMessage : class, IMessage
{
    /// <summary>Gets the consume context.</summary>
    public required ConsumeContext<TMessage> ConsumeContext { get; init; }

    /// <summary>Gets the message being processed.</summary>
    public TMessage Message => ConsumeContext.Message;

    /// <summary>Gets the message envelope.</summary>
    public MessageEnvelope Envelope => ConsumeContext.Envelope;

    /// <summary>Gets the message ID.</summary>
    public MessageId MessageId => ConsumeContext.MessageId;

    /// <summary>Gets the correlation ID.</summary>
    public CorrelationId CorrelationId => ConsumeContext.CorrelationId;
}
