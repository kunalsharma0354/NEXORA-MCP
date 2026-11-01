# Database Engineering

Use this skill for schema changes, migrations and query design on existing storage.

## 1. Model the data before the table

- Write the entities and their relationships in plain language first.
- For each entity list: identity, attributes, lifecycle, owner, and what must never be null.
- Decide cardinality explicitly (1:1, 1:N, N:M). N:M needs a join table with its own constraints.
- Name tables and columns consistently with existing ones. Do not mix `camelCase` and `snake_case`.

## 2. Integrity belongs in the database

Application checks race; constraints do not.

- Primary key on every table.
- Foreign keys with an explicit delete behaviour (`RESTRICT` by default, not `CASCADE` unless intended).
- `NOT NULL` where the domain forbids absence.
- `UNIQUE` for natural keys (email, slug, external id).
- `CHECK` for ranges/enums that must never be violated.
- Timestamps: created/updated stored as UTC with a consistent type.

## 3. Migrations

Every schema change is a migration file, never a manual edit.

Rules:

1. Additive first: add nullable column → backfill → add constraint. Never add `NOT NULL` to a populated table in one step.
2. Renames are two migrations (add new, migrate data, drop old) so old code keeps working during rollout.
3. Drop columns only after no code references them.
4. Large backfills run in batches with a time budget; never one transaction over millions of rows.
5. Every migration has a rollback path or an explicit statement that it is irreversible and why.
6. Test on a copy with production-shaped data, not an empty database.

## 4. Queries

- Select explicit columns; never `SELECT *` in application code.
- Filter and join on indexed columns; confirm with an explain plan.
- Avoid functions on indexed columns in `WHERE` — they disable the index.
- Paginate with a stable sort key; offset pagination degrades on large tables.
- Aggregations over large sets belong in a materialised view or a scheduled job, not in a request path.

## 5. Review checklist

- [ ] Constraints cover every invariant the domain relies on.
- [ ] Migration is additive and re-runnable according to repo conventions.
- [ ] Rollback or irreversibility documented.
- [ ] Backfill batched and measured.
- [ ] New queries have an explain plan, not a guess.
- [ ] No PII written to logs or test fixtures.
- [ ] Indexes added for the new access patterns, and unused ones left alone.

## Definition of done

Schema matches the model, invariants are enforced in the database, the migration ran on realistic data, and the queries are explained.
