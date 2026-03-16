using Microsoft.Extensions.Logging;

namespace VsaResults.Features.WideEvents.Unified.Sinks;

/// <summary>
/// Sink that writes wide events using ILogger (compatible with Serilog).
/// </summary>
public sealed class SerilogWideEventSink : IWideEventSink
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SerilogWideEventSink"/> class.
    /// </summary>
    /// <param name="logger">The logger to write to.</param>
    public SerilogWideEventSink(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SerilogWideEventSink"/> class.
    /// </summary>
    /// <param name="loggerFactory">The logger factory to create a logger from.</param>
    public SerilogWideEventSink(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger("WideEvent");
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(WideEvent wideEvent, CancellationToken ct = default)
    {
        var logLevel = wideEvent.IsSuccess ? LogLevel.Information : LogLevel.Warning;

        var featureName = wideEvent.Feature?.FeatureName ?? "-";
        var messageTypeSuffix = wideEvent.Message?.MessageType != null
            ? $"/{wideEvent.Message.MessageType}"
            : string.Empty;

        _logger.Log(
            logLevel,
            "WideEvent {EventType} {Outcome} {FeatureName}{MessageTypeSuffix} in {TotalMs:F2}ms [TraceId={TraceId}, CorrelationId={CorrelationId}] {ErrorCode} {ErrorMessage}",
            wideEvent.EventType,
            wideEvent.Outcome,
            featureName,
            messageTypeSuffix,
            wideEvent.TotalMs,
            wideEvent.TraceId ?? "-",
            wideEvent.CorrelationId ?? "-",
            wideEvent.Error?.Code ?? "-",
            wideEvent.Error?.Message ?? "-");

        return ValueTask.CompletedTask;
    }
}
