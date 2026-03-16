using VsaResults.Errors;

namespace VsaResults.VsaResult;

public interface IVsaResult<out TValue> : IVsaResult
{
    /// <summary>
    /// Gets the value.
    /// </summary>
    TValue Value { get; }
}

/// <summary>
/// Type-less interface for the <see cref="IVsaResult{TValue}"/> object.
/// </summary>
/// <remarks>
/// This interface is intended for use when the underlying type of the <see cref="IVsaResult{TValue}"/> object is unknown.
/// </remarks>
public interface IVsaResult
{
    /// <summary>
    /// Gets the list of errors.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessed and the result is not in an error state.</exception>
    IReadOnlyList<Error> Errors { get; }

    /// <summary>
    /// Gets a value indicating whether the state is error.
    /// </summary>
    bool IsError { get; }
}
