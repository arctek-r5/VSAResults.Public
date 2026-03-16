namespace VsaResults.Features.Features;

/// <summary>
/// A strongly-typed key for storing and retrieving entities in <see cref="FeatureContext{TRequest}"/>.
/// Encodes both the key name and entity type for safer entity lookups.
/// </summary>
/// <typeparam name="T">The type of entity this key references.</typeparam>
/// <example>
/// <code>
/// // Define keys as static fields (typically in the Requirements class):
/// private static readonly EntityKey&lt;Order&gt; OrderKey = new("order");
///
/// // Set in Requirements:
/// context.SetEntity(OrderKey, order);
///
/// // Get in Mutator (type is inferred):
/// var order = context.GetEntity(OrderKey);
/// </code>
/// </example>
public readonly record struct EntityKey<T>(string Name) where T : notnull
{
    /// <summary>
    /// Returns the key name.
    /// </summary>
    public override string ToString() => Name;
}
