using VsaResults.Errors;

namespace VsaResults.Features.Features;

/// <summary>
/// Fluent extension methods for building validation rules that aggregate errors.
/// </summary>
/// <remarks>
/// These extensions provide a fluent API for common validation patterns.
/// All methods return the <see cref="ValidationContext"/> to enable chaining.
/// </remarks>
public static class ValidationExtensions
{
    /// <summary>
    /// Creates a new validation context.
    /// </summary>
    /// <returns>A new validation context for accumulating errors.</returns>
    public static ValidationContext Validate() => new();

    /// <summary>
    /// Validates that a string is not null or whitespace.
    /// </summary>
    /// <param name="ctx">The validation context.</param>
    /// <param name="value">The string value to validate.</param>
    /// <param name="code">The error code if validation fails.</param>
    /// <param name="description">The error description if validation fails.</param>
    /// <returns>The validation context for chaining.</returns>
    public static ValidationContext RequiredString(
        this ValidationContext ctx, string? value, string code, string description)
        => ctx.AddErrorIf(string.IsNullOrWhiteSpace(value), Error.Validation(code, description));

    /// <summary>
    /// Validates that a value type is not default.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="ctx">The validation context.</param>
    /// <param name="value">The value to validate.</param>
    /// <param name="code">The error code if validation fails.</param>
    /// <param name="description">The error description if validation fails.</param>
    /// <returns>The validation context for chaining.</returns>
    public static ValidationContext RequiredValue<T>(
        this ValidationContext ctx, T value, string code, string description)
        where T : struct
        => ctx.AddErrorIf(EqualityComparer<T>.Default.Equals(value, default), Error.Validation(code, description));

    /// <summary>
    /// Validates that a condition is true.
    /// </summary>
    /// <param name="ctx">The validation context.</param>
    /// <param name="isValid">The condition that must be true.</param>
    /// <param name="code">The error code if validation fails.</param>
    /// <param name="description">The error description if validation fails.</param>
    /// <returns>The validation context for chaining.</returns>
    public static ValidationContext Must(
        this ValidationContext ctx, bool isValid, string code, string description)
        => ctx.AddErrorIf(!isValid, Error.Validation(code, description));

    /// <summary>
    /// Validates that a collection is not null or empty.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="ctx">The validation context.</param>
    /// <param name="collection">The collection to validate.</param>
    /// <param name="code">The error code if validation fails.</param>
    /// <param name="description">The error description if validation fails.</param>
    /// <returns>The validation context for chaining.</returns>
    public static ValidationContext RequiredCollection<T>(
        this ValidationContext ctx, IEnumerable<T>? collection, string code, string description)
        => ctx.AddErrorIf(collection is null || !collection.Any(), Error.Validation(code, description));

    /// <summary>
    /// Validates that a nullable value has a value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="ctx">The validation context.</param>
    /// <param name="value">The nullable value to validate.</param>
    /// <param name="code">The error code if validation fails.</param>
    /// <param name="description">The error description if validation fails.</param>
    /// <returns>The validation context for chaining.</returns>
    public static ValidationContext RequiredNullable<T>(
        this ValidationContext ctx, T? value, string code, string description)
        where T : struct
        => ctx.AddErrorIf(!value.HasValue, Error.Validation(code, description));

    /// <summary>
    /// Validates that a string matches a maximum length.
    /// </summary>
    /// <param name="ctx">The validation context.</param>
    /// <param name="value">The string value to validate.</param>
    /// <param name="maxLength">The maximum allowed length.</param>
    /// <param name="code">The error code if validation fails.</param>
    /// <param name="description">The error description if validation fails.</param>
    /// <returns>The validation context for chaining.</returns>
    public static ValidationContext MaxLength(
        this ValidationContext ctx, string? value, int maxLength, string code, string description)
        => ctx.AddErrorIf(value is not null && value.Length > maxLength, Error.Validation(code, description));

    /// <summary>
    /// Validates that a string matches a minimum length.
    /// </summary>
    /// <param name="ctx">The validation context.</param>
    /// <param name="value">The string value to validate.</param>
    /// <param name="minLength">The minimum required length.</param>
    /// <param name="code">The error code if validation fails.</param>
    /// <param name="description">The error description if validation fails.</param>
    /// <returns>The validation context for chaining.</returns>
    public static ValidationContext MinLength(
        this ValidationContext ctx, string? value, int minLength, string code, string description)
        => ctx.AddErrorIf(value is null || value.Length < minLength, Error.Validation(code, description));

    /// <summary>
    /// Validates that a numeric value is within a range.
    /// </summary>
    /// <typeparam name="T">The numeric type.</typeparam>
    /// <param name="ctx">The validation context.</param>
    /// <param name="value">The value to validate.</param>
    /// <param name="min">The minimum allowed value (inclusive).</param>
    /// <param name="max">The maximum allowed value (inclusive).</param>
    /// <param name="code">The error code if validation fails.</param>
    /// <param name="description">The error description if validation fails.</param>
    /// <returns>The validation context for chaining.</returns>
    public static ValidationContext InRange<T>(
        this ValidationContext ctx, T value, T min, T max, string code, string description)
        where T : IComparable<T>
        => ctx.AddErrorIf(value.CompareTo(min) < 0 || value.CompareTo(max) > 0, Error.Validation(code, description));

    /// <summary>
    /// Validates that an object reference is not null.
    /// </summary>
    /// <typeparam name="T">The reference type.</typeparam>
    /// <param name="ctx">The validation context.</param>
    /// <param name="value">The object to validate.</param>
    /// <param name="code">The error code if validation fails.</param>
    /// <param name="description">The error description if validation fails.</param>
    /// <returns>The validation context for chaining.</returns>
    public static ValidationContext RequiredObject<T>(
        this ValidationContext ctx, T? value, string code, string description)
        where T : class
        => ctx.AddErrorIf(value is null, Error.Validation(code, description));

    /// <summary>
    /// Validates that a string matches a valid email format using <see cref="System.Net.Mail.MailAddress.TryCreate(string?, out System.Net.Mail.MailAddress?)"/>.
    /// Skips validation when the value is null or whitespace (use <see cref="RequiredString"/> to enforce presence).
    /// </summary>
    /// <param name="ctx">The validation context.</param>
    /// <param name="email">The email string to validate.</param>
    /// <param name="code">The error code if validation fails.</param>
    /// <param name="description">The error description if validation fails.</param>
    /// <returns>The validation context for chaining.</returns>
    public static ValidationContext ValidEmail(
        this ValidationContext ctx, string? email, string code, string description)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return ctx;
        }

        var isValid = System.Net.Mail.MailAddress.TryCreate(email, out _);
        return ctx.AddErrorIf(!isValid, Error.Validation(code, description));
    }

    /// <summary>
    /// Conditionally validates if a predicate is true.
    /// </summary>
    /// <param name="ctx">The validation context.</param>
    /// <param name="condition">The condition that determines if validation runs.</param>
    /// <param name="validate">The validation action to run if condition is true.</param>
    /// <returns>The validation context for chaining.</returns>
    public static ValidationContext When(
        this ValidationContext ctx, bool condition, Func<ValidationContext, ValidationContext> validate)
        => condition ? validate(ctx) : ctx;
}
