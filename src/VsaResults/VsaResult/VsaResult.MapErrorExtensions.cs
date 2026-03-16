using VsaResults.Errors;

namespace VsaResults.VsaResult;

/// <summary>
/// Extension methods for mapping errors in Task-wrapped ErrorOr values.
/// </summary>
public static partial class VsaResultExtensions
{
    /// <summary>
    /// If the state is error, transforms each error using the given mapper function.
    /// If the state is not error, returns the original ErrorOr unchanged.
    /// </summary>
    public static Task<VsaResult<TValue>> MapError<TValue>(
        this Task<VsaResult<TValue>> errorOr,
        Func<Error, Error> mapper) =>
        errorOr.ThenSync(result => result.MapError(mapper));

    /// <summary>
    /// If the state is error, transforms the entire error list using the given mapper function.
    /// If the state is not error, returns the original ErrorOr unchanged.
    /// </summary>
    public static Task<VsaResult<TValue>> MapErrors<TValue>(
        this Task<VsaResult<TValue>> errorOr,
        Func<List<Error>, List<Error>> mapper) =>
        errorOr.ThenSync(result => result.MapErrors(mapper));

    /// <summary>
    /// If the state is error, transforms each error using the given async mapper function.
    /// If the state is not error, returns the original ErrorOr unchanged.
    /// </summary>
    public static Task<VsaResult<TValue>> MapErrorAsync<TValue>(
        this Task<VsaResult<TValue>> errorOr,
        Func<Error, Task<Error>> mapper) =>
        errorOr.ThenAsync(result => result.MapErrorAsync(mapper));

    /// <summary>
    /// If the state is error, transforms the entire error list using the given async mapper function.
    /// If the state is not error, returns the original ErrorOr unchanged.
    /// </summary>
    public static Task<VsaResult<TValue>> MapErrorsAsync<TValue>(
        this Task<VsaResult<TValue>> errorOr,
        Func<List<Error>, Task<List<Error>>> mapper) =>
        errorOr.ThenAsync(result => result.MapErrorsAsync(mapper));
}
