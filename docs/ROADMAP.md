# Roadmap

Behaviour comes from `docs/brs/taskflow-brs.md`; the acceptance criteria that implementation is checked against live in `docs/features/`. Status vocabulary: **Planned → In Progress → Built → Ready for Review → Done**.

## Units

| # | Unit | Spec | Status | Notes |
|---|---|---|---|---|
| 1 | **F01 — Strict workflow state machine** | [f01-workflow.md](features/f01-workflow.md) | Planned | Owns the task model, the `Tasks` permission set and the transition table. Includes reopen, the "someone else moves it out of Review" rule, `CompletedAt`, and the allowed-move list the drag-and-drop board needs |
| 2 | **F04 — Task audit & history trail** | [f04-audit.md](features/f04-audit.md) | Planned | Build with F01, not after it: F01, F02 and F05 all write entries, so retrofitting the trail means reopening all three |
| 3 | **F02 — Resource capacity guard** | [f02-capacity.md](features/f02-capacity.md) | Planned | A rule on F01's move and assign operations. Needs the concurrency token, not just a `Count()` |
| 4 | **F03 — Priority-based auto-sorting** | [f03-sorting.md](features/f03-sorting.md) | Planned | The board read: grouped by status, each column paginated, `Done` ordered by `CompletedAt` |
| 5 | **F05 — Deadline & overdue management** | [f05-overdue.md](features/f05-overdue.md) | Planned | The rule plus the 00:05 Vietnam-time Quartz scan; last because it depends on F01's statuses and F04's `System` entries |

Supporting work, in scope but not a BRS feature: task CRUD (create, edit, assign, read, delete), the `Tasks` permission group, and the small in-app notification store F01 needs for the rework event.

## Order and parallelism

1. **Agree the API contract for a unit before either side starts it.** API and client are built in parallel, so route, verb, request fields, response shape, error codes and auth policy are written down first — see the constraint in `CLAUDE.md`.
2. F01 + F04 together as one unit, then F02, then F03, then F05.
3. The client can start against the agreed contract while F01 is in progress; it does not wait for the API to be finished.

## Decisions already locked (2026-10-20)

All 26 open questions raised during planning are answered; the specs state them as rules under each file's **Decisions** heading. The ones that cut across features:

- One shared board — no team entity, no row-level filtering; every authenticated user sees every task.
- Members may view, create, update, move and assign **any** task; delete and reopen are Manager / Admin only.
- Leaving `Review` must be done by someone other than the current assignee. On a one-person board a task therefore stays in `Review` — accepted, and written down as a rule.
- `DueDate` is a `DateOnly`; "today" is resolved in `Asia/Ho_Chi_Minh` from configuration; timestamps stay UTC.
- Overdue covers every task that is not `Done`, `Backlog` included.
- The board is drag-and-drop: dropping a card changes status only, and the drop position carries no meaning.

## Done outside the feature units

- **CORS allow-list** (2026-10-20) — `Cors:AllowedOrigins` per environment, startup fails when none is configured. Replaced `SetIsOriginAllowed(_ => true)` combined with `AllowCredentials()`.

## Found in review (2026-10-25), not yet scheduled

Found by reading the scaffold against the specs; nothing below has been changed in code. Each is a small unit (S or Bug size).

**Before the F01 contract is built** — each one breaks an F01 acceptance criterion or the client's error handling:

| # | Finding | Where | Why it matters |
|---|---|---|---|
| R1 | Model-validation failures return ASP.NET's default ProblemDetails, not the `ExecutionRes` envelope | no `InvalidModelStateResponseFactory` configured | F01 AC15 / AC22 shape errors reach the client without `success`, `errorCode` or `validates`; `FE/src/api/client.ts` cannot show field errors |
| R2 | `Manager` and `Member` roles are not seeded, and `RoleClaimConfiguration` seeds claims for `Admin` only | `RoleConfiguration`, `RoleClaimConfiguration` | Permissions are read from `RoleClaim` rows at login, so the F01 permission table cannot hold until both roles and their claims are seeded by migration |
| R3 | Refreshing a token drops every permission: the new access token is built from `CreateUserPrincipalAsync` claims without the `RolePolicy` claims login adds | `AuthService.RefreshToken` | After the first refresh (15 min) every policy-protected endpoint returns 403. Fix together with the planned httpOnly-cookie change. Same method: the `UserToken` row is found by `DeviceGuid` alone, not by user |
| R4 | A commit that only removes an `IIsDeletedEntity` is a **hard** delete: `SaveChangesInternal` returns early when nothing is Added/Modified, before the soft-delete conversion runs | `UnitOfWork.SaveChangesInternal` | F02 D6 / F04 D6 delete paths are masked only because they add a history row in the same commit |

**Before F05**

| # | Finding | Where |
|---|---|---|
| R5 | `ExampleHostedService` schedules the infinite-loop `ExampleJob` on every start, holding a Quartz worker permanently | `HostedServices/ExampleHostedService.cs` — remove with the demo code |
| R6 | Verify that `TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")` resolves on the Windows dev machine: `TaskFlow.Api` runs with `InvariantGlobalization=true`, and IANA-id lookup on Windows depends on ICU. Linux containers read tzdata | `TaskFlow.Api.csproj` |

**Any time**

- R7 `Logger.LogError("… {ex}", ex)` in `AuthController` and `ExampleController` → `Logger.LogError(ex, "…")`.
- R8 `/healthz` registers no checks — add a database check when `/ship setup` defines the deployment.
- R9 Dev compose pins MinIO, Redis, Loki, Grafana and Prometheus to `latest` and defines no health checks.
- Decide the open questions in `CLAUDE.md`: **Q-T** tests (several F01/F02/F05 criteria need a real MariaDB to verify) and **Q-U** how Member and Manager users exist.

## Known follow-ups, not yet scheduled

- The refresh-token endpoint still takes and returns tokens in the body; it must set and read an `httpOnly` cookie instead, and the client calls it silently at startup.
- `dotnet format --verify-no-changes` fails on the pre-existing `ApiVerioningConfiguration.cs` (analyzer AV0013).
- `Pagination<T>` executes its query in its constructor, and `ExampleJob` runs an infinite loop — both are scaffold demos that the real code must not copy.
