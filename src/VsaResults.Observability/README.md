# VsaResults.Observability

PII masking and observability extensions for the VsaResults wide events system. Ensures sensitive data is masked before wide events are emitted to sinks.

## What This Is

An add-on to `VsaResults.Features` that provides:

1. **PII Masking** -- Detects and replaces personally identifiable information (emails, phone numbers, IPs, etc.) with deterministic hashed values in wide event context.
2. **PII Masking Interceptor** -- A `IWideEventInterceptor` that automatically masks PII in all wide event fields before emission.

## Key Types

### Masking

| Type | Purpose |
|------|---------|
| `IPiiMasker` | Interface for PII masking operations. Methods: `MaskString`, `MaskNullableString`, `MaskValue`. |
| `DefaultPiiMasker` | Default implementation: regex-based detection of emails, phone numbers, IPs, etc. Produces deterministic hashed replacements (e.g., `EM_abc123def456`). |
| `NullPiiMasker` | No-op implementation. Use when PII masking should be disabled. |
| `PiiMaskerOptions` | Configuration: custom patterns, sensitive key names, hash salt. |
| `PiiPattern` | Defines a custom PII detection pattern: name, regex, and prefix for masked values. |

### Interceptor

| Type | Purpose |
|------|---------|
| `PiiMaskingInterceptor` | `IWideEventInterceptor` (order: -400). Masks PII in wide event context, error segments, and message segments before emission. Runs after sampling but before context enrichment. |

## DI Registration

```csharp
// Enable PII masking with defaults
services.AddPiiMasking();

// With custom options
services.AddPiiMasking(options =>
{
    options.AddPattern(new PiiPattern("SSN", @"\d{3}-\d{2}-\d{4}", "SSN_"));
});

// Register the wide event interceptor
services.AddPiiMaskingInterceptor();

// Or disable masking entirely
services.AddNullPiiMasking();
```

Call `AddPiiMasking()` and `AddPiiMaskingInterceptor()` **after** `AddWideEvents()`.

## How Masking Works

1. The `PiiMaskingInterceptor` hooks into the wide event pipeline at order `-400`.
2. Before a wide event is emitted, it scans all context values, error messages, and message segment addresses.
3. String values are checked against PII patterns (email, phone, IP, etc.) and replaced with deterministic hashes.
4. The hash is prefixed with the pattern type (e.g., `EM_` for emails) so you can tell what was masked.
5. Already-redacted values (from `RedactionInterceptor`) are skipped.

## Dependency Chain

```
VsaResults.Features
  ^-- VsaResults.Observability (this project)
        ^-- Host
        ^-- ProvisioningWorker
        ^-- Modules.Provisioning
        ^-- Platform.Data.Dapper
```

**Depends on:** `VsaResults.Features`, Microsoft.Extensions.DI/Options.

**Depended on by:** `Host`, `ProvisioningWorker`, `Modules.Provisioning`, `Platform.Data.Dapper`.

## Do NOT

- **Do not log PII directly in wide event context.** If you must include user-identifying data, rely on the masking interceptor to handle it.
- **Do not use `NullPiiMasker` in production.** It disables all masking and may expose PII in logs.
- **Do not add masking logic inside feature pipeline stages.** Let the interceptor handle it centrally.
- **Do not skip registering `AddPiiMaskingInterceptor()` when `AddPiiMasking()` is registered.** The masker alone does nothing; the interceptor wires it into the wide event pipeline.
