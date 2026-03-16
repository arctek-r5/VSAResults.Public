using VsaResults.Errors;

namespace VsaResults.VsaResult;

public readonly partial record struct VsaResult<TValue> : IVsaResult<TValue>
{
    /// <summary>
    /// If the state is error, transforms each error using the given mapper function.
    /// If the state is not error, returns the original ErrorOr unchanged.
    /// </summary>
    /// <param name="mapper">The function to transform each error.</param>
    /// <returns>A new <see cref="VsaResult{TValue}"/> with transformed errors, or the original if not in error state.</returns>
    public VsaResult<TValue> MapError(Func<Error, Error> mapper) =>
        IsError ? _errors.Select(mapper).ToList() : this;

    /// <summary>
    /// If the state is error, transforms the entire error list using the given mapper function.
    /// If the state is not error, returns the original ErrorOr unchanged.
    /// </summary>
    /// <param name="mapper">The function to transform the error list.</param>
    /// <returns>A new <see cref="VsaResult{TValue}"/> with transformed errors, or the original if not in error state.</returns>
    public VsaResult<TValue> MapErrors(Func<List<Error>, List<Error>> mapper) =>
        IsError ? mapper(_errors) : this;

    /// <summary>
    /// If the state is error, transforms each error using the given async mapper function.
    /// If the state is not error, returns the original ErrorOr unchanged.
    /// </summary>
    /// <param name="mapper">The async function to transform each error.</param>
    /// <returns>A new <see cref="VsaResult{TValue}"/> with transformed errors, or the original if not in error state.</returns>
    public async Task<VsaResult<TValue>> MapErrorAsync(Func<Error, Task<Error>> mapper)
    {
        if (!IsError)
        {
            return this;
        }

        var mappedErrors = new List<Error>(_errors.Count);
        foreach (var error in _errors)
        {
            mappedErrors.Add(await mapper(error).ConfigureAwait(false));
        }

        return mappedErrors;
    }

    /// <summary>
    /// If the state is error, transforms the entire error list using the given async mapper function.
    /// If the state is not error, returns the original ErrorOr unchanged.
    /// </summary>
    /// <param name="mapper">The async function to transform the error list.</param>
    /// <returns>A new <see cref="VsaResult{TValue}"/> with transformed errors, or the original if not in error state.</returns>
    public Task<VsaResult<TValue>> MapErrorsAsync(Func<List<Error>, Task<List<Error>>> mapper) =>
        IsError
            ? mapper(_errors).ContinueWith(t => (VsaResult<TValue>)t.GetAwaiter().GetResult(), TaskContinuationOptions.ExecuteSynchronously)
            : Task.FromResult(this);
}
