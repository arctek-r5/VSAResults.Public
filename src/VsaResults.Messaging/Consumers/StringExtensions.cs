using System.Text;

namespace VsaResults.Messaging.Consumers;

/// <summary>
/// String extension methods for naming conventions.
/// </summary>
internal static class StringExtensions
{
    /// <summary>
    /// Converts a PascalCase string to kebab-case.
    /// </summary>
    /// <param name="value">The string to convert.</param>
    /// <returns>The kebab-case string.</returns>
    /// <example>
    /// "OrderCreatedConsumer" -> "order-created-consumer"
    /// "XMLParser" -> "xml-parser"
    /// </example>
    public static string ToKebabCase(this string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var builder = new StringBuilder();
        var previousWasUpper = false;
        var previousWasDigit = false;

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];

            if (char.IsUpper(c))
            {
                // Add hyphen before uppercase letter if:
                // - Not at the start
                // - Previous was not uppercase (handles "OrderID" -> "order-id")
                // - Or next is lowercase (handles "XMLParser" -> "xml-parser")
                if (i > 0 && (!previousWasUpper || (i + 1 < value.Length && char.IsLower(value[i + 1]))))
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(c));
                previousWasUpper = true;
                previousWasDigit = false;
            }
            else if (char.IsDigit(c))
            {
                if (i > 0 && !previousWasDigit)
                {
                    builder.Append('-');
                }

                builder.Append(c);
                previousWasUpper = false;
                previousWasDigit = true;
            }
            else
            {
                builder.Append(c);
                previousWasUpper = false;
                previousWasDigit = false;
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Converts a PascalCase string to snake_case.
    /// </summary>
    /// <param name="value">The string to convert.</param>
    /// <returns>The snake_case string.</returns>
    /// <example>
    /// "OrderCreatedConsumer" -> "order_created_consumer"
    /// </example>
    public static string ToSnakeCase(this string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var builder = new StringBuilder();
        var previousWasUpper = false;

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];

            if (char.IsUpper(c))
            {
                if (i > 0 && (!previousWasUpper || (i + 1 < value.Length && char.IsLower(value[i + 1]))))
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(c));
                previousWasUpper = true;
            }
            else
            {
                builder.Append(c);
                previousWasUpper = false;
            }
        }

        return builder.ToString();
    }
}
