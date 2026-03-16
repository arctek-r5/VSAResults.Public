using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using VsaResults.Errors;

namespace VsaResults.VsaResult;

/// <summary>
/// A discriminated union of errors or a value.
/// </summary>
/// <typeparam name="TValue">The type of the underlying <see cref="Value"/>.</typeparam>
public readonly partial record struct VsaResult<TValue> : IVsaResult<TValue>
{
    // ReSharper disable InconsistentNaming — internal fields accessed by partial struct files
    internal readonly ImmutableDictionary<string, object>? _context = null;
    private readonly TValue? _value = default;
    internal readonly List<Error>? _errors = null;
    // ReSharper restore InconsistentNaming

    /// <summary>
    /// Prevents a default <see cref="VsaResult{TValue}"/> struct from being created.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when this method is called.</exception>
    public VsaResult()
    {
        throw new InvalidOperationException(ExceptionMessages.DefaultConstruction);
    }

    /// <summary>
    /// Internal constructor for creating a VsaResult with a value and context (for context propagation).
    /// </summary>
    internal VsaResult(TValue value, ImmutableDictionary<string, object>? context)
    {
        ArgumentNullException.ThrowIfNull(value);

        _value = value;
        _context = context;
    }

    /// <summary>
    /// Internal constructor for creating a VsaResult with errors and context (for context propagation).
    /// </summary>
    internal VsaResult(List<Error> errors, ImmutableDictionary<string, object>? context)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (errors.Count == 0)
        {
            throw new ArgumentException(ExceptionMessages.EmptyErrorCollection, nameof(errors));
        }

        _errors = errors;
        _context = context;
    }

    private VsaResult(Error error)
    {
        _errors = [error];
    }

    private VsaResult(List<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (errors.Count == 0)
        {
            throw new ArgumentException(ExceptionMessages.EmptyErrorCollection, nameof(errors));
        }

        _errors = errors;
    }

    private VsaResult(TValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        _value = value;
    }

    /// <summary>
    /// Gets the accumulated context for wide events / canonical log lines.
    /// Context flows through all transformations and can be emitted as a single structured event.
    /// </summary>
    public IReadOnlyDictionary<string, object> Context => _context ?? [];

    /// <summary>
    /// Gets a value indicating whether the state is error.
    /// </summary>
    [MemberNotNullWhen(true, nameof(_errors))]
    [MemberNotNullWhen(true, nameof(Errors))]
    [MemberNotNullWhen(false, nameof(Value))]
    [MemberNotNullWhen(false, nameof(_value))]
    public bool IsError => _errors is not null;

    /// <summary>
    /// Gets the list of errors. If the state is not error, the list will contain a single error representing the state.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no errors are present.</exception>
    public IReadOnlyList<Error> Errors => IsError ? _errors : throw new InvalidOperationException(ExceptionMessages.ErrorsAccessWithoutErrors);

    /// <summary>
    /// Gets the list of errors. If the state is not error, the list will be empty.
    /// </summary>
    public IReadOnlyList<Error> ErrorsOrEmptyList => IsError ? _errors : Array.Empty<Error>();

    /// <summary>
    /// Gets the value.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no value is present.</exception>
    public TValue Value
    {
        get
        {
            if (IsError)
            {
                throw new InvalidOperationException(ExceptionMessages.ValueAccessWithErrors);
            }

            return _value;
        }
    }

    /// <summary>
    /// Gets the first error.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no errors are present.</exception>
    public Error FirstError
    {
        get
        {
            if (!IsError)
            {
                throw new InvalidOperationException(ExceptionMessages.FirstErrorAccessWithoutErrors);
            }

            return _errors[0];
        }
    }

    /// <summary>
    /// Creates an <see cref="VsaResult{TValue}"/> from a list of errors.
    /// </summary>
    public static VsaResult<TValue> From(List<Error> errors) =>
        errors;

    /// <summary>
    /// Gets the value if not in error state, or throws an <see cref="InvalidOperationException"/> with a descriptive message.
    /// </summary>
    /// <param name="message">Optional custom message for the exception.</param>
    /// <returns>The underlying value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the state is error.</exception>
    public TValue GetValueOrThrow(string? message = null)
    {
        if (IsError)
        {
            var errorCodes = string.Join(", ", _errors.Select(e => e.Code));
            throw new InvalidOperationException(
                message ?? $"Expected value but got {_errors.Count} error(s): [{errorCodes}]");
        }

        return _value;
    }

    /// <summary>
    /// Attempts to get the value if this instance is not in an error state.
    /// </summary>
    /// <param name="value">When this method returns, contains the value if this instance is not in an error state; otherwise, the default value.</param>
    /// <returns><c>true</c> if this instance contains a value; otherwise, <c>false</c>.</returns>
    public bool TryGetValue([NotNullWhen(true)] out TValue? value)
    {
        if (IsError)
        {
            value = default;
            return false;
        }

        value = _value;
        return true;
    }

    /// <summary>
    /// Attempts to get the errors if this instance is in an error state.
    /// Returns a defensive copy to prevent external mutation of the error list.
    /// </summary>
    /// <param name="errors">When this method returns, contains a copy of the errors if this instance is in an error state; otherwise, null.</param>
    /// <returns><c>true</c> if this instance contains errors; otherwise, <c>false</c>.</returns>
    public bool TryGetErrors([NotNullWhen(true)] out List<Error>? errors)
    {
        if (IsError)
        {
            errors = [.. _errors];
            return true;
        }

        errors = null;
        return false;
    }

    /// <summary>
    /// Gets the value if not in error state, or returns the specified default value.
    /// </summary>
    /// <param name="defaultValue">The value to return if this instance is in an error state.</param>
    /// <returns>The underlying value if not in error state; otherwise, the default value.</returns>
    public TValue? GetValueOrDefault(TValue? defaultValue = default) =>
        IsError ? defaultValue : _value;

    /// <summary>
    /// Returns a string representation of this ErrorOr for debugging purposes.
    /// </summary>
    public override string ToString()
    {
        if (IsError)
        {
            var errorSummary = _errors.Count == 1
                ? _errors[0].Code
                : $"{_errors[0].Code} (+{_errors.Count - 1} more)";
            return $"VsaResult {{ IsError = True, Errors = [{errorSummary}] }}";
        }

        var valueStr = _value?.ToString() ?? "null";
        if (valueStr.Length > 50)
        {
            valueStr = $"{valueStr[..47]}...";
        }

        return $"VsaResult {{ IsError = False, Value = {valueStr} }}";
    }

    private static class ExceptionMessages
    {
        public const string DefaultConstruction = "Default construction of VsaResult<TValue> is invalid. Please use provided factory methods to instantiate.";
        public const string EmptyErrorCollection = "Cannot create an VsaResult<TValue> from an empty collection of errors. Provide at least one error.";
        public const string ErrorsAccessWithoutErrors = "The Errors property cannot be accessed when no errors have been recorded. Check IsError before accessing Errors.";
        public const string ValueAccessWithErrors = "The Value property cannot be accessed when errors have been recorded. Check IsError before accessing Value.";
        public const string FirstErrorAccessWithoutErrors = "The FirstError property cannot be accessed when no errors have been recorded. Check IsError before accessing FirstError.";
    }
}
