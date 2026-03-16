namespace VsaResults.VsaResult;

/// <summary>
/// Extension methods for flattening nested ErrorOr values.
/// </summary>
public static partial class VsaResultExtensions
{
    /// <summary>
    /// Flattens a nested <see cref="VsaResult{T}"/> structure.
    /// If the outer ErrorOr is in error state, returns those errors.
    /// If the outer ErrorOr is successful, returns the inner ErrorOr.
    /// </summary>
    /// <typeparam name="TValue">The type of the inner value.</typeparam>
    /// <param name="nested">The nested ErrorOr to flatten.</param>
    /// <returns>A flattened <see cref="VsaResult{TValue}"/>.</returns>
    /// <example>
    /// <code>
    /// ErrorOr&lt;ErrorOr&lt;int&gt;&gt; nested = GetNestedResult();
    /// ErrorOr&lt;int&gt; flattened = nested.Flatten();
    /// </code>
    /// </example>
    public static VsaResult<TValue> Flatten<TValue>(this VsaResult<VsaResult<TValue>> nested) =>
        nested.IsError ? nested._errors : nested.Value;

    /// <summary>
    /// Flattens a Task-wrapped nested <see cref="VsaResult{T}"/> structure.
    /// </summary>
    /// <typeparam name="TValue">The type of the inner value.</typeparam>
    /// <param name="nested">The Task-wrapped nested ErrorOr to flatten.</param>
    /// <returns>A flattened <see cref="VsaResult{TValue}"/>.</returns>
    public static Task<VsaResult<TValue>> Flatten<TValue>(this Task<VsaResult<VsaResult<TValue>>> nested) =>
        nested.ThenSync(result => result.Flatten());

    /// <summary>
    /// Flattens an ErrorOr containing a Task-wrapped ErrorOr.
    /// </summary>
    /// <typeparam name="TValue">The type of the inner value.</typeparam>
    /// <param name="nested">The ErrorOr containing a Task-wrapped ErrorOr.</param>
    /// <returns>A flattened <see cref="VsaResult{TValue}"/>.</returns>
    public static Task<VsaResult<TValue>> FlattenAsync<TValue>(this VsaResult<Task<VsaResult<TValue>>> nested) =>
        nested.IsError
            ? Task.FromResult<VsaResult<TValue>>(nested._errors)
            : nested.Value;
}
