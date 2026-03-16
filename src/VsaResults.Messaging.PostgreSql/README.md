# VsaResults.Messaging.PostgreSql

PostgreSQL saga state persistence for `VsaResults.Messaging`.

## What This Is

This package provides a PostgreSQL-backed `ISagaRepository<TState>` implementation for VsaResults messaging workflows. It stores saga state, reloads it for correlated messages, and supports durable orchestration beyond the in-memory test repository.

## Key Types

| Type | Purpose |
|------|---------|
| `PostgreSqlSagaRepository<TState>` | Dapper-backed saga repository for PostgreSQL. |
| `PostgreSqlSagaExtensions` | DI registration helpers for PostgreSQL saga persistence. |
| `SagaStateSql` | Shared SQL definitions for saga state operations. |

## Dependency Chain

```
VsaResults.Messaging
  ^-- VsaResults.Messaging.PostgreSql (this project)
```

**Depends on:** `VsaResults.Messaging`, `Dapper`, `Npgsql`.

**Depended on by:** Any host or worker that needs PostgreSQL-backed saga persistence.

## Do NOT

- **Do not reference this project from Modules or Kernel.** Transport and persistence wiring belongs in Hosts and Workers.
- **Do not hardcode PostgreSQL connection strings in source code.** Use configuration, secret stores, or environment variables.
- **Do not use the in-memory saga repository in environments that require durable orchestration.** Use this package instead.
