namespace VsaResults.Observability.Masking;

/// <summary>
/// Defines the contract for PII (Personally Identifiable Information) masking operations.
/// Implementations provide deterministic, one-way masking of sensitive data.
/// </summary>
public interface IPiiMasker
{
    /// <summary>
    /// Masks a nullable string value, returning null or empty strings unchanged.
    /// </summary>
    /// <param name="input">The string to mask, or null.</param>
    /// <param name="key">Optional context key that may influence masking behavior (e.g., "password" triggers full redaction).</param>
    /// <returns>The masked string, or the original input if null/empty.</returns>
    string? MaskNullableString(string? input, string? key = null);

    /// <summary>
    /// Masks a string value, detecting and replacing PII patterns with deterministic hashes.
    /// </summary>
    /// <param name="input">The string to mask.</param>
    /// <param name="key">Optional context key that may influence masking behavior.</param>
    /// <returns>The masked string with PII replaced by prefixed hashes (e.g., "EM_abc123def456").</returns>
    string MaskString(string input, string? key = null);

    /// <summary>
    /// Masks a value based on its key and type.
    /// </summary>
    /// <param name="key">The context key for this value.</param>
    /// <param name="value">The value to potentially mask.</param>
    /// <returns>The masked value if applicable, or the original value if not maskable.</returns>
    object? MaskValue(string key, object? value);
}
