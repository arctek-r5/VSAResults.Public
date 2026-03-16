using VsaResults.Errors;

namespace VsaResults.VsaResult;

public static partial class VsaResultExtensions
{
    /// <summary>
    /// Executes the appropriate action based on the state of the <see cref="VsaResult{TValue}"/>.
    /// If the state is an error, the provided action <paramref name="onError"/> is executed.
    /// If the state is a value, the provided action <paramref name="onValue"/> is executed.
    /// </summary>
    /// <typeparam name="TValue">The type of the underlying value in the <paramref name="errorOr"/>.</typeparam>
    /// <param name="errorOr">The <see cref="ErrorOr"/> instance.</param>
    /// <param name="onValue">The action to execute if the state is a value.</param>
    /// <param name="onError">The action to execute if the state is an error.</param>
    /// <returns>The result of the executed function.</returns>
    public static Task Switch<TValue>(
        this Task<VsaResult<TValue>> errorOr,
        Action<TValue> onValue,
        Action<List<Error>> onError) =>
        errorOr.ThenSync(result => result.Switch(onValue, onError));

    /// <summary>
    /// Executes the appropriate action based on the state of the <see cref="VsaResult{TValue}"/>.
    /// If the state is an error, the provided action <paramref name="onError"/> is executed.
    /// If the state is a value, the provided action <paramref name="onValue"/> is executed.
    /// </summary>
    /// <typeparam name="TValue">The type of the underlying value in the <paramref name="errorOr"/>.</typeparam>
    /// <param name="errorOr">The <see cref="ErrorOr"/> instance.</param>
    /// <param name="onValue">The action to execute if the state is a value.</param>
    /// <param name="onError">The action to execute if the state is an error.</param>
    /// <returns>The result of the executed function.</returns>
    public static Task SwitchAsync<TValue>(
        this Task<VsaResult<TValue>> errorOr,
        Func<TValue, Task> onValue,
        Func<List<Error>, Task> onError) =>
        errorOr.ThenAsync(result => result.SwitchAsync(onValue, onError));

    /// <summary>
    /// Executes the appropriate action based on the state of the <see cref="VsaResult{TValue}"/>.
    /// If the state is an error, the provided action <paramref name="onError"/> is executed.
    /// If the state is a value, the provided action <paramref name="onValue"/> is executed.
    /// </summary>
    /// <typeparam name="TValue">The type of the underlying value in the <paramref name="errorOr"/>.</typeparam>
    /// <param name="errorOr">The <see cref="ErrorOr"/> instance.</param>
    /// <param name="onValue">The action to execute if the state is a value.</param>
    /// <param name="onError">The action to execute if the state is an error.</param>
    /// <returns>The result of the executed function.</returns>
    public static Task SwitchFirst<TValue>(
        this Task<VsaResult<TValue>> errorOr,
        Action<TValue> onValue,
        Action<Error> onError) =>
        errorOr.ThenSync(result => result.SwitchFirst(onValue, onError));

    /// <summary>
    /// Executes the appropriate action based on the state of the <see cref="VsaResult{TValue}"/>.
    /// If the state is an error, the provided action <paramref name="onError"/> is executed.
    /// If the state is a value, the provided action <paramref name="onValue"/> is executed.
    /// </summary>
    /// <typeparam name="TValue">The type of the underlying value in the <paramref name="errorOr"/>.</typeparam>
    /// <param name="errorOr">The <see cref="ErrorOr"/> instance.</param>
    /// <param name="onValue">The action to execute if the state is a value.</param>
    /// <param name="onError">The action to execute if the state is an error.</param>
    /// <returns>The result of the executed function.</returns>
    public static Task SwitchFirstAsync<TValue>(
        this Task<VsaResult<TValue>> errorOr,
        Func<TValue, Task> onValue,
        Func<Error, Task> onError) =>
        errorOr.ThenAsync(result => result.SwitchFirstAsync(onValue, onError));
}
