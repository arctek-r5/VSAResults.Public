namespace VsaResults.AspNetCore.Binding;

/// <summary>
/// Binding source for a property.
/// </summary>
internal enum BindingSource
{
    /// <summary>Route parameter binding.</summary>
    Route,

    /// <summary>Query string binding.</summary>
    Query,

    /// <summary>Header binding.</summary>
    Header,

    /// <summary>Request body binding.</summary>
    Body,

    /// <summary>Dependency injection service binding.</summary>
    Services,

    /// <summary>No specific binding source.</summary>
    None,
}
