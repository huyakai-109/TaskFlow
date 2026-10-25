# Roadmap

Behaviour comes from `docs/brs/taskflow-brs.md`; the acceptance criteria that implementation is checked against live in `docs/features/`. Status vocabulary: **Planned → In Progress → Built → Ready for Review → Done**.

## Units

| # | Unit | Spec | Status | Notes |
|---|---|---|---|---|
| 0 | **Foundation before F01** | — | In Progress | Backend test project (xUnit, real MySQL) · Development-only seed users · fix R1, R2, R3 (with the httpOnly refresh cookie), R4 · GitHub Actions CI. Broken into slices 0.1–0.5 below |
| 1 | **F01 — Strict workflow state machine** | [f01-workflow.md](features/f01-workflow.md) | Planned | Owns the task model, the `Tasks` permission set and the transition table. Includes reopen, the "someone else moves it out of Review" rule, `CompletedAt`, and the allowed-move list the drag-and-drop board needs |
| 2 | **F04 — Task audit & history trail** | [f04-audit.md](features/f04-audit.md) | Planned | Build with F01, not after it: F01, F02 and F05 all write entries, so retrofitting the trail means reopening all three |
| 3 | **F02 — Resource capacity guard** | [f02-capacity.md](features/f02-capacity.md) | Planned | A rule on F01's move and assign operations. Needs the concurrency token, not just a `Count()` |
| 4 | **F03 — Priority-based auto-sorting** | [f03-sorting.md](features/f03-sorting.md) | Planned | The board read: grouped by status, each column paginated, `Done` ordered by `CompletedAt` |
| 5 | **F05 — Deadline & overdue management** | [f05-overdue.md](features/f05-overdue.md) | Planned | The rule plus the 00:05 Vietnam-time Quartz scan; last because it depends on F01's statuses and F04's `System` entries |

Supporting work, in scope but not a BRS feature: task CRUD (create, edit, assign, read, delete), the `Tasks` permission group, and the small in-app notification store F01 needs for the rework event.

## Slices

One thin, independently verifiable path each. One `feature/<slice>` branch per slice; agents commit, never push.

### Unit 0 — Foundation

| # | Slice | Branch | Owner | Status | Covers |
|---|---|---|---|---|---|
| 0.1 | Roles & permission claims seeded | `feature/unit0-roles-seed` | backend-lead | Built — migrations not applied | R2 — `Manager`/`Member` rows in `RoleConfiguration`, their `RoleClaim` rows · R2b — a first migration aligning the drifted scaffold seed data. Two `MyDbContext` migrations |
| 0.2 | Backend test project | `feature/unit0-test-project` | backend-lead | Planned | `BE/TaskFlow.Tests` (xUnit, real MySQL), first tests proving 0.1 |
| 0.3 | Envelope + soft-delete defects | `feature/unit0-envelope-softdelete` | backend-lead | Planned | R1 `InvalidModelStateResponseFactory` · R4 `SaveChangesInternal` early return · tests for both |
| 0.4 | Auth hardening + dev users | `feature/unit0-auth-hardening` | backend-lead | Planned | R3 — `RolePolicy` claims survive refresh, httpOnly refresh cookie, `UserToken` found by user + device · Development-only seeding of `member1`, `member2`, `manager1` |
| 0.5 | CI | `feature/unit0-ci` | devops-lead | Planned | `.github/workflows/ci.yml` — build + test BE with a MySQL service, typecheck + lint + build FE. No deploy job |

### Units 1–2 — F01 + F04

Contract: `docs/features/f01-workflow.md` → *Technical contract* (shared) and `docs/features/f04-audit.md` → *Technical contract*. **Nothing below starts until the user signs off the open items marked [SIGN-OFF] there.** Riskiest assumptions first: the MySQL concurrency token actually refusing a stale or concurrent write, the time-zone lookup resolving at all (R-TZ), and one-commit atomicity of task + history + notification. All three are retired by the end of slice 1.2.

