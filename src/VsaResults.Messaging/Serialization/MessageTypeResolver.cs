using System.Collections.Concurrent;
using VsaResults.Messaging.ErrorOr;
using VsaResults.Messaging.Messages;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Serialization;

/// <summary>
/// Resolves message types from type identifiers and vice versa.
/// Uses URN format for type identification: urn:message:Namespace:TypeName
/// </summary>
public sealed class MessageTypeResolver
{
    private const string UrnPrefix = "urn:message:";

    private readonly ConcurrentDictionary<string, Type> _identifierToType = new();
    private readonly ConcurrentDictionary<Type, string[]> _typeToIdentifiers = new();

    /// <summary>
    /// Gets the type identifiers for a message type.
    /// Returns identifiers for the type and all implemented message interfaces.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <returns>The type identifiers.</returns>
    public IReadOnlyList<string> GetMessageTypes<TMessage>()
        where TMessage : class, IMessage
        => GetMessageTypes(typeof(TMessage));

    /// <summary>
    /// Gets the type identifiers for a message type.
    /// Returns identifiers for the type and all implemented message interfaces.
    /// </summary>
    /// <param name="messageType">The message type.</param>
    /// <returns>The type identifiers.</returns>
    public IReadOnlyList<string> GetMessageTypes(Type messageType)
    {
        if (_typeToIdentifiers.TryGetValue(messageType, out var cached))
        {
            return cached;
        }

        var identifiers = BuildTypeIdentifiers(messageType);
        _typeToIdentifiers[messageType] = identifiers;

        // Pre-populate reverse lookup
        foreach (var identifier in identifiers)
        {
            _identifierToType.TryAdd(identifier, messageType);
        }

        return identifiers;
    }

    /// <summary>
    /// Resolves a type from its identifier.
    /// </summary>
    /// <param name="typeIdentifier">The type identifier (URN format).</param>
    /// <returns>The resolved type or an error.</returns>
    public VsaResult<Type> ResolveType(string typeIdentifier)
    {
        // Check cache first
        if (_identifierToType.TryGetValue(typeIdentifier, out var cached))
        {
            return cached;
        }

        // Try to resolve from the identifier
        var typeResult = ParseTypeFromIdentifier(typeIdentifier);
        if (typeResult.IsError)
        {
            return typeResult.Errors.ToResult<Type>();
        }

        var type = typeResult.Value;
        _identifierToType[typeIdentifier] = type;
        return type;
    }

    /// <summary>
    /// Registers a type for resolution.
    /// Call this to pre-register types that may be loaded from messages.
    /// </summary>
    /// <typeparam name="TMessage">The message type to register.</typeparam>
    public void Register<TMessage>()
        where TMessage : class, IMessage
        => GetMessageTypes<TMessage>();

    /// <summary>
    /// Registers all message types from an assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    public void RegisterAssembly(System.Reflection.Assembly assembly)
    {
        var messageTypes = assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IMessage).IsAssignableFrom(t));

        foreach (var type in messageTypes)
        {
            GetMessageTypes(type);
        }
    }

    /// <summary>
    /// Gets the primary type identifier for a message type.
    /// </summary>
    /// <param name="messageType">The message type.</param>
    /// <returns>The primary type identifier.</returns>
    public static string GetPrimaryIdentifier(Type messageType)
        => CreateTypeIdentifier(messageType);

    /// <summary>
    /// Gets the primary type identifier for a message type.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <returns>The primary type identifier.</returns>
    public static string GetPrimaryIdentifier<TMessage>()
        where TMessage : class, IMessage
        => CreateTypeIdentifier(typeof(TMessage));

    private static string[] BuildTypeIdentifiers(Type messageType)
    {
        var identifiers = new List<string> { CreateTypeIdentifier(messageType) };

        // Add identifiers for all implemented message interfaces
        foreach (var @interface in messageType.GetInterfaces())
        {
            // Skip marker interfaces
            if (@interface == typeof(IMessage) ||
                @interface == typeof(ICommand) ||
                @interface == typeof(IEvent))
            {
                continue;
            }

            // Only include interfaces that extend IMessage
            if (typeof(IMessage).IsAssignableFrom(@interface))
            {
                identifiers.Add(CreateTypeIdentifier(@interface));
            }
        }

        return [.. identifiers];
    }

    private static string CreateTypeIdentifier(Type type)
    {
        var ns = type.Namespace ?? "Global";
        var name = type.Name;

        // Handle generic types
        if (type.IsGenericType)
        {
            name = type.Name.Split('`')[0];
            var genericArgs = string.Join(",", type.GetGenericArguments().Select(t => t.Name));
            name = $"{name}[{genericArgs}]";
        }

        return $"{UrnPrefix}{ns}:{name}";
    }

    private static VsaResult<Type> ParseTypeFromIdentifier(string identifier)
    {
        if (!identifier.StartsWith(UrnPrefix, StringComparison.Ordinal))
        {
            return MessagingErrors.UnknownMessageType(identifier);
        }

        var typePart = identifier[UrnPrefix.Length..];
        var separatorIndex = typePart.LastIndexOf(':');

        if (separatorIndex < 0)
        {
            return MessagingErrors.UnknownMessageType(identifier);
        }

        var ns = typePart[..separatorIndex];
        var name = typePart[(separatorIndex + 1)..];
        var fullName = $"{ns}.{name}";

        // Try to find the type in loaded assemblies
        var type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(fullName))
            .FirstOrDefault(t => t is not null);

        if (type is null)
        {
            return MessagingErrors.UnknownMessageType(identifier);
        }

        return type;
    }
}
