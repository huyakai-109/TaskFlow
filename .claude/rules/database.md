---
paths:
  # TaskFlow: entities, configurations, both DbContexts and both migration histories.
  - "BE/TaskFlow.DataAccess/**/*.cs"
  - "**/*.sql"
---

# Database Rules

Schema, entity configuration, migrations, indexes and data integrity. How services *query* data is in `backend.md` §6. EF Core recipes: `.claude/references/dotnet-patterns.md` (entity: Recipe 5, concurrency: 14, history: 15).

**Ownership.** The implementer (`backend-lead`) writes entities, configurations and migration files. Applying a migration to any database (local included — §6), backups and restores **always need the user's approval**; on the deployment target they belong to the release (`ship`, `devops-lead`). An app that migrates on startup applies migrations when it is run — treat running it as applying them.

## 1. Schema design

- **[M]** Follow the project's naming. House style: table = plural entity name, `long` identity key `Id`, foreign keys `long` / `long?` named `<Entity>Id`, audit columns inherited from `BaseEntity` (never redeclared), soft delete via `IIsDeletedEntity` + `IsDeleted`.
- **[M]** Every string column has a maximum length; unbounded text is a stated decision. Money is `decimal` with explicit precision (`HasPrecision(18, 2)` or the project's); never `float`/`double` for money.
- **[M]** Times are stored in UTC (`DateTimeOffset`, or `DateTime` treated as UTC — whichever the project uses, consistently). A calendar date with no time is `DateOnly` (EF Core 8+) or a date column.
- **[M]** Nullability mirrors the domain: required in the business → `NOT NULL`. With nullable reference types on (the default for `net10.0`), entity strings are `= string.Empty` or `required`, collections initialised.
- **[M]** Every reference is a real foreign key. Delete behaviour is `Restrict` for business data; cascade only for owned children that have no meaning alone.
- **[M]** Enums: follow the project's storage (int in the house style). Never reorder or renumber existing members — stored values would change meaning.

## 2. Integrity lives in the database too

Application checks give friendly error codes; constraints make the rule true under concurrency.

- **[M]** A natural key that must be unique (code, email, slug) gets a unique index as well as the service check, decided when the column is created. Adding one to a populated table is a **Risky** migration (§5): query for duplicates first; it is a planned change, not part of a bug fix. Map the unique-violation exception to the same error code where the project does so.
- **[M]** Uniqueness follows one comparison rule in code and database: SQL Server and MySQL default collations are case-insensitive, PostgreSQL is case-sensitive. Match the service check to the column's collation (or normalise the value) and say which.
- **[C]** With soft delete, the unique index must ignore deleted rows: a filtered index (`HasFilter("[IsDeleted] = 0")` on SQL Server, `"\"IsDeleted\" = false"` on PostgreSQL). **MySQL has no filtered indexes** — rely on the service check plus a concurrency token on the owning row, or a generated column; state which.
- **[M]** Invariants that count rows ("one default per owner", "at most N active") use a concurrency token on the owning row or a filtered unique index (Recipe 14).
- **[R]** Simple value rules that must never be violated (non-negative quantity, end ≥ start) may be check constraints — when the project already uses them.

## 3. Indexes

- **[M]** Index what hot queries filter, join and sort on. EF creates an index per foreign key — on a large existing table that index build is itself a Risky step; add composite indexes for frequent filter + sort combinations — equality columns first, then range, then sort.
- **[M]** Do not add speculative indexes: each one slows writes. Every new index answers a named query.
- **[R]** `LIKE '%x%'` and functions on columns cannot use an index. On tables expected to grow, design search as prefix matching, a dedicated search column, or the provider's full-text feature — as a decision.
- **[R]** For a slow query, read the plan (`EXPLAIN` / actual execution plan) before and after the change; report the numbers.

## 4. Migrations — authoring

- **[M]** One migration per logical change, named for intent (`AddProductCategory`, not `Update3`). Generated with the project's command (`CLAUDE.md` → Development Commands).
- **[M]** Read the generated migration before accepting it. EF turns a rename into drop + add unless told otherwise (`RenameColumn`), and can add `NOT NULL` columns without a default.
- **[M]** Never edit a migration that has been applied anywhere outside your machine; add a new one. Keep the model snapshot in sync (`dotnet ef migrations has-pending-model-changes`, EF Core 8+).
- **[M]** `Down()` is implemented and correct, or the migration is declared irreversible in the report.
- **[M]** Seed data is deterministic: fixed ids from a constants class, fixed timestamps — never `DateTime.Now` / `Guid.NewGuid()` in `HasData`.
- **[M]** Data fixes that touch many rows run in batches and are idempotent (safe to run twice).

## 5. Migrations — safety class

Classify every schema change in the plan and in the report.

| Class | Examples | Rule |
|---|---|---|
| **Additive** | New table; nullable column; new index on a small table | Safe in one release. |
| **Risky** | `NOT NULL` column on a populated table; unique index on existing data; type or length narrowing; index on a large table | Plan it: default or backfill first; query for duplicates or oversized values before adding the constraint; report expected lock time. |
| **Destructive** | Drop a column or table; rename; type change that loses data | **Expand → migrate → contract** across separate releases: add the new shape, backfill and switch the code, drop the old shape only after nothing reads it. A backup and explicit user approval precede the drop. |

The running application must keep working against the schema during deployment: a migration that removes or renames something the deployed code still reads is a broken release.

## 6. Applying migrations

- **[M]** Applying a migration to any database — local included — needs approval (`workflow.md` §3). Ask once, in the plan: "apply to the local dev database for verification?" Generating and reading migrations needs none.
- **[M]** For review and for release, produce the SQL: `dotnet ef migrations script <from> <to> --idempotent` and read it.
- **[C]** Applying migrations on application startup is acceptable only where the project already does it and has a single instance; otherwise apply as a release step (`ship`).

## 7. Verification

```
dotnet ef migrations add <Name> --project <DataAccess> --startup-project <Server>   # generates, compiles
dotnet ef migrations has-pending-model-changes --project <DataAccess> --startup-project <Server>
dotnet ef migrations script --idempotent --project <DataAccess> --startup-project <Server>  # read the SQL
```

Report the safety class, whether `Down()` works, and whether the migration was applied anywhere (it normally is not: `NOT VERIFIED — not applied`).
