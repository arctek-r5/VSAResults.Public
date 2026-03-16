namespace VsaResults.Errors;

/// <summary>
/// Represents an error.
/// </summary>
public readonly record struct Error
{
    private Error(string code, string description, ErrorType type, IReadOnlyDictionary<string, object>? metadata)
    {
        Code = code;
        Description = description;
        Type = type;
        NumericType = (int)type;
        Metadata = metadata;
    }

    /// <summary>
    /// Gets the unique error code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the error description.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the error type.
    /// </summary>
    public ErrorType Type { get; }

    /// <summary>
    /// Gets the numeric value of the type.
    /// </summary>
    public int NumericType { get; }

    /// <summary>
    /// Gets the metadata. This is a read-only view of the metadata dictionary.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Important:</strong> Metadata values should be immutable (strings, numbers, booleans, etc.).
    /// If you store mutable objects (lists, dictionaries), modifying them after Error creation will
    /// change the Error's hash code, which can cause issues when using Errors in collections like
    /// HashSet or as Dictionary keys.
    /// </para>
    /// <para>
    /// For safe usage, prefer immutable types like <c>string</c>, <c>int</c>, <c>bool</c>,
    /// <c>DateTimeOffset</c>, or use <c>ImmutableArray&lt;T&gt;</c> / <c>ImmutableDictionary&lt;K,V&gt;</c>
    /// for collections.
    /// </para>
    /// </remarks>
    public IReadOnlyDictionary<string, object>? Metadata { get; }

    /// <summary>
    /// Creates an <see cref="Error"/> of type <see cref="ErrorType.Failure"/> from a code and description.
    /// </summary>
    /// <param name="code">The unique error code.</param>
    /// <param name="description">The error description.</param>
    /// <param name="metadata">A dictionary which provides optional space for information.
    /// Values should be immutable types to ensure hash code stability (see <see cref="Metadata"/> remarks).</param>
    public static Error Failure(
        string code = "General.Failure",
        string description = "A failure has occurred.",
        IReadOnlyDictionary<string, object>? metadata = null) =>
            new(code, description, ErrorType.Failure, metadata);

    /// <summary>
    /// Creates an <see cref="Error"/> of type <see cref="ErrorType.Unexpected"/> from a code and description.
    /// </summary>
    /// <param name="code">The unique error code.</param>
    /// <param name="description">The error description.</param>
    /// <param name="metadata">A dictionary which provides optional space for information.</param>
    public static Error Unexpected(
        string code = "General.Unexpected",
        string description = "An unexpected error has occurred.",
        IReadOnlyDictionary<string, object>? metadata = null) =>
            new(code, description, ErrorType.Unexpected, metadata);

    /// <summary>
    /// Creates an <see cref="Error"/> of type <see cref="ErrorType.Validation"/> from a code and description.
    /// </summary>
    /// <param name="code">The unique error code.</param>
    /// <param name="description">The error description.</param>
    /// <param name="metadata">A dictionary which provides optional space for information.</param>
    public static Error Validation(
        string code = "General.Validation",
        string description = "A validation error has occurred.",
        IReadOnlyDictionary<string, object>? metadata = null) =>
            new(code, description, ErrorType.Validation, metadata);

    /// <summary>
    /// Creates an <see cref="Error"/> of type <see cref="ErrorType.Conflict"/> from a code and description.
    /// </summary>
    /// <param name="code">The unique error code.</param>
    /// <param name="description">The error description.</param>
    /// <param name="metadata">A dictionary which provides optional space for information.</param>
    public static Error Conflict(
        string code = "General.Conflict",
        string description = "A conflict error has occurred.",
        IReadOnlyDictionary<string, object>? metadata = null) =>
            new(code, description, ErrorType.Conflict, metadata);

    /// <summary>
    /// Creates an <see cref="Error"/> of type <see cref="ErrorType.NotFound"/> from a code and description.
    /// </summary>
    /// <param name="code">The unique error code.</param>
    /// <param name="description">The error description.</param>
    /// <param name="metadata">A dictionary which provides optional space for information.</param>
    public static Error NotFound(
        string code = "General.NotFound",
        string description = "A 'Not Found' error has occurred.",
        IReadOnlyDictionary<string, object>? metadata = null) =>
            new(code, description, ErrorType.NotFound, metadata);

    /// <summary>
    /// Creates an <see cref="Error"/> of type <see cref="ErrorType.Unauthorized"/> from a code and description.
    /// </summary>
    /// <param name="code">The unique error code.</param>
    /// <param name="description">The error description.</param>
    /// <param name="metadata">A dictionary which provides optional space for information.</param>
    public static Error Unauthorized(
        string code = "General.Unauthorized",
        string description = "An 'Unauthorized' error has occurred.",
        IReadOnlyDictionary<string, object>? metadata = null) =>
            new(code, description, ErrorType.Unauthorized, metadata);

    /// <summary>
    /// Creates an <see cref="Error"/> of type <see cref="ErrorType.Forbidden"/> from a code and description.
    /// </summary>
    /// <param name="code">The unique error code.</param>
    /// <param name="description">The error description.</param>
    /// <param name="metadata">A dictionary which provides optional space for information.</param>
    public static Error Forbidden(
        string code = "General.Forbidden",
        string description = "A 'Forbidden' error has occurred.",
        IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, description, ErrorType.Forbidden, metadata);

    /// <summary>
    /// Creates an <see cref="Error"/> of type <see cref="ErrorType.BadRequest"/> from a code and description.
    /// </summary>
    /// <param name="code">The unique error code.</param>
    /// <param name="description">The error description.</param>
    /// <param name="metadata">A dictionary which provides optional space for information.</param>
    public static Error BadRequest(
        string code = "General.BadRequest",
        string description = "A 'Bad Request' error has occurred.",
        IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, description, ErrorType.BadRequest, metadata);

    /// <summary>
    /// Creates an <see cref="Error"/> of type <see cref="ErrorType.Timeout"/> from a code and description.
    /// </summary>
    /// <param name="code">The unique error code.</param>
    /// <param name="description">The error description.</param>
    /// <param name="metadata">A dictionary which provides optional space for information.</param>
    public static Error Timeout(
        string code = "General.Timeout",
        string description = "A 'Timeout' error has occurred.",
        IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, description, ErrorType.Timeout, metadata);

    /// <summary>
    /// Creates an <see cref="Error"/> of type <see cref="ErrorType.Gone"/> from a code and description.
    /// </summary>
    /// <param name="code">The unique error code.</param>
    /// <param name="description">The error description.</param>
    /// <param name="metadata">A dictionary which provides optional space for information.</param>
    public static Error Gone(
        string code = "General.Gone",
        string description = "A 'Gone' error has occurred.",
        IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, description, ErrorType.Gone, metadata);

    /// <summary>
    /// Creates an <see cref="Error"/> of type <see cref="ErrorType.Locked"/> from a code and description.
    /// </summary>
    /// <param name="code">The unique error code.</param>
    /// <param name="description">The error description.</param>
    /// <param name="metadata">A dictionary which provides optional space for information.</param>
    public static Error Locked(
        string code = "General.Locked",
        string description = "A 'Locked' error has occurred.",
        IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, description, ErrorType.Locked, metadata);

    /// <summary>
    /// Creates an <see cref="Error"/> of type <see cref="ErrorType.TooManyRequests"/> from a code and description.
    /// </summary>
    /// <param name="code">The unique error code.</param>
    /// <param name="description">The error description.</param>
    /// <param name="metadata">A dictionary which provides optional space for information.</param>
    public static Error TooManyRequests(
        string code = "General.TooManyRequests",
        string description = "A 'Too Many Requests' error has occurred.",
        IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, description, ErrorType.TooManyRequests, metadata);

    /// <summary>
    /// Creates an <see cref="Error"/> of type <see cref="ErrorType.Unavailable"/> from a code and description.
    /// </summary>
    /// <param name="code">The unique error code.</param>
    /// <param name="description">The error description.</param>
    /// <param name="metadata">A dictionary which provides optional space for information.</param>
    public static Error Unavailable(
        string code = "General.Unavailable",
        string description = "A 'Service Unavailable' error has occurred.",
        IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, description, ErrorType.Unavailable, metadata);

    /// <summary>
    /// Creates an <see cref="Error"/> with the given numeric <paramref name="type"/>,
    /// <paramref name="code"/>, and <paramref name="description"/>.
    /// </summary>
    /// <param name="type">An integer value which represents the type of error that occurred.</param>
    /// <param name="code">The unique error code.</param>
    /// <param name="description">The error description.</param>
    /// <param name="metadata">A dictionary which provides optional space for information.</param>
    public static Error Custom(
        int type,
        string code,
        string description,
        IReadOnlyDictionary<string, object>? metadata = null) =>
            new(code, description, (ErrorType)type, metadata);

    /// <summary>
    /// Returns a string representation of this error for debugging purposes.
    /// </summary>
    public override string ToString()
    {
        var description = Description.Length > 50
            ? $"{Description[..47]}..."
            : Description;

        return $"Error {{ Code = {Code}, Type = {Type}, Description = {description} }}";
    }

    public bool Equals(Error other)
    {
        if (Type != other.Type ||
            NumericType != other.NumericType ||
            Code != other.Code ||
            Description != other.Description)
        {
            return false;
        }

        if (Metadata is null)
        {
            return other.Metadata is null;
        }

        return other.Metadata is not null && CompareMetadata(Metadata, other.Metadata);
    }

    public override int GetHashCode() =>
        Metadata is null ? HashCode.Combine(Code, Description, Type, NumericType) : ComposeHashCode();

    private int ComposeHashCode()
    {
#pragma warning disable SA1129 // HashCode needs to be instantiated this way
        var hashCode = new HashCode();
#pragma warning restore SA1129

        hashCode.Add(Code);
        hashCode.Add(Description);
        hashCode.Add(Type);
        hashCode.Add(NumericType);

        foreach (var keyValuePair in Metadata!)
        {
            hashCode.Add(keyValuePair.Key);
            hashCode.Add(keyValuePair.Value);
        }

        return hashCode.ToHashCode();
    }

    private static bool CompareMetadata(IReadOnlyDictionary<string, object> metadata, IReadOnlyDictionary<string, object> otherMetadata)
    {
        if (ReferenceEquals(metadata, otherMetadata))
        {
            return true;
        }

        if (metadata.Count != otherMetadata.Count)
        {
            return false;
        }

        foreach (var keyValuePair in metadata)
        {
            if (!otherMetadata.TryGetValue(keyValuePair.Key, out var otherValue) ||
                !keyValuePair.Value.Equals(otherValue))
            {
                return false;
            }
        }

        return true;
    }
}
