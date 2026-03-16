; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md
### New Rules
Rule ID | Category | Severity | Notes
--------|----------|----------|---------------------------------------------------------------
VSA1001 | VsaResults.WideEvents | Warning  | Avoid Console output; use wide events instead
VSA1002 | VsaResults.WideEvents | Warning  | Avoid ILogger usage; use wide events instead
VSA1003 | VsaResults.WideEvents | Warning  | Avoid Debug/Trace output; use wide events instead
VSA1004 | VsaResults.WideEvents | Warning  | Avoid Serilog static calls; use wide events instead
