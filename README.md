# VsaResults

Core discriminated union type for elegant error handling in C#. Zero external dependencies.

This is the foundation of the VsaResults family. Every other VsaResults package depends on this one.

## What This Is

A minimal `VsaResult<T>` type that represents either a successful value or a list of errors. Business rule rejections are outcomes, not exceptions.

## Key Types

| Type | Purpose |
|------|---------|
| `VsaResult<T>` | Discriminated union: value or errors. The core type everything is built on. |
| `Error` | Readonly record struct with `Code`, `Description`, `Type`, and optional `Metadata`. |
| `ErrorType` | Enum: `Failure`, `Unexpected`, `Validation`, `Conflict`, `NotFound`, `Unauthorized`, `Forbidden`, `BadRequest`, `Timeout`, `Gone`, `Locked`, `TooManyRequests`, `Unavailable`. |
| `IVsaResult<T>` | Interface for the result type (covariant on T). |
| `VsaResultFactory` | Static helpers: `From<T>`, `FromError<T>`, `FromErrors<T>`, `Try<T>`, `TryAsync<T>`. |
| `Success`, `Created`, `Deleted`, `Updated` | Marker structs for void-equivalent results (e.g., `VsaResult<Success>`). |

## Fluent API

`VsaResult<T>` supports a rich set of functional operations, each in its own partial file:

- **`Then`** / **`ThenAsync`** -- chain operations on the value
- **`Match`** / **`MatchAsync`** -- map both value and error cases
- **`Switch`** / **`SwitchAsync`** -- execute actions on both cases (no return)
- **`Else`** / **`ElseAsync`** -- provide fallback values on error
- **`FailIf`** / **`FailIfAsync`** -- conditionally fail a successful result
- **`Select`** -- project the value to a new type
- **`MapError`** -- transform errors
- **`OrElse`** -- chain recovery logic
- **`WithContext`** -- attach immutable context for wide events
- **`Combine`** -- merge multiple results
- **`ToResult`** -- extension to wrap any value in a `VsaResult<T>`
- **`Try`** / **`TryAsync`** -- wrap exception-throwing code as results

## Context System

`VsaResult<T>` carries an immutable `ImmutableDictionary<string, object>?` context that flows through fluent chains. This context is merged into wide events at the end of feature execution.

```csharp
var result = await GetOrderAsync(id)
    .WithContext("order_id", id)
    .ThenAsync(order => ProcessAsync(order));
```

## Dependency Chain

```
VsaResults (this project)
  ^-- VsaResults.Features
  ^-- VsaResults.AspNetCore
  ^-- VsaResults.Messaging
  ^-- Kernel
  ^-- Modules.Provisioning
  ^-- Host
```

**Depends on:** Nothing. Zero external dependencies.

**Depended on by:** Everything. This is the leaf of the dependency tree.

## InternalsVisibleTo

- `VsaResults.Features`
- `VsaResults.AspNetCore`
- `Tests`

## Do NOT

- **Do not throw exceptions for business rule rejections.** Return `VsaResult<T>` with typed errors instead. Exceptions are reserved for broken system states.
- **Do not use `new VsaResult<T>()`** -- the default constructor throws. Use implicit conversions, `VsaResultFactory`, or `.ToResult()`.
- **Do not store mutable objects in `Error.Metadata`** -- it changes the hash code and breaks collections. Use immutable types (string, int, bool, ImmutableArray).
- **Do not add external dependencies to this project.** It must remain zero-dependency.
- **Do not reference `VsaResults.Features` or any other VsaResults package from here.** This is the foundation; the arrow only points downward.

## License

Released under the MIT License. See `LICENSE`.

## Standalone Repo Extraction

To publish the VsaResults family into its own GitHub repository using `git subtree`, see `SUBTREE-EXTRACTION.md`.
