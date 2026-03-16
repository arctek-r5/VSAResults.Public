namespace VsaResults.Observability.Masking;

/// <summary>
/// A no-op implementation of <see cref="IPiiMasker"/> that returns values unchanged.
/// Useful for testing scenarios or when PII masking should be disabled.
/// </summary>
public sealed class NullPiiMasker : IPiiMasker
{
    /// <summary>
    /// Gets a shared instance of the null masker.
    /// </summary>
    public static NullPiiMasker Instance { get; } = new();

    /// <inheritdoc />
    public string? MaskNullableString(string? input, string? key = null) => input;

    /// <inheritdoc />
    public string MaskString(string input, string? key = null) => input;

    /// <inheritdoc />
    public object? MaskValue(string key, object? value) => value;
}
