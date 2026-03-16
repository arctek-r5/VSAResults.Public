using VsaResults.Errors;

namespace VsaResults.Features.Features;

/// <summary>
/// Factory methods for common feature errors.
/// Provides consistent error creation patterns across features.
/// </summary>
public static class FeatureErrors
{
    /// <summary>
    /// Creates a NotFound error for an entity.
    /// </summary>
    /// <param name="entity">The entity type name (e.g., "User", "Order").</param>
    /// <param name="id">The identifier that was not found.</param>
    /// <returns>A NotFound error.</returns>
    public static Error NotFound(string entity, object id)
        => Error.NotFound($"{entity}{ErrorCodes.NotFoundSuffix}", $"{entity} with id '{id}' was not found.");

    /// <summary>
    /// Creates a NotFound error for an entity.
    /// </summary>
    /// <param name="entity">The entity type name.</param>
    /// <param name="description">Custom description.</param>
    /// <returns>A NotFound error.</returns>
    public static Error NotFound(string entity, string description)
        => Error.NotFound($"{entity}{ErrorCodes.NotFoundSuffix}", description);

    /// <summary>
    /// Creates a Validation error.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="description">The error description.</param>
    /// <returns>A Validation error.</returns>
    public static Error Validation(string code, string description)
        => Error.Validation(code, description);

    /// <summary>
    /// Creates a Forbidden error.
    /// </summary>
    /// <param name="description">The error description.</param>
    /// <returns>A Forbidden error.</returns>
    public static Error Forbidden(string description)
        => Error.Forbidden(ErrorCodes.AccessForbidden, description);

    /// <summary>
    /// Creates a Forbidden error with a custom code.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="description">The error description.</param>
    /// <returns>A Forbidden error.</returns>
    public static Error Forbidden(string code, string description)
        => Error.Forbidden(code, description);

    /// <summary>
    /// Creates a Conflict error.
    /// </summary>
    /// <param name="description">The error description.</param>
    /// <returns>A Conflict error.</returns>
    public static Error Conflict(string description)
        => Error.Conflict(ErrorCodes.StateConflict, description);

    /// <summary>
    /// Creates a Conflict error with a custom code.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="description">The error description.</param>
    /// <returns>A Conflict error.</returns>
    public static Error Conflict(string code, string description)
        => Error.Conflict(code, description);

    /// <summary>
    /// Creates a service/infrastructure error.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="description">The error description.</param>
    /// <returns>A Failure error.</returns>
    public static Error ServiceError(string code, string description)
        => Error.Failure(code, description);

    /// <summary>
    /// Creates an Unauthorized error.
    /// </summary>
    /// <returns>An Unauthorized error.</returns>
    public static Error Unauthorized()
        => Error.Unauthorized(ErrorCodes.AuthUnauthorized, ErrorMessages.UserNotAuthenticated);

    /// <summary>
    /// Creates an Unauthorized error with a custom description.
    /// </summary>
    /// <param name="description">The error description.</param>
    /// <returns>An Unauthorized error.</returns>
    public static Error Unauthorized(string description)
        => Error.Unauthorized(ErrorCodes.AuthUnauthorized, description);

    /// <summary>
    /// Creates an Unexpected error for unhandled exceptions.
    /// </summary>
    /// <param name="description">The error description.</param>
    /// <returns>An Unexpected error.</returns>
    public static Error Unexpected(string description)
        => Error.Unexpected(ErrorCodes.InternalUnexpected, description);

    private static class ErrorCodes
    {
        public const string NotFoundSuffix = ".NotFound";
        public const string AccessForbidden = "Access.Forbidden";
        public const string StateConflict = "State.Conflict";
        public const string AuthUnauthorized = "Auth.Unauthorized";
        public const string InternalUnexpected = "Internal.Unexpected";
    }

    private static class ErrorMessages
    {
        public const string UserNotAuthenticated = "User is not authenticated.";
    }
}
