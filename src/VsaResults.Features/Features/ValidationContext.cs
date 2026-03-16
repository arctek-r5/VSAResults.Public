using VsaResults.Errors;
using VsaResults.VsaResult;

namespace VsaResults.Features.Features;

/// <summary>
/// Accumulates validation errors, enabling multiple validation checks before returning.
/// </summary>
/// <remarks>
/// <para>
/// Use this class to collect all validation errors at once, rather than returning
/// on the first error. This provides a better user experience by showing all issues
/// that need to be fixed.
/// </para>
/// <para>
/// Usage pattern:
/// <code>
/// var result = Validate()
///     .RequiredString(request.Title, "Adr.TitleRequired", "Title is required")
///     .RequiredString(request.Context, "Adr.ContextRequired", "Context is required")
///     .Must(request.Priority > 0, "Adr.InvalidPriority", "Priority must be positive")
///     .ToResult(request);
/// </code>
/// </para>
/// </remarks>
public sealed class ValidationContext
{
    private readonly List<Error> _errors = [];

    /// <summary>
    /// Gets the accumulated errors.
    /// </summary>
    public IReadOnlyList<Error> Errors => _errors;

    /// <summary>
    /// Gets a value indicating whether any errors have been accumulated.
    /// </summary>
    public bool HasErrors => _errors.Count > 0;

    /// <summary>
    /// Adds an error if the condition is true.
    /// </summary>
    /// <param name="condition">If true, the error is added.</param>
    /// <param name="error">The error to add.</param>
    /// <returns>This context for fluent chaining.</returns>
    public ValidationContext AddErrorIf(bool condition, Error error)
    {
        if (condition)
        {
            _errors.Add(error);
        }

        return this;
    }

    /// <summary>
    /// Unconditionally adds an error.
    /// </summary>
    /// <param name="error">The error to add.</param>
    /// <returns>This context for fluent chaining.</returns>
    public ValidationContext AddError(Error error)
    {
        _errors.Add(error);
        return this;
    }

    /// <summary>
    /// Adds multiple errors at once.
    /// </summary>
    /// <param name="errors">The errors to add.</param>
    /// <returns>This context for fluent chaining.</returns>
    public ValidationContext AddErrors(IEnumerable<Error> errors)
    {
        _errors.AddRange(errors);
        return this;
    }

    /// <summary>
    /// Converts this validation context to a result.
    /// Returns errors if any exist, otherwise returns the validated request.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <param name="request">The request being validated.</param>
    /// <returns>A result containing either the request or validation errors.</returns>
    public VsaResult<TRequest> ToResult<TRequest>(TRequest request)
        => HasErrors ? VsaResultFactory.FromErrors<TRequest>(_errors.ToArray()) : request.ToResult();
}
