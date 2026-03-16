using VsaResults.Errors;

namespace VsaResults.VsaResult;

public readonly partial record struct VsaResult<TValue> : IVsaResult<TValue>
{
    /// <summary>
    /// Executes a function and wraps any thrown exception as an Error.
    /// </summary>
    /// <param name="func">The function to execute.</param>
    /// <param name="errorMapper">An optional function to map exceptions to errors. If not provided, creates an Unexpected error.</param>
    /// <returns>The result of the function or an error if an exception was thrown.</returns>
    public static VsaResult<TValue> Try(Func<TValue> func, Func<Exception, Error>? errorMapper = null)
    {
        try
        {
            return func();
        }
        catch (Exception ex)
        {
            return errorMapper?.Invoke(ex) ?? Error.Unexpected(
                code: ex.GetType().Name,
                description: ex.Message,
                metadata: new Dictionary<string, object> { { MetadataKeys.Exception, ex } });
        }
    }

    /// <summary>
    /// Executes an async function and wraps any thrown exception as an Error.
    /// </summary>
    /// <param name="func">The async function to execute.</param>
    /// <param name="errorMapper">An optional function to map exceptions to errors. If not provided, creates an Unexpected error.</param>
    /// <returns>The result of the function or an error if an exception was thrown.</returns>
    public static async Task<VsaResult<TValue>> TryAsync(
        Func<Task<TValue>> func,
        Func<Exception, Error>? errorMapper = null)
    {
        try
        {
            return await func().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return errorMapper?.Invoke(ex) ?? Error.Unexpected(
                code: ex.GetType().Name,
                description: ex.Message,
                metadata: new Dictionary<string, object> { { MetadataKeys.Exception, ex } });
        }
    }

    /// <summary>
    /// Executes an async function and wraps any thrown exception as an Error.
    /// </summary>
    /// <param name="func">The async function to execute.</param>
    /// <param name="errorMapper">An optional async function to map exceptions to errors. If not provided, creates an Unexpected error.</param>
    /// <returns>The result of the function or an error if an exception was thrown.</returns>
    public static async Task<VsaResult<TValue>> TryAsync(
        Func<Task<TValue>> func,
        Func<Exception, Task<Error>> errorMapper)
    {
        try
        {
            return await func().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return await errorMapper(ex).ConfigureAwait(false);
        }
    }

    private static class MetadataKeys
    {
        public const string Exception = "Exception";
    }
}
