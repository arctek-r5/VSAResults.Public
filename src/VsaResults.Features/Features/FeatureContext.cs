namespace VsaResults.Features.Features;

/// <summary>
/// Carries the request through the feature pipeline along with loaded entities and wide event context.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <remarks>
/// <para>
/// <strong>Context System Overview:</strong> VsaResults has two context mechanisms:
/// </para>
/// <list type="bullet">
///   <item>
///     <term><c>FeatureContext&lt;TRequest&gt;.AddContext()</c> (this class)</term>
///     <description>
///       Mutable context scoped to a Feature Pipeline execution.
///       Use within pipeline stages (<c>IFeatureValidator</c>, <c>IFeatureRequirements</c>,
///       <c>IFeatureMutator</c>, <c>IFeatureSideEffects</c>).
///       Automatically merged into the wide event at the end of execution.
///     </description>
///   </item>
///   <item>
///     <term><c>VsaResult&lt;T&gt;.WithContext()</c></term>
///     <description>
///       Immutable context that flows through fluent chains (<c>Then</c>, <c>Else</c>, <c>Match</c>, etc.).
///       Use for ad-hoc operations or when not using the Feature Pipeline.
///     </description>
///   </item>
/// </list>
/// <para>
/// <strong>Usage:</strong> When implementing feature pipeline stages, use <c>context.AddContext()</c>
/// to add business-relevant data that should appear in the wide event log.
/// </para>
/// </remarks>
public sealed class FeatureContext<TRequest>
{
    private readonly Dictionary<EntityStorageKey, object> _entities = [];
    private readonly Dictionary<string, object?> _wideEventContext = [];
    private readonly Dictionary<string, object?> _clientContext = [];
    private string? _receiptMessage;

    /// <summary>
    /// Gets the validated request being processed.
    /// </summary>
    public required TRequest Request { get; init; }

    /// <summary>
    /// Gets the entities loaded during requirements enforcement.
    /// Use SetEntity/GetEntity for type-safe access.
    /// Keys are <see cref="EntityStorageKey"/> composed of (Name, Type) for uniqueness.
    /// </summary>
    public IReadOnlyDictionary<string, object> Entities =>
        _entities.ToDictionary(kvp => kvp.Key.Name, kvp => kvp.Value);

    /// <summary>
    /// Gets the context to be included in the wide event log (internal zone).
    /// Add business-relevant data here during feature execution.
    /// </summary>
    public IReadOnlyDictionary<string, object?> WideEventContext => _wideEventContext;

    /// <summary>
    /// Gets the client-safe context projected into the API response envelope (client zone).
    /// Use <see cref="AddClientContext(string, object?)"/> to add entries from pipeline stages.
    /// </summary>
    public IReadOnlyDictionary<string, object?> ClientContext => _clientContext;

    /// <summary>
    /// Gets the human-readable message describing the mutation outcome.
    /// Projected into the response envelope's <c>message</c> field.
    /// </summary>
    public string? ReceiptMessage => _receiptMessage;

    /// <summary>
    /// Gets a previously stored entity by key.
    /// Uses <c>typeof(T)</c> combined with the string key for lookup — both the name
    /// and the type must match what was passed to <see cref="SetEntity{T}(string, T)"/>.
    /// </summary>
    /// <typeparam name="T">The type of the entity.</typeparam>
    /// <param name="key">The storage key.</param>
    /// <returns>The entity cast to the specified type.</returns>
    public T GetEntity<T>(string key) where T : notnull
    {
        var storageKey = new EntityStorageKey(key, typeof(T));
        if (_entities.TryGetValue(storageKey, out var value))
        {
            return (T)value;
        }

        throw new KeyNotFoundException($"No entity was stored for key '{key}' and type '{typeof(T).Name}'.");
    }

    /// <summary>
    /// Gets a previously stored entity by strongly-typed key.
    /// </summary>
    /// <typeparam name="T">The type of the entity (inferred from key).</typeparam>
    /// <param name="key">The strongly-typed storage key.</param>
    /// <returns>The entity.</returns>
    public T GetEntity<T>(EntityKey<T> key) where T : notnull
        => GetEntity<T>(key.Name);

