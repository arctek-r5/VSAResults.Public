using System.Text.RegularExpressions;

namespace VsaResults.Observability.Masking;

/// <summary>
/// Defines a custom pattern for detecting and masking PII values.
/// </summary>
public sealed class PiiPattern
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PiiPattern"/> class.
    /// </summary>
    /// <param name="name">A descriptive name for this pattern.</param>
    /// <param name="pattern">The regex pattern to match PII values.</param>
    /// <param name="prefix">The prefix for masked values (e.g., "SSN_").</param>
    public PiiPattern(string name, string pattern, string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        Name = name;
        Pattern = new Regex(pattern, RegexOptions.Compiled);
        Prefix = prefix;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PiiPattern"/> class with a pre-compiled regex.
    /// </summary>
    /// <param name="name">A descriptive name for this pattern.</param>
    /// <param name="pattern">The compiled regex pattern to match PII values.</param>
    /// <param name="prefix">The prefix for masked values (e.g., "SSN_").</param>
    public PiiPattern(string name, Regex pattern, string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        Name = name;
        Pattern = pattern;
        Prefix = prefix;
    }

    /// <summary>
    /// Gets the name of this pattern (used for logging and diagnostics).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the compiled regex pattern for detecting this PII type.
    /// </summary>
    public Regex Pattern { get; }

    /// <summary>
    /// Gets the prefix added to masked values (e.g., "SSN_" for social security numbers).
    /// </summary>
    public string Prefix { get; }
}
