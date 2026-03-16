using VsaResults.Errors;

namespace VsaResults.VsaResult;

public static partial class VsaResultExtensions
{
    /// <summary>
    /// If the state is error, the provided function <paramref name="onError"/> is executed asynchronously and its result is returned.
    /// </summary>
    /// <typeparam name="TValue">The type of the underlying value in the <paramref name="errorOr"/>.</typeparam>
    /// <param name="errorOr">The <see cref="ErrorOr"/> instance.</param>
    /// <param name="onError">The function to execute if the state is error.</param>
    /// <returns>The result from calling <paramref name="onError"/> if state is error; otherwise the original value.</returns>
    public static Task<VsaResult<TValue>> Else<TValue>(
        this Task<VsaResult<TValue>> errorOr,
        Func<List<Error>, TValue> onError) =>
        errorOr.ThenSync(result => result.Else(onError));

    /// <summary>
    /// If the state is error, the provided function <paramref name="onError"/> is executed asynchronously and its result is returned.
    /// </summary>
    /// <typeparam name="TValue">The type of the underlying value in the <paramref name="errorOr"/>.</typeparam>
    /// <param name="errorOr">The <see cref="ErrorOr"/> instance.</param>
    /// <param name="onError">The function to execute if the state is error.</param>
    /// <returns>The result from calling <paramref name="onError"/> if state is error; otherwise the original value.</returns>
    public static Task<VsaResult<TValue>> Else<TValue>(
        this Task<VsaResult<TValue>> errorOr,
        TValue onError) =>
        errorOr.ThenSync(result => result.Else(onError));

    /// <summary>
    /// If the state is error, the provided function <paramref name="onError"/> is executed asynchronously and its result is returned.
    /// </summary>
    /// <typeparam name="TValue">The type of the underlying value in the <paramref name="errorOr"/>.</typeparam>
    /// <param name="errorOr">The <see cref="ErrorOr"/> instance.</param>
    /// <param name="onError">The function to execute if the state is error.</param>
    /// <returns>The result from calling <paramref name="onError"/> if state is error; otherwise the original value.</returns>
    public static Task<VsaResult<TValue>> ElseAsync<TValue>(
        this Task<VsaResult<TValue>> errorOr,
        Func<List<Error>, Task<TValue>> onError) =>
        errorOr.ThenAsync(result => result.ElseAsync(onError));

    /// <summary>
    /// If the state is error, the provided function <paramref name="onError"/> is executed asynchronously and its result is returned.
    /// </summary>
    /// <typeparam name="TValue">The type of the underlying value in the <paramref name="errorOr"/>.</typeparam>
    /// <param name="errorOr">The <see cref="ErrorOr"/> instance.</param>
    /// <param name="onError">The function to execute if the state is error.</param>
    /// <returns>The result from calling <paramref name="onError"/> if state is error; otherwise the original value.</returns>
    public static Task<VsaResult<TValue>> ElseAsync<TValue>(
        this Task<VsaResult<TValue>> errorOr,
        Task<TValue> onError) =>
        errorOr.ThenAsync(result => result.ElseAsync(onError));

    /// <summary>
    /// If the state is error, the provided function <paramref name="onError"/> is executed asynchronously and its result is returned.
    /// </summary>
    /// <typeparam name="TValue">The type of the underlying value in the <paramref name="errorOr"/>.</typeparam>
    /// <param name="errorOr">The <see cref="ErrorOr"/> instance.</param>
    /// <param name="onError">The function to execute if the state is error.</param>
    /// <returns>The result from calling <paramref name="onError"/> if state is error; otherwise the original value.</returns>
    public static Task<VsaResult<TValue>> Else<TValue>(
        this Task<VsaResult<TValue>> errorOr,
        Func<List<Error>, Error> onError) =>
        errorOr.ThenSync(result => result.Else(onError));

    /// <summary>
    /// If the state is error, the provided function <paramref name="onError"/> is executed asynchronously and its result is returned.
    /// </summary>
    /// <typeparam name="TValue">The type of the underlying value in the <paramref name="errorOr"/>.</typeparam>
    /// <param name="errorOr">The <see cref="ErrorOr"/> instance.</param>
    /// <param name="onError">The function to execute if the state is error.</param>
    /// <returns>The result from calling <paramref name="onError"/> if state is error; otherwise the original value.</returns>
    public static Task<VsaResult<TValue>> Else<TValue>(
        this Task<VsaResult<TValue>> errorOr,
        Func<List<Error>, List<Error>> onError) =>
        errorOr.ThenSync(result => result.Else(onError));

    /// <summary>
    /// If the state is error, the provided <paramref name="error"/> is returned.
    /// </summary>
    /// <typeparam name="TValue">The type of the underlying value in the <paramref name="errorOr"/>.</typeparam>
    /// <param name="errorOr">The <see cref="ErrorOr"/> instance.</param>
    /// <param name="error">The error to return.</param>
    /// <returns>The given <paramref name="error"/>.</returns>
    public static Task<VsaResult<TValue>> Else<TValue>(
        this Task<VsaResult<TValue>> errorOr,
        Error error) =>
        errorOr.ThenSync(result => result.Else(error));

    /// <summary>
    /// If the state is error, the provided function <paramref name="onError"/> is executed asynchronously and its result is returned.
    /// </summary>
    /// <typeparam name="TValue">The type of the underlying value in the <paramref name="errorOr"/>.</typeparam>
    /// <param name="errorOr">The <see cref="ErrorOr"/> instance.</param>
    /// <param name="onError">The function to execute if the state is error.</param>
    /// <returns>The result from calling <paramref name="onError"/> if state is error; otherwise the original value.</returns>
    public static Task<VsaResult<TValue>> ElseAsync<TValue>(
        this Task<VsaResult<TValue>> errorOr,
        Func<List<Error>, Task<Error>> onError) =>
        errorOr.ThenAsync(result => result.ElseAsync(onError));

    /// <summary>
    /// If the state is error, the provided function <paramref name="onError"/> is executed asynchronously and its result is returned.
    /// </summary>
    /// <typeparam name="TValue">The type of the underlying value in the <paramref name="errorOr"/>.</typeparam>
    /// <param name="errorOr">The <see cref="ErrorOr"/> instance.</param>
    /// <param name="onError">The function to execute if the state is error.</param>
    /// <returns>The result from calling <paramref name="onError"/> if state is error; otherwise the original value.</returns>
    public static Task<VsaResult<TValue>> ElseAsync<TValue>(
        this Task<VsaResult<TValue>> errorOr,
        Func<List<Error>, Task<List<Error>>> onError) =>
        errorOr.ThenAsync(result => result.ElseAsync(onError));

    /// <summary>
    /// If the state is error, the provided function <paramref name="onError"/> is executed asynchronously and its result is returned.
    /// </summary>
    /// <typeparam name="TValue">The type of the underlying value in the <paramref name="errorOr"/>.</typeparam>
    /// <param name="errorOr">The <see cref="ErrorOr"/> instance.</param>
    /// <param name="onError">The function to execute if the state is error.</param>
    /// <returns>The result from calling <paramref name="onError"/> if state is error; otherwise the original value.</returns>
    public static Task<VsaResult<TValue>> ElseAsync<TValue>(
        this Task<VsaResult<TValue>> errorOr,
        Task<Error> onError) =>
        errorOr.ThenAsync(result => result.ElseAsync(onError));
}