    /// <summary>
    /// Tries to get a previously stored entity by key.
    /// </summary>
    /// <typeparam name="T">The type of the entity.</typeparam>
    /// <param name="key">The storage key.</param>
    /// <param name="value">The entity if found.</param>
    /// <returns>True if the entity was found.</returns>
    public bool TryGetEntity<T>(string key, out T? value) where T : notnull
    {
        var storageKey = new EntityStorageKey(key, typeof(T));
        if (_entities.TryGetValue(storageKey, out var obj))
        {
            value = (T)obj;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Tries to get a previously stored entity by strongly-typed key.
    /// </summary>
    /// <typeparam name="T">The type of the entity (inferred from key).</typeparam>
    /// <param name="key">The strongly-typed storage key.</param>
    /// <param name="value">The entity if found.</param>
    /// <returns>True if the entity was found.</returns>
    public bool TryGetEntity<T>(EntityKey<T> key, out T? value) where T : notnull
        => TryGetEntity(key.Name, out value);

    /// <summary>
    /// Stores an entity for later retrieval in the pipeline.
    /// The entity is keyed by both name and type — this means <c>SetEntity("order", orderA)</c>
    /// and <c>SetEntity("order", orderB)</c> where A and B are different types will store
    /// two separate entries, not overwrite each other.
    /// </summary>
    /// <typeparam name="T">The type of the entity.</typeparam>
    /// <param name="key">The storage key.</param>
    /// <param name="value">The entity to store.</param>
    public void SetEntity<T>(string key, T value)
        where T : notnull
        => _entities[new EntityStorageKey(key, typeof(T))] = value;

    /// <summary>
    /// Stores an entity for later retrieval in the pipeline using a strongly-typed key.
    /// </summary>
    /// <typeparam name="T">The type of the entity (inferred from key).</typeparam>
    /// <param name="key">The strongly-typed storage key.</param>
    /// <param name="value">The entity to store.</param>
    public void SetEntity<T>(EntityKey<T> key, T value)
        where T : notnull
        => SetEntity(key.Name, value);

    /// <summary>
    /// Adds context to be included in the wide event log.
    /// Use snake_case keys for consistency with structured logging.
    /// </summary>
    /// <param name="key">The context key (e.g., "user_id", "order_count").</param>
    /// <param name="value">The context value.</param>
    /// <returns>This context for fluent chaining.</returns>
    public FeatureContext<TRequest> AddContext(string key, object? value)
    {
        _wideEventContext[key] = value;
        return this;
    }

    /// <summary>
    /// Adds context entries extracted from a strongly typed context object.
    /// </summary>
    public FeatureContext<TRequest> AddContextObject(object? context)
    {
        foreach (var (key, value) in WideEvents.WideEventContextExtractor.Extract(context))
        {
            _wideEventContext[key] = value;
        }

        return this;
    }

    /// <summary>
    /// Adds multiple context entries to be included in the wide event log.
    /// </summary>
    /// <param name="pairs">Key-value pairs to add.</param>
    /// <returns>This context for fluent chaining.</returns>
    public FeatureContext<TRequest> AddContext(params (string Key, object? Value)[] pairs)
    {
        foreach (var (key, value) in pairs)
        {
            _wideEventContext[key] = value;
        }

        return this;
    }

    /// <summary>
    /// Sets the human-readable message describing the mutation outcome.
    /// This is projected into the API response envelope's <c>message</c> field.
    /// </summary>
    /// <param name="message">The outcome message (e.g., "Tenant was soft-deleted.").</param>
    /// <returns>This context for fluent chaining.</returns>
    public FeatureContext<TRequest> SetReceiptMessage(string message)
    {
        _receiptMessage = message;
        return this;
    }

    /// <summary>
    /// Adds a client-safe context entry projected into the API response envelope.
    /// Only include information that is safe and useful for the API consumer.
    /// Use snake_case keys for consistency.
    /// </summary>
    /// <param name="key">The context key (e.g., "tenant_id", "previous_status").</param>
    /// <param name="value">The context value.</param>
    /// <returns>This context for fluent chaining.</returns>
    public FeatureContext<TRequest> AddClientContext(string key, object? value)
    {
        _clientContext[key] = value;
        return this;
    }

    /// <summary>
    /// Adds multiple client-safe context entries projected into the API response envelope.
    /// </summary>
    /// <param name="pairs">Key-value pairs to add.</param>
    /// <returns>This context for fluent chaining.</returns>
    public FeatureContext<TRequest> AddClientContext(params (string Key, object? Value)[] pairs)
    {
        foreach (var (key, value) in pairs)
        {
            _clientContext[key] = value;
        }

        return this;
    }

    internal readonly record struct EntityStorageKey(string Name, Type EntityType);
}
