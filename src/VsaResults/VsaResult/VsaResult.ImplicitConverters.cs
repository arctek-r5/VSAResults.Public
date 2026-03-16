using VsaResults.Errors;

namespace VsaResults.VsaResult;

public readonly partial record struct VsaResult<TValue> : IVsaResult<TValue>
{
    /// <summary>
    /// Creates an <see cref="VsaResult{TValue}"/> from a value.
    /// </summary>
    public static implicit operator VsaResult<TValue>(TValue value) =>
        new VsaResult<TValue>(value);

    /// <summary>
    /// Creates an <see cref="VsaResult{TValue}"/> from an error.
    /// </summary>
    public static implicit operator VsaResult<TValue>(Error error) =>
        new VsaResult<TValue>(error);

    /// <summary>
    /// Creates an <see cref="VsaResult{TValue}"/> from a list of errors.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="errors"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="errors" /> is an empty list.</exception>
    public static implicit operator VsaResult<TValue>(List<Error> errors) =>
        new VsaResult<TValue>(errors);

    /// <summary>
    /// Creates an <see cref="VsaResult{TValue}"/> from a list of errors.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="errors"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="errors" /> is an empty array.</exception>
    public static implicit operator VsaResult<TValue>(Error[] errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        return new VsaResult<TValue>([.. errors]);
    }
}
