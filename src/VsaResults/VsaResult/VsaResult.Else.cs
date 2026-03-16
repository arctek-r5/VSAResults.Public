using VsaResults.Errors;

namespace VsaResults.VsaResult;

public readonly partial record struct VsaResult<TValue> : IVsaResult<TValue>
{
    /// <summary>
    /// If the state is error, the provided function <paramref name="onError"/> is executed and its result is returned.
    /// </summary>
    /// <param name="onError">The function to execute if the state is error.</param>
    /// <returns>The result from calling <paramref name="onError"/> if state is error; otherwise the original <see cref="Value"/>.</returns>
    public VsaResult<TValue> Else(Func<List<Error>, Error> onError) =>
        IsError ? onError(_errors) : Value;

    /// <summary>
    /// If the state is error, the provided function <paramref name="onError"/> is executed and its result is returned.
    /// </summary>
    /// <param name="onError">The function to execute if the state is error.</param>
    /// <returns>The result from calling <paramref name="onError"/> if state is error; otherwise the original <see cref="Value"/>.</returns>
    public VsaResult<TValue> Else(Func<List<Error>, List<Error>> onError) =>
        IsError ? onError(_errors) : Value;

    /// <summary>
    /// If the state is error, the provided <paramref name="error"/> is returned.
    /// </summary>
    /// <param name="error">The error to return.</param>
    /// <returns>The given <paramref name="error"/>.</returns>
    public VsaResult<TValue> Else(Error error) =>
        IsError ? error : Value;

    /// <summary>
    /// If the state is error, the provided function <paramref name="onError"/> is executed and its result is returned.
    /// </summary>
    /// <param name="onError">The function to execute if the state is error.</param>
    /// <returns>The result from calling <paramref name="onError"/> if state is error; otherwise the original <see cref="Value"/>.</returns>
    public VsaResult<TValue> Else(Func<List<Error>, TValue> onError) =>
        IsError ? onError(_errors) : Value;

    /// <summary>
    /// If the state is error, the provided function <paramref name="onError"/> is executed and its result is returned.
    /// </summary>
    /// <param name="onError">The value to return if the state is error.</param>
    /// <returns>The result from calling <paramref name="onError"/> if state is error; otherwise the original <see cref="Value"/>.</returns>
    public VsaResult<TValue> Else(TValue onError) =>
        IsError ? onError : Value;

    /// <summary>
    /// If the state is error, the provided function <paramref name="onError"/> is executed asynchronously and its result is returned.
    /// </summary>
    /// <param name="onError">The function to execute if the state is error.</param>
    /// <returns>The result from calling <paramref name="onError"/> if state is error; otherwise the original <see cref="Value"/>.</returns>
    public Task<VsaResult<TValue>> ElseAsync(Func<List<Error>, Task<TValue>> onError) =>
        IsError
            ? onError(_errors).ContinueWith(t => (VsaResult<TValue>)t.GetAwaiter().GetResult(), TaskContinuationOptions.ExecuteSynchronously)
            : Task.FromResult<VsaResult<TValue>>(Value);

    /// <summary>
    /// If the state is error, the provided function <paramref name="onError"/> is executed asynchronously and its result is returned.
    /// </summary>
    /// <param name="onError">The function to execute if the state is error.</param>
    /// <returns>The result from calling <paramref name="onError"/> if state is error; otherwise the original <see cref="Value"/>.</returns>
    public Task<VsaResult<TValue>> ElseAsync(Func<List<Error>, Task<Error>> onError) =>
        IsError
            ? onError(_errors).ContinueWith(t => (VsaResult<TValue>)t.GetAwaiter().GetResult(), TaskContinuationOptions.ExecuteSynchronously)
            : Task.FromResult<VsaResult<TValue>>(Value);

    /// <summary>
    /// If the state is error, the provided function <paramref name="onError"/> is executed asynchronously and its result is returned.
    /// </summary>
    /// <param name="onError">The function to execute if the state is error.</param>
    /// <returns>The result from calling <paramref name="onError"/> if state is error; otherwise the original <see cref="Value"/>.</returns>
    public Task<VsaResult<TValue>> ElseAsync(Func<List<Error>, Task<List<Error>>> onError) =>
        IsError
            ? onError(_errors).ContinueWith(t => (VsaResult<TValue>)t.GetAwaiter().GetResult(), TaskContinuationOptions.ExecuteSynchronously)
            : Task.FromResult<VsaResult<TValue>>(Value);

    /// <summary>
    /// If the state is error, the provided <paramref name="error"/> is awaited and returned.
    /// </summary>
    /// <param name="error">The error to return if the state is error.</param>
    /// <returns>The result from awaiting the given <paramref name="error"/>.</returns>
    public Task<VsaResult<TValue>> ElseAsync(Task<Error> error) =>
        IsError
            ? error.ContinueWith(t => (VsaResult<TValue>)t.GetAwaiter().GetResult(), TaskContinuationOptions.ExecuteSynchronously)
            : Task.FromResult<VsaResult<TValue>>(Value);

    /// <summary>
    /// If the state is error, the provided function <paramref name="onError"/> is executed asynchronously and its result is returned.
    /// </summary>
    /// <param name="onError">The function to execute if the state is error.</param>
    /// <returns>The result from calling <paramref name="onError"/> if state is error; otherwise the original <see cref="Value"/>.</returns>
    public Task<VsaResult<TValue>> ElseAsync(Task<TValue> onError) =>
        IsError
            ? onError.ContinueWith(t => (VsaResult<TValue>)t.GetAwaiter().GetResult(), TaskContinuationOptions.ExecuteSynchronously)
            : Task.FromResult<VsaResult<TValue>>(Value);
}
