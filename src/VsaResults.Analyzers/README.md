# VsaResults.Analyzers

Roslyn analyzers that enforce the wide events observability pattern. Detects and warns when code bypasses the wide event system by using direct logging or console output.

## What This Is

A `netstandard2.0` Roslyn analyzer assembly that ships inside the `VsaResults.Features` NuGet package (under `analyzers/dotnet/cs`). It runs at compile time and produces warnings or errors when code uses logging patterns that bypass the wide event system.

## Diagnostic Rules

| Rule ID | Title | Trigger |
|---------|-------|---------|
| **VSA1001** | Avoid Console output | `Console.Write()`, `Console.WriteLine()` |
| **VSA1002** | Avoid ILogger usage | `ILogger.Log*()`, `LoggerExtensions.LogInformation()`, `Serilog.ILogger.Information()`, etc. |
| **VSA1003** | Avoid Debug/Trace output | `Debug.Write*()`, `Trace.Write*()` |
| **VSA1004** | Avoid Serilog static calls | `Serilog.Log.*()` |

All rules share the same guidance: use `IWideEventEmitter` or the structured pipeline observability instead.

## Configuration

The analyzer is enabled in-repo through a central `Directory.Build.props` project-analyzer reference. Set the enforcement level in your `.csproj` or `Directory.Build.props`:

```xml
<PropertyGroup>
  <VsaWideEventEnforcement>warn</VsaWideEventEnforcement>
</PropertyGroup>
```

| Value | Effect |
|-------|--------|
| `warn` | Produces warnings (default severity). |
| `error` | Promotes all diagnostics to errors (blocks compilation). |
| *(not set)* | Falls back to the package default (`warn`). |

The value is read from the MSBuild property `VsaWideEventEnforcement` via `build_property.VsaWideEventEnforcement` in the analyzer config.

## Key Types

| Type | Purpose |
|------|---------|
| `WideEventEnforcementAnalyzer` | The Roslyn `DiagnosticAnalyzer`. Registers for `InvocationExpression` syntax nodes and checks against known types. |
| `DiagnosticDescriptors` | Static definitions for VSA1001-VSA1004. |
| `KnownTypes` | Resolves `System.Console`, `System.Diagnostics.Debug`, `System.Diagnostics.Trace`, `ILogger`, `LoggerExtensions`, `Serilog.Log` from the compilation. |

## How It Works

1. On compilation start, reads `VsaWideEventEnforcement` from MSBuild properties.
2. If not set, the package default applies (`warn`).
3. Resolves known types (`Console`, `ILogger`, `Debug`, etc.) from the compilation.
4. For every `InvocationExpression`, checks if the method's containing type matches a known type.
5. Reports a diagnostic with the configured severity.

## Dependency Chain

```
VsaResults.Analyzers (this project, netstandard2.0)
  ^-- VsaResults.Features (ships as embedded analyzer in NuGet)
```

**Depends on:** `Microsoft.CodeAnalysis.CSharp` (Roslyn).

**Depended on by:** `VsaResults.Features` (as a packed analyzer, not as a runtime reference).

**Test project:** `tests/Tests.Analyzers`.

## Do NOT

- **Do not add runtime dependencies to this project.** It is a Roslyn analyzer targeting `netstandard2.0`. It runs inside the compiler, not the application.
- **Do not reference this project as a runtime dependency from application code.** Use analyzer-only references such as the central repo `Directory.Build.props` wiring.
- **Do not add StyleCop or SourceLink to this project.** They are explicitly removed in the `.csproj` due to `netstandard2.0` incompatibilities.
- **Do not suppress VSA1001-VSA1004 diagnostics without good reason.** If you need logging outside the wide event system (e.g., caught exceptions, external state changes), those are legitimate uses -- but the wide event system should be the primary observability mechanism.

## Current Host Exceptions

The analyzer intentionally skips a small set of host files that operate outside the request feature pipeline and already have explicit observability rationale:

- `src/Host/Program.cs`
- `src/Host/Composition/Startup/**`
- `src/Host/Composition/Middleware/AuthRejectionMiddleware.cs`
- `src/Host/Composition/Middleware/TenantRouteValidationMiddleware.cs`
- `src/Host/Composition/Hubs/UnifiedHub.cs`
- `src/Host/Composition/SideEffects/AuditSideEffect.cs`
