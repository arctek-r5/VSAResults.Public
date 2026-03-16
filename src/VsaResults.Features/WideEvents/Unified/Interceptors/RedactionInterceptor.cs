namespace VsaResults.Features.WideEvents.Unified.Interceptors;

/// <summary>
/// Interceptor that redacts sensitive context keys from wide events.
/// </summary>
public sealed class RedactionInterceptor : WideEventInterceptorBase
{
    /// <summary>
    /// The sentinel value used to replace redacted context entries.
    /// </summary>
    public const string RedactedValue = "[REDACTED]";
    private readonly WideEventOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedactionInterceptor"/> class.
    /// </summary>
    /// <param name="options">The wide event options.</param>
    public RedactionInterceptor(WideEventOptions options)
    {
        _options = options;
    }

    /// <inheritdoc />
    public override int Order => -500; // Run before context enrichment but after sampling

    /// <inheritdoc />
    public override ValueTask<WideEvent?> OnBeforeEmitAsync(WideEvent wideEvent, CancellationToken ct)
    {
        // Redact sensitive keys from main context
        RedactContext(wideEvent.Context);

        // Redact from child spans if present
        foreach (var childSpan in wideEvent.ChildSpans)
        {
            if (childSpan.Context != null)
            {
                RedactContext(childSpan.Context);
            }
        }

        return ValueTask.FromResult<WideEvent?>(wideEvent);
    }

    private void RedactContext(IDictionary<string, object?> context)
    {
        var keysToRedact = new List<string>();

        foreach (var key in context.Keys)
        {
            if (IsSensitiveKey(key))
            {
                keysToRedact.Add(key);
            }
        }

        foreach (var key in keysToRedact)
        {
            context[key] = RedactedValue;
        }
    }

    private bool IsSensitiveKey(string key)
    {
        foreach (var excluded in _options.ExcludedContextKeys)
        {
            if (key.Contains(excluded, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
