namespace VsaResults.Observability.Masking;

/// <summary>
/// Configuration options for PII masking behavior.
/// </summary>
public sealed class PiiMaskerOptions
{
    /// <summary>
    /// Gets or sets the salt used for deterministic hashing.
    /// When set, the same input always produces the same hash, enabling correlation across logs.
    /// Defaults to reading from the "PII_MASK_SALT" environment variable.
    /// </summary>
    public string? Salt { get; set; }

    /// <summary>
    /// Gets or sets the length of the hash suffix in masked values.
    /// Default is 12 characters (e.g., "EM_abc123def456").
    /// </summary>
    public int HashLength { get; set; } = 12;

    /// <summary>
    /// Gets or sets a value indicating whether to mask email addresses.
    /// Default is true.
    /// </summary>
    public bool MaskEmails { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to mask phone numbers.
    /// Default is true.
    /// </summary>
    public bool MaskPhones { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to mask IP addresses.
    /// Default is true.
    /// </summary>
    public bool MaskIps { get; set; } = true;

    /// <summary>
    /// Gets or sets key patterns that trigger full redaction (password-style masking).
    /// When a context key contains any of these tokens, the value is fully masked.
    /// Default includes: password, passwd, secret, token, apikey, api_key, authorization, auth.
    /// </summary>
    public HashSet<string> SecretKeyPatterns { get; set; } =
    [
        "password",
        "passwd",
        "secret",
        "token",
        "apikey",
        "api_key",
        "authorization",
        "auth"
    ];

    /// <summary>
    /// Gets or sets key patterns that indicate name fields requiring masking.
    /// When a context key contains any of these tokens, the value is masked as a name.
    /// Default includes: name, first_name, last_name, full_name, display_name, user_name, given_name, family_name.
    /// </summary>
    public HashSet<string> NameKeyPatterns { get; set; } =
    [
        "name",
        "first_name",
        "last_name",
        "full_name",
        "display_name",
        "user_name",
        "given_name",
        "family_name"
    ];

    /// <summary>
    /// Gets or sets additional custom patterns to detect and mask.
    /// </summary>
    public List<PiiPattern> CustomPatterns { get; set; } = [];

    /// <summary>
    /// Gets or sets the mask displayed for fully redacted secrets.
    /// Default is "PW_******".
    /// </summary>
    public string PasswordMask { get; set; } = "PW_******";
}
