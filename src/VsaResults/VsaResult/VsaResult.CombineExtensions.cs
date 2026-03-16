namespace VsaResults.VsaResult;

public static partial class VsaResultExtensions
{
    /// <summary>
    /// Combines two async ErrorOr results. Returns a tuple of values if both are successful,
    /// or all accumulated errors if any failed.
    /// </summary>
    public static Task<VsaResult<(T1 First, T2 Second)>> Combine<T1, T2>(
        Task<VsaResult<T1>> firstTask,
        Task<VsaResult<T2>> secondTask) =>
        Task.WhenAll(firstTask, secondTask)
            .ContinueWith(
                _ => VsaResultCombine.Combine(firstTask.Result, secondTask.Result),
                TaskContinuationOptions.ExecuteSynchronously);

    /// <summary>
    /// Combines three async ErrorOr results. Returns a tuple of values if all are successful,
    /// or all accumulated errors if any failed.
    /// </summary>
    public static Task<VsaResult<(T1 First, T2 Second, T3 Third)>> Combine<T1, T2, T3>(
        Task<VsaResult<T1>> firstTask,
        Task<VsaResult<T2>> secondTask,
        Task<VsaResult<T3>> thirdTask) =>
        Task.WhenAll(firstTask, secondTask, thirdTask)
            .ContinueWith(
                _ => VsaResultCombine.Combine(firstTask.Result, secondTask.Result, thirdTask.Result),
                TaskContinuationOptions.ExecuteSynchronously);

    /// <summary>
    /// Combines four async ErrorOr results. Returns a tuple of values if all are successful,
    /// or all accumulated errors if any failed.
    /// </summary>
    public static Task<VsaResult<(T1 First, T2 Second, T3 Third, T4 Fourth)>> Combine<T1, T2, T3, T4>(
        Task<VsaResult<T1>> firstTask,
        Task<VsaResult<T2>> secondTask,
        Task<VsaResult<T3>> thirdTask,
        Task<VsaResult<T4>> fourthTask) =>
        Task.WhenAll(firstTask, secondTask, thirdTask, fourthTask)
            .ContinueWith(
                _ => VsaResultCombine.Combine(firstTask.Result, secondTask.Result, thirdTask.Result, fourthTask.Result),
                TaskContinuationOptions.ExecuteSynchronously);

    /// <summary>
    /// Collects all results from a sequence of async ErrorOr instances.
    /// Returns all values if all are successful, or all accumulated errors if any failed.
    /// </summary>
    public static Task<VsaResult<List<TValue>>> CollectAsync<TValue>(IEnumerable<Task<VsaResult<TValue>>> tasks)
    {
        var taskArray = tasks.ToArray();
        return Task.WhenAll(taskArray)
            .ContinueWith(
                _ => VsaResultCombine.Collect(taskArray.Select(t => t.Result)),
                TaskContinuationOptions.ExecuteSynchronously);
    }

    /// <summary>
    /// Collects all results from an array of async ErrorOr instances.
    /// Returns all values if all are successful, or all accumulated errors if any failed.
    /// </summary>
    public static Task<VsaResult<List<TValue>>> CollectAsync<TValue>(params Task<VsaResult<TValue>>[] tasks) =>
        CollectAsync((IEnumerable<Task<VsaResult<TValue>>>)tasks);
}
