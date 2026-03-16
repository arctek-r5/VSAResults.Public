using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace VsaResults.Observability.Masking;

/// <summary>
/// Default implementation of <see cref="IPiiMasker"/> that masks PII using deterministic hashing.
/// Detects emails, phone numbers, IP addresses, and configurable patterns.
/// </summary>
public sealed class DefaultPiiMasker : IPiiMasker
{
    /// <summary>
    /// The environment variable name for the PII masking salt.
    /// </summary>
    public const string MaskSaltEnvVar = "PII_MASK_SALT";

    private const int PhoneDigitsMin = 7;
    private const int PhoneDigitsMax = 15;

    private static readonly Regex EmailRegex = new(
        @"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PhoneRegex = new(
        $@"(?<!\d)(?:\+?\d[\d\s\-\(\)]{{{PhoneDigitsMin - 1},{PhoneDigitsMax - 1}}}\d)(?!\d)",
        RegexOptions.Compiled);

    private static readonly Regex IpRegex = new(
        @"\b(?:(?:25[0-5]|2[0-4]\d|[01]?\d\d?)\.){3}(?:25[0-5]|2[0-4]\d|[01]?\d\d?)\b",
        RegexOptions.Compiled);

    private readonly PiiMaskerOptions _options;
    private readonly string _salt;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultPiiMasker"/> class with default options.
    /// </summary>
    public DefaultPiiMasker()
        : this(new PiiMaskerOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultPiiMasker"/> class with options from DI.
    /// </summary>
    /// <param name="options">The masking configuration options wrapped in IOptions.</param>
    public DefaultPiiMasker(IOptions<PiiMaskerOptions> options)
        : this(options.Value)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultPiiMasker"/> class with the specified options.
    /// </summary>
    /// <param name="options">The masking configuration options.</param>
    internal DefaultPiiMasker(PiiMaskerOptions options)
    {
        _options = options;
        _salt = options.Salt ?? Environment.GetEnvironmentVariable(MaskSaltEnvVar) ?? string.Empty;
    }

    /// <inheritdoc />
    public string? MaskNullableString(string? input, string? key = null)
        => string.IsNullOrEmpty(input) ? input : MaskString(input, key);

    /// <inheritdoc />
    public string MaskString(string input, string? key = null)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        // Key-based masking takes precedence
        if (!string.IsNullOrEmpty(key))
        {
            if (IsSecretKey(key))
            {
                return _options.PasswordMask;
            }

            if (IsNameKey(key))
            {
                return $"NM_{Hash(input)}";
            }
        }

        // Pattern-based masking
        var masked = input;

        if (_options.MaskEmails)
        {
            masked = ReplaceMatches(EmailRegex, masked, "EM_");
        }

        if (_options.MaskPhones)
        {
            masked = ReplaceMatches(PhoneRegex, masked, "PH_");
        }

        if (_options.MaskIps)
        {
            masked = ReplaceMatches(IpRegex, masked, "IP_");
        }

        // Apply custom patterns
        foreach (var pattern in _options.CustomPatterns)
        {
            masked = ReplaceMatches(pattern.Pattern, masked, pattern.Prefix);
        }

        return masked;
    }

    /// <inheritdoc />
    public object? MaskValue(string key, object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is string text)
        {
            return MaskString(text, key);
        }

        if (value is IEnumerable<string> list)
        {
            return list.Select(item => MaskString(item, key)).ToArray();
        }

        return value;
    }

    private bool IsSecretKey(string key)
        => _options.SecretKeyPatterns.Any(token => key.Contains(token, StringComparison.OrdinalIgnoreCase));

    private bool IsNameKey(string key)
        => _options.NameKeyPatterns.Any(token => key.Contains(token, StringComparison.OrdinalIgnoreCase));

    private string ReplaceMatches(Regex regex, string input, string prefix)
        => regex.Replace(input, match => $"{prefix}{Hash(match.Value)}");

    private string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{_salt}:{value}"));
        return Convert.ToHexString(bytes).ToLowerInvariant()[.._options.HashLength];
    }
}
