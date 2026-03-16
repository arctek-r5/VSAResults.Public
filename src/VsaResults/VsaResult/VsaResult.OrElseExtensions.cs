using VsaResults.Errors;

namespace VsaResults.VsaResult;

/// <summary>
/// Extension methods for OrElse operations on Task-wrapped ErrorOr values.
/// </summary>
public static partial class VsaResultExtensions
{
    /// <summary>
    /// If the state is error, returns the result of invoking the fallback function.
    /// If the state is not error, returns the original ErrorOr.
    /// </summary>
    public static Task<VsaResult<TValue>> OrElse<TValue>(
        this Task<VsaResult<TValue>> errorOr,
        Func<List<Error>, VsaResult<TValue>> fallback) =>
        errorOr.ThenSync(result => result.OrElse(fallback));

    /// <summary>
    /// If the state is error, returns the provided fallback ErrorOr.
    /// If the state is not error, returns the original ErrorOr.
    /// </summary>
    public static Task<VsaResult<TValue>> OrElse<TValue>(
        this Task<VsaResult<TValue>> errorOr,
        VsaResult<TValue> fallback) =>
        errorOr.ThenSync(result => result.OrElse(fallback));

    /// <summary>
    /// If the state is error, returns the result of invoking the fallback function based on the first error.
    /// If the state is not error, returns the original ErrorOr.
    /// </summary>
    public static Task<VsaResult<TValue>> OrElseFirst<TValue>(
        this Task<VsaResult<TValue>> errorOr,
        Func<Error, VsaResult<TValue>> fallback) =>
        errorOr.ThenSync(result => result.OrElseFirst(fallback));

    /// <summary>
    /// If the state is error, returns the result of invoking the async fallback function.
    /// If the state is not error, returns the original ErrorOr.
    /// </summary>
    public static Task<VsaResult<TValue>> OrElseAsync<TValue>(
        this Task<VsaResult<TValue>> errorOr,
        Func<List<Error>, Task<VsaResult<TValue>>> fallback) =>
        errorOr.ThenAsync(result => result.OrElseAsync(fallback));

    /// <summary>
    /// If the state is error, awaits and returns the provided fallback Task.
    /// If the state is not error, returns the original ErrorOr.
    /// </summary>
    public static Task<VsaResult<TValue>> OrElseAsync<TValue>(
        this Task<VsaResult<TValue>> errorOr,
        Task<VsaResult<TValue>> fallback) =>
        errorOr.ThenAsync(result => result.OrElseAsync(fallback));

    /// <summary>
    /// If the state is error, returns the result of invoking the async fallback function based on the first error.
    /// If the state is not error, returns the original ErrorOr.
    /// </summary>
    public static Task<VsaResult<TValue>> OrElseFirstAsync<TValue>(
        this Task<VsaResult<TValue>> errorOr,
        Func<Error, Task<VsaResult<TValue>>> fallback) =>
        errorOr.ThenAsync(result => result.OrElseFirstAsync(fallback));
}
