namespace VsaResults.Errors;

/// <summary>
/// Error types.
/// </summary>
public enum ErrorType
{
    /// <summary>A general failure error.</summary>
    Failure = 0,

    /// <summary>An unexpected error.</summary>
    Unexpected = 1,

    /// <summary>A validation error.</summary>
    Validation = 2,

    /// <summary>A conflict error (HTTP 409).</summary>
    Conflict = 3,

    /// <summary>A not found error (HTTP 404).</summary>
    NotFound = 4,

    /// <summary>An unauthorized error (HTTP 401).</summary>
    Unauthorized = 5,

    /// <summary>A forbidden error (HTTP 403).</summary>
    Forbidden = 6,

    /// <summary>A bad request error (HTTP 400).</summary>
    BadRequest = 7,

    /// <summary>A timeout error (HTTP 408).</summary>
    Timeout = 8,

    /// <summary>A gone error for permanently removed resources (HTTP 410).</summary>
    Gone = 9,

    /// <summary>A locked error for resources that are locked (HTTP 423).</summary>
    Locked = 10,

    /// <summary>A rate limiting error (HTTP 429).</summary>
    TooManyRequests = 11,

    /// <summary>A service unavailable error (HTTP 503).</summary>
    Unavailable = 12,
}
