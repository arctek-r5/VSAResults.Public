using VsaResults.Errors;

namespace VsaResults.VsaResult;

public static partial class VsaResultExtensions
{
    /// <summary>
    /// Projects the value using a selector function. This is an alias for ThenAsync.
    /// </summary>
    public static Task<VsaResult<TNextValue>> Select<TValue, TNextValue>(
        this Task<VsaResult<TValue>> errorOrTask,
        Func<TValue, TNextValue> selector) =>
        errorOrTask.ThenSync(errorOr => errorOr.Select(selector));

    /// <summary>
    /// Projects the value using an async selector function.
    /// </summary>
    public static Task<VsaResult<TNextValue>> SelectAsync<TValue, TNextValue>(
        this VsaResult<TValue> errorOr,
        Func<TValue, Task<TNextValue>> selector) =>
        errorOr.IsError
            ? Task.FromResult<VsaResult<TNextValue>>(errorOr._errors)
            : selector(errorOr.Value).ContinueWith(
                t => (VsaResult<TNextValue>)t.GetAwaiter().GetResult(),
                TaskContinuationOptions.ExecuteSynchronously);

    /// <summary>
    /// Projects the value using an async selector function on a Task-wrapped ErrorOr.
    /// </summary>
    public static Task<VsaResult<TNextValue>> SelectAsync<TValue, TNextValue>(
        this Task<VsaResult<TValue>> errorOrTask,
        Func<TValue, Task<TNextValue>> selector) =>
        errorOrTask.ThenAsync(errorOr => errorOr.SelectAsync(selector));

    /// <summary>
    /// Projects the value using a selector function that returns an ErrorOr. This is an alias for ThenAsync.
    /// </summary>
    public static Task<VsaResult<TNextValue>> SelectMany<TValue, TNextValue>(
        this Task<VsaResult<TValue>> errorOrTask,
        Func<TValue, VsaResult<TNextValue>> selector) =>
        errorOrTask.ThenSync(errorOr => errorOr.SelectMany(selector));

    /// <summary>
    /// Projects the value using an async selector function that returns an ErrorOr.
    /// </summary>
    public static Task<VsaResult<TNextValue>> SelectManyAsync<TValue, TNextValue>(
        this VsaResult<TValue> errorOr,
        Func<TValue, Task<VsaResult<TNextValue>>> selector) =>
        errorOr.IsError
            ? Task.FromResult<VsaResult<TNextValue>>(errorOr._errors)
            : selector(errorOr.Value);

    /// <summary>
    /// Projects the value using an async selector function that returns an ErrorOr on a Task-wrapped ErrorOr.
    /// </summary>
    public static Task<VsaResult<TNextValue>> SelectManyAsync<TValue, TNextValue>(
        this Task<VsaResult<TValue>> errorOrTask,
        Func<TValue, Task<VsaResult<TNextValue>>> selector) =>
        errorOrTask.ThenAsync(errorOr => errorOr.SelectManyAsync(selector));

    /// <summary>
    /// Filters the value based on a predicate on a Task-wrapped ErrorOr.
    /// </summary>
    public static Task<VsaResult<TValue>> Where<TValue>(
        this Task<VsaResult<TValue>> errorOrTask,
        Func<TValue, bool> predicate,
        Error error) =>
        errorOrTask.ThenSync(errorOr => errorOr.Where(predicate, error));

    /// <summary>
    /// Filters the value based on a predicate on a Task-wrapped ErrorOr.
    /// </summary>
    public static Task<VsaResult<TValue>> Where<TValue>(
        this Task<VsaResult<TValue>> errorOrTask,
        Func<TValue, bool> predicate,
        Func<TValue, Error> errorFactory) =>
        errorOrTask.ThenSync(errorOr => errorOr.Where(predicate, errorFactory));

    /// <summary>
    /// Filters the value based on an async predicate.
    /// </summary>
    public static Task<VsaResult<TValue>> WhereAsync<TValue>(
        this VsaResult<TValue> errorOr,
        Func<TValue, Task<bool>> predicate,
        Error error) =>
        errorOr.IsError
            ? Task.FromResult<VsaResult<TValue>>(errorOr._errors)
            : predicate(errorOr.Value).ContinueWith(
                t => t.GetAwaiter().GetResult() ? errorOr : error,
                TaskContinuationOptions.ExecuteSynchronously);

    /// <summary>
    /// Filters the value based on an async predicate on a Task-wrapped ErrorOr.
    /// </summary>
    public static Task<VsaResult<TValue>> WhereAsync<TValue>(
        this Task<VsaResult<TValue>> errorOrTask,
        Func<TValue, Task<bool>> predicate,
        Error error) =>
        errorOrTask.ThenAsync(errorOr => errorOr.WhereAsync(predicate, error));
}
