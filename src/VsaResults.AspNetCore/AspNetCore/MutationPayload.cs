using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using VsaResults.Features.Features;

namespace VsaResults.AspNetCore.AspNetCore;

/// <summary>
/// The API response envelope for mutation endpoints.
/// Every mutation returns this envelope, providing a structured receipt of what was attempted and what happened.
/// </summary>
/// <typeparam name="T">The type of the mutation result data.</typeparam>
/// <remarks>
/// <para>
/// This implements the Mutation Payload pattern where the response is a receipt of the operation,
/// not just the resulting data. The client zone of the <see cref="FeatureContext{TRequest}"/>
/// is projected into <see cref="Context"/>, and the internal zone flows to the wide event separately.
/// </para>
/// </remarks>
public sealed record MutationPayload<T>
{
    /// <summary>
    /// Gets the kebab-case action name derived from the feature name (e.g., "soft-delete-tenant").
    /// </summary>
    [JsonPropertyName("action")]
    public required string Action { get; init; }

    /// <summary>
    /// Gets the outcome status: "success" or "failed".
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>
    /// Gets the human-readable message describing the outcome.
    /// When a feature provides a custom message via <see cref="FeatureContext{TRequest}.SetReceiptMessage"/>,
    /// that message is used. Otherwise, a convention-derived message is generated from the action name.
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>
    /// Gets the UTC timestamp of when the operation completed.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets the client-safe context from the pipeline (e.g., tenant_id, previous_status).
    /// Only present when pipeline stages have written client context via
    /// <see cref="FeatureContext{TRequest}.AddClientContext(string, object?)"/>.
    /// </summary>
    [JsonPropertyName("context")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, object?>? Context { get; init; }

    /// <summary>
    /// Gets the mutation result data. Null for operations that produce no data (e.g., former NoContent mutations).
    /// </summary>
    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public T? Data { get; init; }
}

/// <summary>
/// Provides factory methods and utilities for creating <see cref="MutationPayload{T}"/> instances.
/// </summary>
public static class MutationPayload
{
    private static readonly Regex PascalCaseSplitter = new(
        @"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])",
        RegexOptions.Compiled);

    /// <summary>
    /// Converts a PascalCase feature name to a kebab-case action name.
    /// </summary>
    /// <param name="featureName">The PascalCase feature name (e.g., "SoftDeleteTenant").</param>
    /// <returns>The kebab-case action name (e.g., "soft-delete-tenant").</returns>
    public static string ToKebabCase(string featureName) =>
        PascalCaseSplitter.Replace(featureName, "-").ToLowerInvariant();

    /// <summary>
    /// Generates a convention-derived success message from an action name.
    /// </summary>
    /// <param name="action">The kebab-case action name.</param>
    /// <returns>A human-readable success message.</returns>
    internal static string DefaultSuccessMessage(string action) =>
        $"{action} completed successfully.";

    /// <summary>
    /// Extracts the <see cref="MutationReceipt"/> from a <see cref="VsaResult.VsaResult{TValue}"/>'s context,
    /// if one was attached by the pipeline executor.
    /// </summary>
    /// <param name="resultContext">The VsaResult context dictionary.</param>
    /// <returns>The receipt, or null if not present.</returns>
    internal static MutationReceipt? ExtractReceipt(IReadOnlyDictionary<string, object> resultContext) =>
        resultContext.TryGetValue(MutationReceipt.ContextKey, out var receiptObj) && receiptObj is MutationReceipt receipt
            ? receipt
            : null;
}
