namespace VsaResults.Features.WideEvents.Unified.Interceptors;

/// <summary>
/// Interceptor that enforces context limits (entry count, string length, child spans).
/// </summary>
public sealed class ContextLimitInterceptor : WideEventInterceptorBase
{
    private readonly WideEventOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContextLimitInterceptor"/> class.
    /// </summary>
    /// <param name="options">The wide event options.</param>
    public ContextLimitInterceptor(WideEventOptions options)
    {
        _options = options;
    }

    /// <inheritdoc />
    public override int Order => 500; // Run late after all enrichment

    /// <inheritdoc />
    public override ValueTask<WideEvent?> OnBeforeEmitAsync(WideEvent wideEvent, CancellationToken ct)
    {
        // Limit context entries
        LimitContextEntries(wideEvent.Context);

        // Truncate long string values
        TruncateStringValues(wideEvent.Context);

        // Limit child spans
        if (wideEvent.ChildSpans.Count > _options.MaxChildSpans)
        {
            wideEvent.ChildSpans = [.. wideEvent.ChildSpans.Take(_options.MaxChildSpans)];
        }

        // Process child span context based on options
        foreach (var childSpan in wideEvent.ChildSpans)
        {
            if (!_options.IncludeChildSpanContext)
            {
                childSpan.Context = null;
            }
            else if (childSpan.Context != null)
            {
                LimitContextEntries(childSpan.Context);
                TruncateStringValues(childSpan.Context);
            }
        }

        // Remove stack trace if disabled
        if (!_options.IncludeStackTraces && wideEvent.Error != null)
        {
            wideEvent.Error.ExceptionStackTrace = null;
        }

        return ValueTask.FromResult<WideEvent?>(wideEvent);
    }

    private void LimitContextEntries(IDictionary<string, object?> context)
    {
        if (context.Count <= _options.MaxContextEntries)
        {
            return;
        }

        // Keep the first N entries
        var keysToRemove = context.Keys.Skip(_options.MaxContextEntries).ToList();
        foreach (var key in keysToRemove)
        {
            context.Remove(key);
        }
    }

    private void TruncateStringValues(IDictionary<string, object?> context)
    {
        var keysToUpdate = new List<(string Key, string Value)>();

        foreach (var (key, value) in context)
        {
            if (value is string str && str.Length > _options.MaxStringValueLength)
            {
                keysToUpdate.Add((key, str[..(_options.MaxStringValueLength - 3)] + "..."));
            }
        }

        foreach (var (key, value) in keysToUpdate)
        {
            context[key] = value;
        }
    }
}
