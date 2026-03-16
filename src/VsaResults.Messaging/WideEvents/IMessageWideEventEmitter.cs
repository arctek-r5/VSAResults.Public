namespace VsaResults.Messaging.WideEvents;

/// <summary>
/// Abstraction for emitting message processing wide events.
/// Implement this to integrate with your telemetry system (Serilog, OpenTelemetry, etc.).
/// </summary>
public interface IMessageWideEventEmitter
{
    /// <summary>
    /// Emits a wide event for message processing.
    /// </summary>
    /// <param name="wideEvent">The wide event to emit.</param>
    void Emit(MessageWideEvent wideEvent);
}

/// <summary>
/// A null implementation that discards all events.
/// Used when WideEvents are not configured.
/// </summary>
public sealed class NullMessageWideEventEmitter : IMessageWideEventEmitter
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static readonly NullMessageWideEventEmitter Instance = new();

    private NullMessageWideEventEmitter()
    {
    }

    /// <inheritdoc />
    public void Emit(MessageWideEvent wideEvent)
    {
        // Intentionally empty - discards events
    }
}
