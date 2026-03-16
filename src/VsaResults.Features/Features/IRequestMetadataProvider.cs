namespace VsaResults.Features.Features;

/// <summary>
/// Provides request-level metadata (IP address, user agent, etc.) for automatic
/// enrichment of FeatureContext during pipeline execution.
/// </summary>
/// <remarks>
/// <para>
/// Implement this interface in your host application to automatically populate
/// request metadata in the FeatureContext. The metadata is added after Requirements
/// enforcement succeeds, making it available to Mutators and SideEffects.
/// </para>
/// <para>
/// Common implementation: use <c>IHttpContextAccessor</c> in ASP.NET Core to extract
/// values from the current HTTP request.
/// </para>
/// </remarks>
public interface IRequestMetadataProvider
{
    /// <summary>
    /// Gets the client IP address (e.g., from X-Forwarded-For or RemoteIpAddress).
    /// </summary>
    string? IpAddress { get; }

    /// <summary>
    /// Gets the User-Agent header value.
    /// </summary>
    string? UserAgent { get; }

    /// <summary>
    /// Gets the request trace/correlation ID.
    /// </summary>
    string? TraceId { get; }

    /// <summary>
    /// Gets the request path (e.g., "/api/tenants/123").
    /// </summary>
    string? RequestPath { get; }

    /// <summary>
    /// Gets the HTTP method (e.g., "GET", "POST").
    /// </summary>
    string? RequestMethod { get; }
}
