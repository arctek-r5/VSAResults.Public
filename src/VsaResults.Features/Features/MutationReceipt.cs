namespace VsaResults.Features.Features;

/// <summary>
/// Captures the client-safe projection of a mutation's pipeline context.
/// Transported out of the pipeline via <see cref="VsaResult.VsaResult{TValue}.Context"/>
/// under the <see cref="ContextKey"/> key, then projected into the API response envelope
/// by the FeatureHandler.
/// </summary>
/// <remarks>
/// <para>
/// This is part of the dual-zone context architecture:
/// </para>
/// <list type="bullet">
///   <item>
///     <term>Internal zone</term>
///     <description><c>FeatureContext.WideEventContext</c> → wide event + side effects</description>
///   </item>
///   <item>
///     <term>Client zone</term>
///     <description><c>FeatureContext.ClientContext</c> → <see cref="MutationReceipt"/> → API response</description>
///   </item>
/// </list>
/// </remarks>
public sealed class MutationReceipt
{
    /// <summary>
    /// The well-known key used to store the receipt in <see cref="VsaResult.VsaResult{TValue}.Context"/>.
    /// </summary>
    public const string ContextKey = "_receipt";

    /// <summary>
    /// Gets the human-readable message describing the mutation outcome.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Gets the client-safe context entries from the pipeline.
    /// </summary>
    public IReadOnlyDictionary<string, object?> ClientContext { get; init; } = new Dictionary<string, object?>();

    /// <summary>
    /// Creates a <see cref="MutationReceipt"/> from a <see cref="FeatureContext{TRequest}"/>.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <param name="context">The feature context to extract the client zone from.</param>
    /// <returns>A receipt containing the client-safe context, or null if no client context was set.</returns>
    public static MutationReceipt? FromContext<TRequest>(FeatureContext<TRequest>? context)
    {
        if (context is null)
        {
            return null;
        }

        if (context.ReceiptMessage is null && context.ClientContext.Count == 0)
        {
            return null;
        }

        return new MutationReceipt
        {
            Message = context.ReceiptMessage,
            ClientContext = new Dictionary<string, object?>(context.ClientContext),
        };
    }
}
