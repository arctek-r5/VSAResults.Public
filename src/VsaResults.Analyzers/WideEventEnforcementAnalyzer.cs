using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace VsaResults.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WideEventEnforcementAnalyzer : DiagnosticAnalyzer
{
    private const string ConfigKey = "build_property.VsaWideEventEnforcement";
    private static readonly string[] ExcludedPathFragments =
    [
        "/src/Host/Program.cs",
        "/src/Host/Composition/Startup/",
        "/src/Host/Composition/Middleware/AuthRejectionMiddleware.cs",
        "/src/Host/Composition/Middleware/TenantRouteValidationMiddleware.cs",
        "/src/Host/Composition/Hubs/UnifiedHub.cs",
        "/src/Host/Composition/SideEffects/AuditSideEffect.cs"
    ];

    private static readonly ImmutableArray<DiagnosticDescriptor> Diagnostics =
    [
        DiagnosticDescriptors.ConsoleOutput,
        DiagnosticDescriptors.ILoggerUsage,
        DiagnosticDescriptors.DebugTraceOutput,
        DiagnosticDescriptors.SerilogStaticCalls
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Diagnostics;

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var severity = GetConfiguredSeverity(compilationContext.Options.AnalyzerConfigOptionsProvider);
            if (severity is null)
                return;

            var knownTypes = new KnownTypes(compilationContext.Compilation);
            if (!knownTypes.HasAnyTypes)
                return;

            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, knownTypes, severity.Value),
                SyntaxKind.InvocationExpression);
        });
    }

    private static DiagnosticSeverity? GetConfiguredSeverity(AnalyzerConfigOptionsProvider provider)
    {
        if (!provider.GlobalOptions.TryGetValue(ConfigKey, out var value) || string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim().ToLowerInvariant() switch
        {
            "warn" => DiagnosticSeverity.Warning,
            "error" => DiagnosticSeverity.Error,
            _ => null,
        };
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        KnownTypes knownTypes,
        DiagnosticSeverity severity)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (IsExcludedPath(invocation.SyntaxTree.FilePath))
            return;

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol methodSymbol)
            return;

        var containingType = methodSymbol.ContainingType;
        if (containingType is null)
            return;

        DiagnosticDescriptor? descriptor = null;

        // VSA1001: Console output
        if (knownTypes.Console is not null &&
            SymbolEqualityComparer.Default.Equals(containingType, knownTypes.Console) &&
            IsConsoleOutputMethod(methodSymbol.Name))
        {
            descriptor = DiagnosticDescriptors.ConsoleOutput;
        }
        // VSA1003: Debug/Trace output
        else if (knownTypes.Debug is not null &&
                 SymbolEqualityComparer.Default.Equals(containingType, knownTypes.Debug))
        {
            descriptor = DiagnosticDescriptors.DebugTraceOutput;
        }
        else if (knownTypes.Trace is not null &&
                 SymbolEqualityComparer.Default.Equals(containingType, knownTypes.Trace))
        {
            descriptor = DiagnosticDescriptors.DebugTraceOutput;
        }
        // VSA1002: ILogger usage
        else if (IsLoggerCall(methodSymbol, containingType, knownTypes))
        {
            descriptor = DiagnosticDescriptors.ILoggerUsage;
        }
        // VSA1004: Serilog static calls
        else if (knownTypes.SerilogLog is not null &&
                 SymbolEqualityComparer.Default.Equals(containingType, knownTypes.SerilogLog))
        {
            descriptor = DiagnosticDescriptors.SerilogStaticCalls;
        }

        if (descriptor is null)
            return;

        var effectiveDescriptor = DiagnosticDescriptors.WithSeverity(descriptor, severity);
        var memberName = $"{containingType.Name}.{methodSymbol.Name}";
        context.ReportDiagnostic(Diagnostic.Create(effectiveDescriptor, invocation.GetLocation(), memberName));
    }

    private static bool IsConsoleOutputMethod(string methodName) =>
        methodName == "Write" || methodName == "WriteLine";

    private static bool IsLoggerCall(IMethodSymbol method, INamedTypeSymbol containingType, KnownTypes knownTypes)
    {
        // Extension methods on logger types (LoggerExtensions.LogInformation, custom Serilog adapters, etc.)
        if (method.IsExtensionMethod && method.Parameters.Length > 0)
        {
            var receiverType = method.Parameters[0].Type;

            if (knownTypes.ILogger is not null && IsLoggerType(receiverType, knownTypes.ILogger))
            {
                return true;
            }

            if (knownTypes.SerilogILogger is not null && IsLoggerType(receiverType, knownTypes.SerilogILogger))
            {
                return true;
            }
        }

        // Instance calls on ILogger (logger.Log, etc.)
        if (knownTypes.ILogger is not null && method.Name.StartsWith("Log", StringComparison.Ordinal))
        {
            return IsLoggerType(containingType, knownTypes.ILogger);
        }

        // Instance calls on Serilog.ILogger (logger.Information(...), etc.)
        if (knownTypes.SerilogILogger is not null && IsSerilogLoggerMethod(method.Name))
        {
            return IsLoggerType(containingType, knownTypes.SerilogILogger);
        }

        return false;
    }

    private static bool IsLoggerType(ITypeSymbol typeSymbol, INamedTypeSymbol loggerType)
    {
        if (typeSymbol is not INamedTypeSymbol namedType)
        {
            return false;
        }

        if (SymbolEqualityComparer.Default.Equals(namedType, loggerType))
        {
            return true;
        }

        return namedType.AllInterfaces.Any(iface => SymbolEqualityComparer.Default.Equals(iface, loggerType));
    }

    private static bool IsSerilogLoggerMethod(string methodName) =>
        methodName is "Verbose" or "Debug" or "Information" or "Warning" or "Error" or "Fatal" or "Write";

    private static bool IsExcludedPath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        var normalized = filePath!.Replace('\\', '/');
        return ExcludedPathFragments.Any(normalized.Contains);
    }
}
