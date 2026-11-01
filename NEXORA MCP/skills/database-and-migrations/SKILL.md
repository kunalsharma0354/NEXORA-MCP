---
id: database-and-migrations
name: Database Design & Safe Migrations
version: 1.0.0
description: Design data models and evolve schemas while preserving integrity, performance, and existing user data.
category: database
tags: [database, schema, migrations, sql, integrity]
---

# Database Design & Safe Migrations

Design data models and evolve schemas while preserving integrity, performance, and existing user data.

## When to use
Use when adding tables/collections, modifying schema, writing queries, or changing persisted data.

## Inspect before changing
Identify the actual database, ORM/query layer, schema conventions, migration system, transaction support, indexes, and current data relationships. Never assume a database vendor.

## Data modeling
- Define entities, ownership, relationships, uniqueness, nullability, lifecycle, and deletion behavior.
- Use constraints for invariants that must hold regardless of which application path writes the data.
- Store timestamps consistently and document timezone assumptions.
- Choose indexes based on real query/filter/order patterns; avoid indexing every column.
- Consider pagination and expected data growth.
- Avoid duplicating derived values unless consistency is explicitly managed.

## Migration workflow
1. Review current schema and affected queries.
2. Write a reversible migration when feasible.
3. For large or production tables, prefer backward-compatible expand/migrate/contract steps.
4. Backfill in bounded batches when necessary.
5. Consider locks, table rewrites, downtime, and deployment ordering.
6. Preserve existing data and provide a recovery plan for risky changes.
7. Update model types, queries, fixtures, and documentation.
8. Test migrations against a representative disposable database or supported test environment.

## Query safety
- Use parameterized queries or safe ORM methods.
- Use transactions for related changes that require atomicity.
- Avoid N+1 access patterns when practical.
- Inspect query plans when performance is a concern.
- Enforce tenant/user isolation in data access paths.

## Do not
Never run destructive production migrations, drop data, or perform broad data rewrites without explicit authorization and a verified backup/recovery strategy. Do not claim a migration is safe without testing it.

## Final response
Summarize what changed, relevant files or components, checks actually run, results, and any remaining limitations. Be concise, specific, and honest.
