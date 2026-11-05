# 0002 — F02 capacity token: reuse `Users.ConcurrencyStamp`; deadlocks map to Conflict

Status: **Accepted** (approved by the user 2026-11-05) · Design by `architect`

## Context

F02 D7 requires the "max N `InProgress` per assignee" rule to be guarded by a concurrency token on the assignee row, changed in the same `SaveChanges()` as the task (AC18). `Users` already carries ASP.NET Identity's `ConcurrencyStamp`, configured `IsConcurrencyToken()`. F01 T7 left the choice to F02. ROADMAP R13 showed that an FK-child insert plus an `UPDATE` of the parent row in one commit produces InnoDB lock-upgrade deadlocks (1213) under real concurrency; F02's writes take FK S-locks on the same `Users` row they then update.

## Decision

- The token is `Users.ConcurrencyStamp`. Move and Assign load the target assignee tracked, read it **before** counting, and set a new GUID string when the write makes the task occupy a new slot for that user. No schema change for the token.
- Both concurrency outcomes — `DbUpdateConcurrencyException` and MySQL 1213 (`TaskConcurrencyHelper.IsDeadlock`) — are caught around the single `SaveChanges()` and returned as the operation's existing `…Conflict` code. No `EnableRetryOnFailure`, no application-level retry.

## Consequences

- No migration for the token; the only F02 migration is an additive index.
- Identity and capacity writes on the same user can conflict: a failed-login lockout update (`AccessFailedAsync`) racing a capacity write makes one side fail — the task side returns `Conflict`, the Identity side may not count that failed attempt. Rare; accepted.
- `Users.UpdatedAt/UpdatedBy` are stamped by `UnitOfWork` on capacity writes; nothing reads them.
- Under true concurrency the loser sees `Conflict` rather than `CapacityReached`; the client refetches and a retry yields the accurate code. Two simultaneous moves to an assignee with room can also produce a spurious `Conflict`.

## Alternatives rejected

- **New `Users.CapacityVersion` column** — EF places every concurrency token of an entity in the `WHERE`, so it would not decouple from Identity; redundant.
- **1:1 `UserCapacity` table** — new entity plus a first-insert race (duplicate key) to handle.
- **Token on `TaskItem`** — does not serialise two *different* tasks, which is AC18's case.
- **`SELECT … FOR UPDATE` in an explicit transaction** — raw SQL, breaks F01 T2's single-`SaveChanges()` rule, and D7 names a token.
- **Automatic retry on 1213** — `EnableRetryOnFailure` is excluded; an application retry adds code for a case the client already resolves by refetching.
