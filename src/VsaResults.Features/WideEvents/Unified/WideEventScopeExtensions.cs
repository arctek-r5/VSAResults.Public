namespace VsaResults.Features.WideEvents.Unified;

/// <summary>
/// Static helpers for adding context to the ambient <see cref="WideEventScope"/>.
/// Services call these instead of logging directly — context accumulates on the
/// caller's wide event and is emitted once when the scope completes.
/// </summary>
public static class WideEventScopeExtensions
{
    /// <summary>
    /// Adds a single context entry to the current ambient scope (no-op if no scope is active).
    /// </summary>
    public static void AddContext(string key, object? value)
    {
        WideEventScope.Current?.Builder.WithContext(key, value);
    }

    /// <summary>
    /// Adds context entries extracted from a strongly typed context object
    /// to the current ambient scope (no-op if no scope is active).
    /// </summary>
    public static void AddContextObject(object? context)
    {
        WideEventScope.Current?.Builder.WithContextObject(context);
    }

    /// <summary>
    /// Adds multiple context entries to the current ambient scope (no-op if no scope is active).
    /// </summary>
    public static void AddContext(params (string Key, object? Value)[] entries)
    {
        var scope = WideEventScope.Current;
        if (scope is null) return;

        foreach (var (key, value) in entries)
        {
            scope.Builder.WithContext(key, value);
        }
    }
}
