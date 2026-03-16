using System.Collections.Immutable;
using VsaResults.Errors;

namespace VsaResults.VsaResult;

public readonly partial record struct VsaResult<TValue>
{
    /// <summary>
    /// Adds a key-value pair to the context that flows through the chain.
    /// Use this to accumulate context for wide events instead of scattered log statements.
    /// </summary>
    /// <param name="key">The context key (e.g., "user.id", "order.count").</param>
    /// <param name="value">The context value.</param>
    /// <returns>A new ErrorOr with the updated context.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Context System Overview:</strong> VsaResults has two context mechanisms:
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <term><c>ErrorOr&lt;T&gt;.WithContext()</c></term>
    ///     <description>
    ///       Immutable context that flows through fluent chains (<c>Then</c>, <c>Else</c>, <c>Match</c>, etc.).
    ///       Use for ad-hoc operations or when not using the Feature Pipeline.
    ///       Context is preserved across transformations and merged automatically.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>FeatureContext&lt;TRequest&gt;.AddContext()</c></term>
    ///     <description>
    ///       Mutable context scoped to a Feature Pipeline execution.
    ///       Use within <c>IFeatureValidator</c>, <c>IFeatureRequirements</c>, <c>IFeatureMutator</c>, etc.
    ///       Automatically merged into the wide event at the end of execution.
    ///     </description>
    ///   </item>
    /// </list>
    /// <para>
    /// <strong>Which to use?</strong>
    /// </para>
    /// <list type="bullet">
    ///   <item>Inside Feature Pipeline stages → use <c>FeatureContext.AddContext()</c></item>
    ///   <item>Outside Feature Pipeline (standalone ErrorOr chains) → use <c>ErrorOr.WithContext()</c></item>
    ///   <item>Both contexts are merged into the final wide event automatically</item>
    /// </list>
    /// </remarks>
    public VsaResult<TValue> WithContext(string key, object value)
    {
        var newContext = (_context ?? [])
            .SetItem(key, value);

        return IsError
            ? new VsaResult<TValue>(_errors, newContext)
            : new VsaResult<TValue>(_value!, newContext);
    }

    /// <summary>
    /// Adds multiple key-value pairs to the context.
    /// </summary>
    /// <param name="pairs">The key-value pairs to add.</param>
    /// <returns>A new ErrorOr with the updated context.</returns>
    public VsaResult<TValue> WithContext(params (string Key, object Value)[] pairs)
    {
        var builder = (_context ?? []).ToBuilder();

        foreach (var (key, value) in pairs)
        {
            builder[key] = value;
        }

        var newContext = builder.ToImmutable();

        return IsError
            ? new VsaResult<TValue>(_errors, newContext)
            : new VsaResult<TValue>(_value!, newContext);
    }

    /// <summary>
    /// Adds context derived from the success value. Only executes if not in error state.
    /// </summary>
    /// <param name="selector">A function that extracts a key-value pair from the value.</param>
    /// <returns>A new ErrorOr with the updated context, or the same ErrorOr if in error state.</returns>
    public VsaResult<TValue> WithContext(Func<TValue, (string Key, object Value)> selector)
    {
        if (IsError)
        {
            return this;
        }

        var (key, value) = selector(_value);
        return WithContext(key, value);
    }

    /// <summary>
    /// Adds multiple context entries derived from the success value. Only executes if not in error state.
    /// </summary>
    /// <param name="selector">A function that extracts key-value pairs from the value.</param>
    /// <returns>A new ErrorOr with the updated context, or the same ErrorOr if in error state.</returns>
    public VsaResult<TValue> WithContext(Func<TValue, IEnumerable<(string Key, object Value)>> selector)
    {
        if (IsError)
        {
            return this;
        }

        var pairs = selector(_value).ToArray();
        return WithContext(pairs);
    }

    /// <summary>
    /// Adds context derived from errors. Only executes if in error state.
    /// </summary>
    /// <param name="selector">A function that extracts a key-value pair from the errors.</param>
    /// <returns>A new ErrorOr with the updated context, or the same ErrorOr if not in error state.</returns>
    public VsaResult<TValue> WithErrorContext(Func<List<Error>, (string Key, object Value)> selector)
    {
        if (!IsError)
        {
            return this;
        }

        var (key, value) = selector(_errors);
        return WithContext(key, value);
    }

    /// <summary>
    /// Adds context derived from the first error. Only executes if in error state.
    /// </summary>
    /// <param name="selector">A function that extracts a key-value pair from the first error.</param>
    /// <returns>A new ErrorOr with the updated context, or the same ErrorOr if not in error state.</returns>
    public VsaResult<TValue> WithFirstErrorContext(Func<Error, (string Key, object Value)> selector)
    {
        if (!IsError)
        {
            return this;
        }

        var (key, value) = selector(_errors[0]);
        return WithContext(key, value);
    }

    /// <summary>
    /// Merges the provided context with this ErrorOr's context.
    /// The existing context in this ErrorOr takes precedence (newer values win).
    /// </summary>
    internal VsaResult<TValue> MergeContext(ImmutableDictionary<string, object>? otherContext)
    {
        if (otherContext is null || otherContext.Count == 0)
        {
            return this;
        }

        if (_context is null || _context.Count == 0)
        {
            return IsError
                ? new VsaResult<TValue>(_errors!, otherContext)
                : new VsaResult<TValue>(_value!, otherContext);
        }

        // Merge: other context first, then our context (our context wins on conflicts)
        var merged = otherContext.SetItems(_context);

        return IsError
            ? new VsaResult<TValue>(_errors!, merged)
            : new VsaResult<TValue>(_value!, merged);
    }
}
