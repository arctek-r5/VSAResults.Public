using System.Collections.Concurrent;
using System.Reflection;

namespace VsaResults.Features.WideEvents;

internal static class WideEventContextExtractor
{
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<(PropertyInfo Property, WideEventPropertyAttribute Attribute)>> PropertyCache = new();

    public static IEnumerable<KeyValuePair<string, object?>> Extract(object? context)
    {
        if (context is null)
        {
            yield break;
        }

        var contextType = context.GetType();

        foreach (var (property, attribute) in GetWideEventProperties(contextType))
        {
            var key = attribute.Key ?? ToSnakeCase(property.Name);
            yield return new KeyValuePair<string, object?>(key, property.GetValue(context));
        }

        if (context is not IWideEventContext contextProvider)
        {
            yield break;
        }

        foreach (var pair in contextProvider.GetWideEventContext())
        {
            yield return pair;
        }
    }

    private static IReadOnlyList<(PropertyInfo Property, WideEventPropertyAttribute Attribute)> GetWideEventProperties(Type type)
        => PropertyCache.GetOrAdd(type, static currentType =>
        {
            var result = new List<(PropertyInfo Property, WideEventPropertyAttribute Attribute)>();

            foreach (var property in currentType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attribute = property.GetCustomAttribute<WideEventPropertyAttribute>();
                if (attribute is not null)
                {
                    result.Add((property, attribute));
                }
            }

            return result;
        });

    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var result = new System.Text.StringBuilder();
        result.Append(char.ToLowerInvariant(input[0]));

        for (var i = 1; i < input.Length; i++)
        {
            var current = input[i];
            if (char.IsUpper(current))
            {
                result.Append('_');
                result.Append(char.ToLowerInvariant(current));
            }
            else
            {
                result.Append(current);
            }
        }

        return result.ToString();
    }
}