| # | Slice | Branch | Owner | Status | Closes |
|---|---|---|---|---|---|
| 1.1 | Model, permissions, create + read one | `feature/f01-model-create` | backend-lead | Planned | Entities, configurations, two migrations, enums, `TaskTransitions`, `TaskControllerMS`, `JsonStringEnumConverter`, `Pagination<T>.CreateAsync`, zone key + R-TZ fix, `POST /tasks`, `GET /tasks/{id}`. F04 AC1, AC17, AC21; F01 AC18 (Backlog), AC24, AC26 |
| 1.2 | Move + history + concurrency | `feature/f01-move` | backend-lead | Planned | The gate slice. F01 AC1–AC5, AC7–AC16, AC20, AC25, AC27–AC31; F04 AC2, AC7, AC10–AC14, AC18. Includes the two-connection AC29 test against real MySQL |
| 1.3 | Reopen + notification store | `feature/f01-reopen-notify` | backend-lead | Planned | F01 AC6, AC21–AC23, D7; F04 AC6 |
| 1.4 | Reads, edits, delete, history read | `feature/f01-reads-edits` | backend-lead | Planned | F04 AC3–AC5, AC9, AC15–AC21, AC23; F03's shape but not its order; the remaining F01 primitives |
| 1.5 | Board, read-only | `feature/f01-board` | frontend-lead | Planned | Five columns, cards, priority/due/overdue chips, drop targets derived from `allowedTransitions`, one error-code→copy map. Starts on contract approval alone |
| 1.6 | Drag-and-drop + detail | `feature/f01-dnd` | frontend-lead | Planned | `@dnd-kit/core`, refusal dialogs per error code, optimistic rollback, history timeline, reopen dialog, notification inbox |
| 1.7 | Create / edit / assign UI | `feature/f01-task-forms` | frontend-lead | Planned | New-task dialog, edit, priority, due date, assignee picker; `GET /users` if approved |

Slice 1.5 may run beside the backend slices once the contract is approved; one working tree keeps the backend slices sequential. Slice 1.2 is the gate — nothing else starts before its concurrency test passes against real MySQL.

## Order and parallelism

1. **Agree the API contract for a unit before either side starts it.** API and client are built in parallel, so route, verb, request fields, response shape, error codes and auth policy are written down first — see the constraint in `CLAUDE.md`.
2. Unit 0 first, then F01 + F04 together as one unit, then F02, then F03, then F05.
3. The client can start against the agreed contract while F01 is in progress; it does not wait for the API to be finished.
4. There is one working tree, so one slice branch is checked out at a time and build slices run in sequence. Read-only work — the architect's F01 + F04 design — runs concurrently with a build slice, because it edits no files.

## Decisions already locked (2026-10-20)

All 26 open questions raised during planning are answered; the specs state them as rules under each file's **Decisions** heading. The ones that cut across features:

- One shared board — no team entity, no row-level filtering; every authenticated user sees every task.
- Members may view, create, update, move and assign **any** task; delete and reopen are Manager / Admin only.
- Leaving `Review` must be done by someone other than the current assignee. On a one-person board a task therefore stays in `Review` — accepted, and written down as a rule.
- `DueDate` is a `DateOnly`; "today" is resolved in `Asia/Ho_Chi_Minh` from configuration; timestamps stay UTC.
- Overdue covers every task that is not `Done`, `Backlog` included.
- The board is drag-and-drop: dropping a card changes status only, and the drop position carries no meaning.

Tests, users, the drag-and-drop library, CI and the git rule were decided on 2026-10-25: `CLAUDE.md` → *Decided before F01*.

## Done outside the feature units

- **CORS allow-list** (2026-10-20) — `Cors:AllowedOrigins` per environment, startup fails when none is configured. Replaced `SetIsOriginAllowed(_ => true)` combined with `AllowCredentials()`.
- **UI reference** (2026-10-25) — `docs/ui.md` and the wireframe snapshot in `docs/ui/screens/`.
- **R10 — MySQL data directory and health check** (2026-10-25) — `taskflow.mysql` bind-mounted `./data/mysql` into the repository. On Windows that puts the InnoDB data directory on drvfs, which made first start slow enough to look like a connection failure and, more seriously, forced `lower_case_table_names=2`, so table names compared case-insensitively here and case-sensitively on the Linux filesystem CI will run against. Replaced with the named volume `taskflow-mysql-data` and given a `mysqladmin ping` health check (`start_period: 60s`). The old data directory held only system schemas and was deleted with the user's approval. This also closes the MySQL half of R9.

