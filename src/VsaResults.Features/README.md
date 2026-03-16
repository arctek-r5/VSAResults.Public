# VsaResults.Features

The VSA (Vertical Slice Architecture) feature pipeline. This is where request processing is orchestrated through a series of well-defined stages with automatic wide event emission.

## What This Is

A framework for building features as composable pipelines:

**Request --> Validator --> Requirements --> Mutator/Query --> SideEffects --> Wide Event**

Each stage has a single responsibility. The pipeline automatically emits one structured wide event per feature execution for observability.

## Key Types

### Pipeline Interfaces

| Interface | Stage | Responsibility |
|-----------|-------|---------------|
| `IFeatureValidator<TRequest>` | Validation | Validate shape and format. Returns `VsaResult<TRequest>`. |
| `IFeatureRequirements<TRequest>` | Requirements | Auth + entity loading. Returns `VsaResult<FeatureContext<TRequest>>`. |
| `IFeatureMutator<TRequest, TResult>` | Execution | Core business logic (mutations). Returns `VsaResult<TResult>`. |
| `IFeatureQuery<TRequest, TResult>` | Execution | Read-only data retrieval. Returns `VsaResult<TResult>`. |
| `IFeatureSideEffects<TRequest>` | Side Effects | Audit, notifications, events. Returns `VsaResult<Unit>`. |
| `IFeaturePermission<TRequest>` | Permission | Authorization check (resource + action). Returns `VsaResult<TRequest>`. |

### Feature Orchestrators

| Type | Purpose |
|------|---------|
| `IMutationFeature<TRequest, TResult>` | Interface for state-changing features (create, update, delete). |
| `IQueryFeature<TRequest, TResult>` | Interface for read-only features. |
| `MutationFeature<TRequest, TResult>` | Base class that wires pipeline stages via constructor injection. |
| `QueryFeature<TRequest, TResult>` | Base class for query features. |

### Context and Helpers

| Type | Purpose |
|------|---------|
| `FeatureContext<TRequest>` | Carries request + loaded entities + wide event context through the pipeline. |
| `EntityKey<T>` | Strongly-typed key for storing/retrieving entities in the context. |
| `Unit` | Void equivalent for `VsaResult<Unit>` (side effects, no-content responses). |
| `ValidationContext` | Fluent builder for accumulating multiple validation errors. |
| `FluentValidationFeatureValidator<T>` | Auto-discovers `IValidator<T>` from DI and delegates to FluentValidation. |

### No-Op Implementations

| Type | Purpose |
|------|---------|
| `NoOpValidator<T>` | Passes request through unchanged. |
| `NoOpRequirements<T>` | Creates context with no entity loading. |
| `NoOpSideEffects<T>` | Does nothing. |
| `NoOpPermission<T>` | Allows all requests. |

Use these explicitly when a stage is not needed.

### Wide Events

| Type | Purpose |
|------|---------|
| `WideEvent` | The unified wide event -- one comprehensive log event per operation. |
| `WideEventBuilder` | Fluent builder for constructing wide events during pipeline execution. |
| `IWideEventEmitter` | Emits wide events to sinks. |
| `IWideEventSink` | Destination for wide events (Serilog, structured log, in-memory, custom). |
| `IWideEventInterceptor` | Intercepts events before emission (sampling, redaction, PII masking, context limits). |
| `IWideEventContext` | Optional interface for requests needing complex context extraction. |
| `WideEventPropertyAttribute` | Attribute for marking request properties to include in wide events. |

### Built-in Interceptors

| Interceptor | Purpose |
|-------------|---------|
| `SamplingInterceptor` | Rate-limits wide event emission. |
| `RedactionInterceptor` | Redacts sensitive fields. |
| `ContextLimitInterceptor` | Caps context size to prevent bloat. |
| `VerbosityInterceptor` | Controls event verbosity level. |

### Built-in Sinks

| Sink | Purpose |
|------|---------|
| `SerilogWideEventSink` | Emits via Serilog-compatible ILogger. |
| `StructuredLogWideEventSink` | Emits all event properties as structured log scope. |
| `InMemoryWideEventSink` | In-memory buffer for testing. |
| `CompositeWideEventSink` | Fans out to multiple sinks. |

## DI Registration

```csharp
// Register all feature stages from assemblies
services.AddVsaFeatures(typeof(Program).Assembly);

// Register wide events with sinks
services.AddWideEvents(options => { ... });
services.AddSerilogWideEventSink();
// or
services.AddStructuredLogWideEventSink();
```

## Execution

Features are executed via extension methods on the orchestrator interfaces:

```csharp
// Mutation
var result = await feature.ExecuteAsync(request, emitter, ct);

// Query
var result = await feature.ExecuteAsync(request, emitter, ct);
```

The `FeatureExecutionExtensions` class handles the full pipeline orchestration, stage timing, wide event construction, and Activity/trace integration.

## Entity Context Pattern

Requirements loads entities for authorization; Mutator retrieves them from context:

```csharp
// In Requirements:
context.SetEntity("order", order);
// or with strongly-typed key:
context.SetEntity(OrderKey, order);

// In Mutator:
var order = context.GetEntity<Order>("order");
// or:
var order = context.GetEntity(OrderKey);
```

## Dependency Chain

```
VsaResults
  ^-- VsaResults.Features (this project)
        ^-- VsaResults.AspNetCore
        ^-- VsaResults.Messaging
        ^-- VsaResults.Observability
        ^-- Platform.Data
        ^-- Platform.Azure
```

**Depends on:** `VsaResults`, FluentValidation, Microsoft.Extensions.DI/Logging abstractions.

**Ships with:** `VsaResults.Analyzers` (packed as a Roslyn analyzer in the NuGet package).

## Do NOT

- **Do not mix stage responsibilities.** Validators must not load data. Requirements must not mutate state. Mutators must not check auth.
- **Do not reference other feature slices.** Each feature owns its pipeline stages. Shared logic goes in Kernel or Infrastructure.
- **Do not emit multiple log lines per feature execution.** The wide event system handles observability. One event per operation.
- **Do not log inside validators.** Validators return errors; they do not produce side effects.
- **Do not `GetEntity<T>()` without a prior `SetEntity()`.** It throws `KeyNotFoundException` at runtime.
- **Do not add manual start/finish logging for pipeline stages.** The pipeline already instruments each stage with timing and Activity spans.
- **Do not use `Task.Run` for background work.** Use the messaging system (RabbitMQ) instead.
