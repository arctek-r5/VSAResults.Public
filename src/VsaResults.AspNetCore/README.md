# VsaResults.AspNetCore

ASP.NET Core integration for VsaResults. Bridges the gap between the VSA feature pipeline and HTTP endpoints (Minimal APIs and MVC Controllers).

## What This Is

Provides three things:

1. **`ApiResults`** -- Converts `VsaResult<T>` to ASP.NET Core `IResult` with correct HTTP status codes and Problem Details.
2. **`FeatureHandler`** -- Static factory for creating Minimal API delegate handlers that execute features and map results.
3. **`EndpointRouteBuilderExtensions`** -- Ultra-minimal endpoint registration (`MapGetFeature`, `MapPostFeature`, etc.).
4. **`RequestBinder`** -- Manual model binding with wide event coverage for binding failures.

## Key Types

### Result Mapping

| Type | Purpose |
|------|---------|
| `ApiResults` | Static methods: `Ok()`, `Created()`, `NoContent()`, `Accepted()`, `ToProblem()`. Maps `ErrorType` to HTTP status codes. |

### ErrorType to HTTP Status Code Mapping

| ErrorType | HTTP Status |
|-----------|-------------|
| `Validation`, `BadRequest` | 400 |
| `Unauthorized` | 401 |
| `Forbidden` | 403 |
| `NotFound` | 404 |
| `Timeout` | 408 |
| `Conflict` | 409 |
| `Gone` | 410 |
| `Locked` | 423 |
| `TooManyRequests` | 429 |
| `Failure`, `Unexpected` | 500 |
| `Unavailable` | 503 |

Validation errors are returned as `ValidationProblemDetails` with errors grouped by code.

### Feature Handlers

| Method | HTTP Semantic |
|--------|--------------|
| `FeatureHandler.QueryOk<TReq, TRes>()` | Query -> 200 OK |
| `FeatureHandler.MutationOk<TReq, TRes>()` | Mutation -> 200 OK |
| `FeatureHandler.MutationCreated<TReq, TRes>(locationSelector)` | Mutation -> 201 Created |
| `FeatureHandler.MutationNoContent<TReq>()` | Mutation -> 204 No Content |
| `FeatureHandler.Query<TReq, TRes>(mapper)` | Query -> custom mapping |
| `FeatureHandler.Mutation<TReq, TRes>(mapper)` | Mutation -> custom mapping |

All handlers resolve `IQueryFeature` / `IMutationFeature` and `IWideEventEmitter` from DI automatically.

**Bound variants** (`QueryOkBound`, `MutationOkBound`, `MutationAcceptedBound`, etc.) use `RequestBinder` for manual model binding with wide event emission on binding failures.

### Endpoint Registration

```csharp
app.MapGetFeature<GetUser.Request, UserDto>("/users/{id}");
app.MapPostFeature<CreateUser.Request, UserDto>("/users", r => $"/users/{r.Id}");
app.MapPutFeature<UpdateUser.Request, UserDto>("/users/{id}");
app.MapDeleteFeature<DeleteUser.Request>("/users/{id}");
app.MapPatchFeature<UpdateStatus.Request, StatusDto>("/users/{id}/status");
app.MapPostFeatureAcceptedBound<StartVm.Request, VmOperationAcceptedResponse>("/vms/{vmId}/start");
```

### Request Binding

| Type | Purpose |
|------|---------|
| `RequestBinder` | Binds from route, query, header, body, and DI services. Handles records, init-only properties, and collections. |
| `BindingSource` | Enum: `Route`, `Query`, `Header`, `Body`, `Services`, `None`. |
| `PropertyBindingInfo` | Cached metadata about how each request property should be bound. |

## Dependency Chain

```
VsaResults
  ^-- VsaResults.Features
        ^-- VsaResults.AspNetCore (this project)
              ^-- Host
```

**Depends on:** `VsaResults`, `VsaResults.Features`, `Microsoft.AspNetCore.App` (framework reference).

**Depended on by:** `Host`.

## Do NOT

- **Do not hardcode route strings in endpoint handlers.** Use the centralized `Endpoints` class in `src/Host/Composition/Routing/Endpoints.cs`.
- **Do not manually create `ProblemDetails` responses.** Use `ApiResults.ToProblem()` which handles the `ErrorType` -> status code mapping consistently.
- **Do not call `feature.ExecuteAsync()` without passing the `IWideEventEmitter`.** Without it, you lose observability. The `FeatureHandler` methods handle this automatically.
- **Do not use `FeatureHandler` methods and then also manually map errors.** The handlers already convert errors to Problem Details.
- **Do not add HTTP-specific logic into feature pipeline stages.** The pipeline is transport-agnostic. HTTP concerns stay in this project.