## Found in review (2026-10-25)

Found by reading the scaffold against the specs.

**Scheduled in unit 0** — each one breaks an F01 acceptance criterion or the client's error handling:

| # | Finding | Where | Why it matters | Slice |
|---|---|---|---|---|
| R1 | Model-validation failures return ASP.NET's default ProblemDetails, not the `ExecutionRes` envelope | no `InvalidModelStateResponseFactory` configured | F01 AC15 / AC22 shape errors reach the client without `success`, `errorCode` or `validates`; `FE/src/api/client.ts` cannot show field errors | 0.3 |
| R2 | `Manager` and `Member` roles are not seeded, and `RoleClaimConfiguration` seeds claims for `Admin` only | `RoleConfiguration`, `RoleClaimConfiguration` | Permissions are read from `RoleClaim` rows at login, so the F01 permission table cannot hold until both roles and their claims are seeded by migration | 0.1 |
| R2b | **The seed data in the database does not match the configurations** — found while building 0.1 on 2026-10-25. The committed migration seeds role 1 as `SysAdmin` / "System Admin" and `RoleClaim` 1–4 as `ViewEmployees`, `ManageEmployees`, `ViewCustomers`, `ManageCustomers`, with no row for `ManageReports`; the configurations now declare `Admin` and the five `Members`/`Reports` permissions. `Example.IsDeleted` was likewise added to the entity with no migration | `RoleConfiguration`, `RoleClaimConfiguration`, `Example`, `20240713060801_InitDatabase` | `SecurityConfiguration` registers one policy per string in `Permissions.All`, so against a freshly migrated database **no role matches any permission — Admin included**. R2 alone would not have fixed this. The missing `Examples.IsDeleted` column also means every future migration silently carries an unrelated `AddColumn` | 0.1 |
| R3 | Refreshing a token drops every permission: the new access token is built from `CreateUserPrincipalAsync` claims without the `RolePolicy` claims login adds | `AuthService.RefreshToken` | After the first refresh (15 min) every policy-protected endpoint returns 403. Fix together with the planned httpOnly-cookie change. Same method: the `UserToken` row is found by `DeviceGuid` alone, not by user | 0.4 |
| R4 | A commit that only removes an `IIsDeletedEntity` is a **hard** delete: `SaveChangesInternal` returns early when nothing is Added/Modified, before the soft-delete conversion runs | `UnitOfWork.SaveChangesInternal` | F02 D6 / F04 D6 delete paths are masked only because they add a history row in the same commit | 0.3 |

**Before F05, not yet scheduled**

| # | Finding | Where |
|---|---|---|
| R5 | `ExampleHostedService` schedules the infinite-loop `ExampleJob` on every start, holding a Quartz worker permanently | `HostedServices/ExampleHostedService.cs` — remove with the demo code |
| R6 | Verify that `TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")` resolves on the Windows dev machine: `TaskFlow.Api` runs with `InvariantGlobalization=true`, and IANA-id lookup on Windows depends on ICU. Linux containers read tzdata | `TaskFlow.Api.csproj` |

**Any time, not yet scheduled**

- R7 `Logger.LogError("… {ex}", ex)` in `AuthController` and `ExampleController` → `Logger.LogError(ex, "…")`.
- R8 `/healthz` registers no checks — add a database check when `/ship setup` defines the deployment.
- R9 Dev compose pins MinIO, Redis, Loki, Grafana and Prometheus to `latest` and defines no health checks for them. **The MySQL half is Done** (2026-10-25, below); the rest stands. `taskflow.api`'s `depends_on` still waits only for the container to start, not for MySQL to be healthy — `condition: service_healthy` is the follow-up.

## Known follow-ups, not yet scheduled

- The refresh-token endpoint still takes and returns tokens in the body; slice 0.4 replaces that with an `httpOnly` cookie, and the client calls it silently at startup.
- `dotnet format --verify-no-changes` fails on the pre-existing `ApiVerioningConfiguration.cs` (analyzer AV0013).
- `Pagination<T>` executes its query in its constructor, and `ExampleJob` runs an infinite loop — both are scaffold demos that the real code must not copy.
