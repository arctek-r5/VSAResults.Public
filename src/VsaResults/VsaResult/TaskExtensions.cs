namespace VsaResults.VsaResult;

/// <summary>
/// Internal helper extensions for Task continuation without async state machine overhead.
/// </summary>
internal static class TaskExtensions
{
    /// <summary>
    /// Continues a task with a synchronous transformation function.
    /// Avoids async state machine overhead when the continuation is synchronous.
    /// </summary>
    internal static Task<TResult> ThenSync<TSource, TResult>(
        this Task<TSource> task,
        Func<TSource, TResult> selector) =>
        task.ContinueWith(
            t => selector(t.GetAwaiter().GetResult()),
            TaskContinuationOptions.ExecuteSynchronously);

    /// <summary>
    /// Continues a task with a void action.
    /// Avoids async state machine overhead when the continuation is synchronous.
    /// </summary>
    internal static Task ThenSync<TSource>(
        this Task<TSource> task,
        Action<TSource> action) =>
        task.ContinueWith(
            t =>
            {
                action(t.GetAwaiter().GetResult());
            },
            TaskContinuationOptions.ExecuteSynchronously);

    /// <summary>
    /// Continues a task with an async transformation function (unwraps the nested Task).
    /// </summary>
    internal static Task<TResult> ThenAsync<TSource, TResult>(
        this Task<TSource> task,
        Func<TSource, Task<TResult>> selector) =>
        task.ContinueWith(
            t => selector(t.GetAwaiter().GetResult()),
            TaskContinuationOptions.ExecuteSynchronously).Unwrap();

    /// <summary>
    /// Continues a task with an async void action (unwraps the nested Task).
    /// </summary>
    internal static Task ThenAsync<TSource>(
        this Task<TSource> task,
        Func<TSource, Task> action) =>
        task.ContinueWith(
            t => action(t.GetAwaiter().GetResult()),
            TaskContinuationOptions.ExecuteSynchronously).Unwrap();
}
