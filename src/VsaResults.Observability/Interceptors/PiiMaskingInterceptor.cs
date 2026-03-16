using VsaResults.Features.WideEvents.Unified;
using VsaResults.Features.WideEvents.Unified.Interceptors;
using VsaResults.Features.WideEvents.Unified.Segments;
using VsaResults.Observability.Masking;

namespace VsaResults.Observability.Interceptors;

/// <summary>
/// Wide event interceptor that applies PII masking to context values before emission.
/// Runs early in the pipeline (after sampling but before context enrichment).
/// </summary>
public sealed class PiiMaskingInterceptor : WideEventInterceptorBase
{
    private readonly IPiiMasker _masker;

    /// <summary>
    /// Initializes a new instance of the <see cref="PiiMaskingInterceptor"/> class.
    /// </summary>
    /// <param name="masker">The PII masker to use.</param>
    public PiiMaskingInterceptor(IPiiMasker masker)
    {
        _masker = masker;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Runs at order -400, after sampling (-500) but before context enrichment.
    /// </remarks>
    public override int Order => -400;

    /// <inheritdoc />
    public override ValueTask<WideEvent?> OnBeforeEmitAsync(WideEvent wideEvent, CancellationToken ct)
    {
        // Mask main context
        MaskContext(wideEvent.Context);

        // Mask child span contexts
        foreach (var childSpan in wideEvent.ChildSpans)
        {
            if (childSpan.Context != null)
            {
                MaskContext(childSpan.Context);
            }
        }

        // Mask error details if present
        if (wideEvent.Error != null)
        {
            MaskErrorSegment(wideEvent.Error);
        }

        // Mask message segment if present
        if (wideEvent.Message != null)
        {
            MaskMessageSegment(wideEvent.Message);
        }

        return ValueTask.FromResult<WideEvent?>(wideEvent);
    }

    private void MaskContext(IDictionary<string, object?> context)
    {
        // Get keys to process (avoid modifying during enumeration)
        var keys = context.Keys.ToList();

        foreach (var key in keys)
        {
            var originalValue = context[key];

            // Skip values already redacted by the RedactionInterceptor
            if (originalValue is string s && s == RedactionInterceptor.RedactedValue)
            {
                continue;
            }

            var maskedValue = _masker.MaskValue(key, originalValue);

            if (!ReferenceEquals(originalValue, maskedValue))
            {
                context[key] = maskedValue;
            }
        }
    }

    private void MaskErrorSegment(WideEventErrorSegment error)
    {
        // Mask error message if it contains PII
        if (!string.IsNullOrEmpty(error.Message))
        {
            error.Message = _masker.MaskString(error.Message);
        }

        // Mask exception message if it contains PII
        if (!string.IsNullOrEmpty(error.ExceptionMessage))
        {
            error.ExceptionMessage = _masker.MaskString(error.ExceptionMessage);
        }

        // Mask all descriptions if present
        if (!string.IsNullOrEmpty(error.AllDescriptions))
        {
            error.AllDescriptions = _masker.MaskString(error.AllDescriptions);
        }

        // Note: Stack traces are not masked as they typically don't contain PII
        // and are valuable for debugging
    }

    private void MaskMessageSegment(WideEventMessageSegment message)
    {
        // Message IDs and types are typically not PII, but addresses might contain user data
        if (!string.IsNullOrEmpty(message.SourceAddress))
        {
            message.SourceAddress = _masker.MaskString(message.SourceAddress, "address");
        }

        if (!string.IsNullOrEmpty(message.DestinationAddress))
        {
            message.DestinationAddress = _masker.MaskString(message.DestinationAddress, "address");
        }

        if (!string.IsNullOrEmpty(message.ResponseAddress))
        {
            message.ResponseAddress = _masker.MaskString(message.ResponseAddress, "address");
        }

        if (!string.IsNullOrEmpty(message.FaultAddress))
        {
            message.FaultAddress = _masker.MaskString(message.FaultAddress, "address");
        }

        if (!string.IsNullOrEmpty(message.InputAddress))
        {
            message.InputAddress = _masker.MaskString(message.InputAddress, "address");
        }
    }
}
