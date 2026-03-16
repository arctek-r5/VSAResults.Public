using VsaResults.Errors;

namespace VsaResults.VsaResult;

public static partial class VsaResultExtensions
{
    /// <summary>
    /// Creates an <see cref="VsaResult{TValue}"/> instance with the given <paramref name="value"/>.
    /// </summary>
    public static VsaResult<TValue> ToResult<TValue>(this TValue value) =>
        value;

    /// <summary>
    /// Creates an <see cref="VsaResult{TValue}"/> instance with the given <paramref name="error"/>.
    /// </summary>
    public static VsaResult<TValue> ToResult<TValue>(this Error error) =>
        error;

    /// <summary>
    /// Creates an <see cref="VsaResult{TValue}"/> instance with the given <paramref name="errors"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="errors"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="errors" /> is an empty list.</exception>
    public static VsaResult<TValue> ToResult<TValue>(this List<Error> errors) =>
        errors;

    /// <summary>
    /// Creates an <see cref="VsaResult{TValue}"/> instance with the given <paramref name="errors"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="errors"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="errors" /> is an empty list.</exception>
    public static VsaResult<TValue> ToResult<TValue>(this IReadOnlyList<Error> errors) =>
        errors is List<Error> list ? list : [.. errors];

    /// <summary>
    /// Creates an <see cref="VsaResult{TValue}"/> instance with the given <paramref name="errors"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="errors"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="errors" /> is an empty array.</exception>
    public static VsaResult<TValue> ToResult<TValue>(this Error[] errors) =>
        errors;
}
