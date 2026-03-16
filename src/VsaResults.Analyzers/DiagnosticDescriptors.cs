using Microsoft.CodeAnalysis;

namespace VsaResults.Analyzers;

internal static class DiagnosticDescriptors
{
    private const string Category = "VsaResults.WideEvents";

    public static readonly DiagnosticDescriptor ConsoleOutput = new(
        id: "VSA1001",
        title: "Avoid Console output; use wide events instead",
        messageFormat: "'{0}' bypasses the wide events system. Use IWideEventEmitter or structured pipeline observability instead.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    // ReSharper disable once InconsistentNaming — name matches the ILogger type it detects
    public static readonly DiagnosticDescriptor ILoggerUsage = new(
        id: "VSA1002",
        title: "Avoid ILogger usage; use wide events instead",
        messageFormat: "'{0}' bypasses the wide events system. Use IWideEventEmitter or structured pipeline observability instead.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DebugTraceOutput = new(
        id: "VSA1003",
        title: "Avoid Debug/Trace output; use wide events instead",
        messageFormat: "'{0}' bypasses the wide events system. Use IWideEventEmitter or structured pipeline observability instead.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SerilogStaticCalls = new(
        id: "VSA1004",
        title: "Avoid Serilog static calls; use wide events instead",
        messageFormat: "'{0}' bypasses the wide events system. Use IWideEventEmitter or structured pipeline observability instead.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor WithSeverity(DiagnosticDescriptor descriptor, DiagnosticSeverity severity)
    {
        if (descriptor.DefaultSeverity == severity)
            return descriptor;

        return new DiagnosticDescriptor(
            descriptor.Id,
            descriptor.Title,
            descriptor.MessageFormat,
            descriptor.Category,
            severity,
            descriptor.IsEnabledByDefault);
    }
}
