namespace VsaResults.Features.Features;

/// <summary>
/// Provides ambient access to request metadata for automatic enrichment of FeatureContext.
/// </summary>
/// <remarks>
/// <para>
/// Use <see cref="Begin"/> at the start of a request to establish a scope, and dispose it at the end.
/// Within the scope, <see cref="Current"/> provides access to the request metadata.
/// </para>
/// <para>
/// In ASP.NET Core, this should be set up via middleware that creates the scope at the
/// start of each request.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // In middleware
/// using (RequestMetadataScope.Begin(new MyRequestMetadataProvider(httpContext)))
/// {
///     await next(context);
/// }
/// </code>
/// </example>
public static class RequestMetadataScope
{
    private static readonly AsyncLocal<IRequestMetadataProvider?> CurrentProvider = new();

    /// <summary>
    /// Gets the current request metadata provider, or null if not in a scope.
    /// </summary>
    public static IRequestMetadataProvider? Current => CurrentProvider.Value;

    /// <summary>
    /// Begins a new request metadata scope.
    /// </summary>
    /// <param name="provider">The request metadata provider for this scope.</param>
    /// <returns>A disposable that should be disposed when the scope ends.</returns>
    public static IDisposable Begin(IRequestMetadataProvider provider)
    {
        var previous = CurrentProvider.Value;
        CurrentProvider.Value = provider;
        return new Scope(previous);
    }

    private sealed class Scope(IRequestMetadataProvider? previous) : IDisposable
    {
        public void Dispose() => CurrentProvider.Value = previous;
    }
}
