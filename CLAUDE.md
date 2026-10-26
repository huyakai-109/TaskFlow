# Project Context

> Facts about TaskFlow, checked against the repository on 2026-10-25. An unknown stays `TBD`. Where the code contradicts this file, the code wins: say so and propose a fix.
> How work is done — sizing, gates, approvals, verification, status — is in `.claude/rules/workflow.md`, loaded every session.

| Field | Value |
|---|---|
| **Project** | **Kanban TaskFlow** (`BE/TaskFlow.sln` · `FE/`): a team task board whose lifecycle, workload limit, ordering, audit trail and deadlines are enforced by the server, not by team habit. ASP.NET Core API + React client, built in parallel. |
| **Problem** | Teams on a Kanban board skip stages, silently overload people, lose urgent work down the list, and cannot prove who changed what (`docs/brs/taskflow-brs.md`). |
| **Users and roles** | Scaffold roles `Admin` · `Manager` · `Member`. Members view, create, edit, move and assign **any** task; only Manager/Admin delete and reopen (F01 permission table). The BRS "reviewer" is not a role: anyone **other than the current assignee** may move a task out of `Review` (F01 D5). |
| **Primary goal** | The five BRS rules enforced through a versioned API and shown on a drag-and-drop board. |
| **In scope (MVP)** | F01 workflow state machine · F02 capacity guard (max 3 `InProgress` per assignee, configurable) · F03 priority auto-sorting · F04 append-only history · F05 overdue flag + daily scan. Plus the primitives they need: create, edit title/description, set priority and due date, assign, read, list, delete (Manager/Admin), reopen `Done → Review` with a reason, one in-app notification (task sent back for rework). Client: board with drag-and-drop, task create/edit, overdue indicator, history view, that notification. |
| **Out of scope** | Email or any outbound channel · configurable workflows or priorities · teams, membership, row-level visibility (one shared board, everyone sees every task) · comments, attachments, labels, subtasks, estimates, sprints · dashboards, exports · real-time push · manual card order (order is computed, never stored) · bulk operations. |

**Requirements and status.** Behaviour: `docs/brs/taskflow-brs.md`. Acceptance criteria: `docs/features/f01…f05` — **decided** on 2026-10-20; they state rules, not assumptions, and are not re-planned. Status and order: `docs/ROADMAP.md` (F01 + F04 together → F02 → F03 → F05).

## Technical context

| | |
|---|---|
| **Project type** | Full-stack: API + React SPA, developed in parallel. The API contract is the only coupling. |
| **Backend** | ASP.NET Core controllers on `net10.0` (no `global.json` pin). Asp.Versioning 10.2.1 · AutoMapper 16.2.0 (licence key read from `LuckyPennyLicense`) · Swashbuckle 10.3.1 · Serilog 10.0.0 · Quartz 3.22.7 (clustered ADO job store) · Minio 7.0.0 · ASP.NET Identity + JWT bearer. `TaskFlow.Api` sets `InvariantGlobalization=true`. |
| **Frontend** | `FE/` — React ^19.3.1, TypeScript ~6.0.3 strict, Vite ^8.3.4, TanStack Query ^5.104.5 (server state), React Router ^7.18.4. Lint: oxlint. One API client (`FE/src/api/client.ts`) unwraps the envelope into `ApiError` with `errorCode`; the access token lives in memory only. |
| **UI conventions** | CSS Modules over design tokens in `FE/src/styles/tokens.css` (light + dark; priority and overdue colours defined there). No component library. Widths: kit default 390 / 768 / 1440 until decided otherwise. Screens, UI rules and copy: `docs/ui.md` (wireframe snapshot in `docs/ui/screens/`). The specs win where they differ. |
| **Database** | MySQL 8.4.0 — one server (port 3306), two databases: `mydb` for `MyDbContext` (migrations in `BE/TaskFlow.DataAccess/Migrations/`) and `quartz` for `QuartzContext` (Quartz tables only, `BE/TaskFlow.DataAccess/Quartz-Migrations/`). EF Core 9.0.21 + Pomelo 9.0.0, so EF Core 10 features (named query filters) are unavailable. Redis (distributed cache), MinIO (objects). MySQL has no filtered indexes; default collations compare case-insensitively. |
| **Architecture** | `BE/` layered, one project per layer, one-way references: `TaskFlow.Api` → `TaskFlow.BusinessLogic` → `TaskFlow.Repositories` → `TaskFlow.DataAccess` → `TaskFlow.Common` (leaf). `FE/` is a separate app. `.claude/` and `docs/` at the root serve both. |
| **Tests** | `BE/TaskFlow.Tests` (xUnit) — DB-level, run against a real MySQL database `mydb_test`, never `mydb`: the test fixture migrates and cleans it, and refuses to run against any database whose name doesn't end `_test`. Added in slice 0.2, before F01. Run: `docker compose -f BE/TaskFlow.Docker/docker-compose.yml up -d taskflow.mysql`, then `dotnet test BE/TaskFlow.Tests` (Development commands → Test). Decided 2026-10-25 (Q-T). A frontend runner (Vitest) is added later. |
| **Infrastructure** | Dev only: `BE/TaskFlow.Docker/docker-compose.yml` (API, MySQL, MinIO, Redis, Loki, Grafana, Prometheus — several images on `latest`, no health checks) and `BE/TaskFlow.Api/Dockerfile` (`aspnet:10.0`, non-root `app`). Serilog → console / file / Loki; OpenTelemetry → Prometheus `/metrics` + OTLP; `/healthz` (no checks registered). No CI. |
| **Status** | Bootstrapping. The API scaffold builds and runs; `Example*` is placeholder code; the task domain is not started. The client is scaffolded and fetches nothing — its board waits on the F01 contract. |

## How this codebase works — read before the first task

Complements `.claude/rules/backend.md`. Where this section and a kit rule differ, this section is the project's decision.

- **Request flow** — controller in `BE/TaskFlow.Api/Controllers/V1/` (inherits `BaseController`: `[Authorize]`, `Logger`, `Mapper`, `InternalServerError()`), route `api/v{version:apiVersion}/<feature>` → AutoMapper profile `BE/TaskFlow.Api/Mappers/<Feature>Mapper.cs` → service in `BE/TaskFlow.BusinessLogic/Services/` (interface + implementation in one file, registered scoped in `CoreDependencyConfiguration.AddServices`) → `IUnitOfWork.GetRepository<T>()` → `MyDbContext`. `…Req`/`…Res` in `TaskFlow.Api/Models/`, `…Dto` in `TaskFlow.BusinessLogic/Dtos/`.
- **Envelope** — `ExecutionRes { Success, ErrorCode, Error, Validates[] }` → `ResultRes<T>` → `PaginationResultRes<T>` (`PaginationRes` includes `CurrentCount`). The client branches on `errorCode`, so every business failure sets a constant code; `Error` is a human-readable message only.
- **Error codes** — no message-constant class exists yet; `AuthController` and `ExampleController` set literal `Error` strings and are **not** the precedent. The first task feature establishes `<Feature>ControllerMS` constants (`"Task.Move.InvalidTransition"` style, kit `backend.md` §5) — propose the file location in the F01 plan.
- **Exceptions** — every action body in `try`/`catch (Exception ex)` → `Logger.LogError(ex, "...")` → `InternalServerError(response)`; there is no exception middleware. The existing `LogError("… {ex}", ex)` calls are a known defect, not the pattern.
- **Unit of work** — `UnitOfWork.SaveChanges()` stamps `CreatedBy/At`, `UpdatedBy/At` from claims (user `0` when there is no `HttpContext`, e.g. in Quartz jobs), trims strings, turns empty strings into `null`, and converts `Remove` on an `IIsDeletedEntity` into `IsDeleted = true`. Never set those fields in a service.
- **Soft delete** — a soft-deletable entity needs `IIsDeletedEntity`, a `HasQueryFilter` in its configuration and registration in `MyDbContext.OnModelCreating` (`ExampleConfiguration` has no filter, so deleted examples still return). On EF Core 9, `IgnoreQueryFilters()` removes **every** filter of that entity — needed, deliberately, when F04 reads the history of a deleted task.
- **History actor** — F04 stores the actor in its own nullable column (`null` = System). Do **not** use `CreatedBy` as the actor, as kit Recipe 15 suggests: it is `0` inside the F05 job, which F04 AC22 forbids.
- **Permissions are data** — login reads the caller's `RoleClaim` rows and issues them as `RolePolicy` claims; `SecurityConfiguration` registers one policy per string in `Permissions.All`. A new permission therefore needs: the constant, `Permissions.All`, the role lists in `RolePolicies`, **and** seeded `RoleClaim` rows (`RoleClaimConfiguration.HasData`) plus a migration. Today only the `Admin` role and Admin's claims are seeded — `Manager` (Id 3) and `Member` (Id 4) have no rows. Seed ids are positional: append, never insert in the middle.
- **Users** — only `admin` is seeded and no endpoint creates users (the `Members` permissions have no controller). See open questions.
- **Composition** — `CoreDependencyConfiguration.AddCoreDependencies`; each infrastructure concern has a `*Injection` extension beside it. `IScheduleService` and the Quartz scheduler factory are singletons: jobs resolve scoped services inside a created scope.
- **Time** — UTC through `DateTimeHelper.GetDtOffset()` (no `TimeProvider` registered). `DueDate` is `DateOnly`; "today" for F05 is computed in the zone read from configuration (`Asia/Ho_Chi_Minh`).
- **CORS** — allow-list from `Cors:AllowedOrigins` (`ConfigKeys.Cors.AllowedOrigins`); startup fails without one. Vite's `http://localhost:5173` is allowed in `appsettings.Development.json`. Never reintroduce any-origin with credentials.
- **Scaffold code that is not a pattern** — `Example*` (sync `FirstOrDefault`, DTO→entity mapping, literal errors); the demo actions `weather-forecast`, `cache/test`, `minio/upload` (two `[AllowAnonymous]`); `Pagination<T>` running its query synchronously in the constructor (new code: async paging, kit Recipe 1); `QueryHelper.ApplySorting` throwing on unknown columns (F03 takes no client sort column); `ExampleHostedService` scheduling the infinite-loop `ExampleJob` on every start. Open defects: `docs/ROADMAP.md` → *Found in review*.
- **Nullable** — enabled in every project except `TaskFlow.DataAccess` (`disable`). Do not flip it as a side effect.

## Development commands

| Purpose | Command |
|---|---|
| Build | `dotnet build BE/TaskFlow.sln -c Debug` |
| Run API | `dotnet run --project BE/TaskFlow.Api` → http://localhost:5198, Swagger at `/swagger`. **`AutoMigration` is `true`: running the API applies pending migrations to both databases** — ask before running it with a new migration (`workflow.md` §3) |
| Run dependencies | `docker compose -f BE/TaskFlow.Docker/docker-compose.yml up -d taskflow.mysql taskflow.redis taskflow.minio` |
| Test | Start MySQL first (`docker compose -f BE/TaskFlow.Docker/docker-compose.yml up -d taskflow.mysql`), then `dotnet test BE/TaskFlow.Tests`. Needs `mydb_test` reachable on the same MySQL server as dev — the test fixture creates/migrates it automatically and refuses to run against anything not named `*_test` |
| Lint / format | `dotnet format BE/TaskFlow.sln --verify-no-changes` — currently fails on pre-existing `ApiVerioningConfiguration.cs` (AV0013); severities in `BE/.editorconfig` |
| Add migration | `dotnet ef migrations add <Name> --project BE/TaskFlow.DataAccess --startup-project BE/TaskFlow.Api --context MyDbContext` |
| Quartz migration | same, with `--context QuartzContext --output-dir Quartz-Migrations` |
| Migration SQL | `dotnet ef migrations script --idempotent --project BE/TaskFlow.DataAccess --startup-project BE/TaskFlow.Api --context MyDbContext` |
| Frontend | `npm --prefix FE run dev` (http://localhost:5173, `strictPort` because the API allow-lists that origin) · `build` · `typecheck` · `lint` (oxlint) · test `n/a` |

Package Manager Console equivalents: `BE/TaskFlow.DataAccess/README.md`.

## Deployment

One target. Written by `/ship setup` after approval; read by `/ship`. **Names and commands only — never secret values.**

| | |
|---|---|
| **Mode** | TBD — run `/ship setup` |
| **Target** | TBD |
| **Deploy command** | TBD |
| **Migration step** | TBD — today migrations run at startup (`AutoMigration=true`) |
| **Backup / restore** | TBD — MySQL: `mydb` (`MyDbContext`) and `quartz` (Quartz) |
| **Health check** | TBD — `/healthz` exists but is liveness only (no database or dependency check) |
| **Rollback command** | TBD |
| **Smoke checks** | TBD |

## Project-specific constraints

- `BE/TaskFlow.Api/appsettings.json` contains committed **development** secrets (JWT secrets, MinIO keys, database passwords, AutoMapper licence). Add no more; never treat them as valid for a shared or deployed environment.
- Two databases, two contexts, two migration histories: always pass `--context`; never generate a change against the wrong one.
- `DueDate` is `DateOnly`; "today" comes from the configured zone, never the server clock; stored timestamps are UTC. The `InProgress` limit (default 3) is configuration read through a `ConfigKeys` constant — never a literal.
- Contract first: route, verb, request and response fields, error codes and permission for a unit are written down (in its `docs/features/` spec) before either side builds it. A change to an agreed contract is announced, never silent.
- One shared board: no team entity and no row-level filtering — kit `backend.md` §9's row-visibility rule is satisfied by design. Card order is always computed (F03); no position is stored.
- The single notification is in-app (F01 D7). No email, no generic event bus.

## Decided before F01 (2026-10-25)

- **Q-T Tests** — yes: backend xUnit against a real MySQL, from slice 0. Frontend runner (Vitest) later.
- **Q-U Users** — roles `Manager`/`Member` and their `RoleClaim` rows are seeded by migration. Users `member1`, `member2`, `manager1` are seeded **only in the Development environment**, never by migration (`BE/TaskFlow.Api/Configurations/DevelopmentSeedConfiguration.cs`, run from `Program.cs` after `RunMigration()`, idempotent). `member1`/`member2` are `Member`, `manager1` is `Manager`. Their shared password is read from `DevSeed:Password` (`BE/TaskFlow.Api/appsettings.Development.json` only — never hardcoded, never logged). User management stays out of scope.
- **Drag and drop** — `@dnd-kit/core` (approved dependency).
- **CI** — GitHub Actions on every push and pull request: build + test the backend with a MySQL service, typecheck + lint + build the frontend. No deploy job; deployment is decided later with `/ship setup`.
- **Git** — one `feature/<slice>` branch per slice; agents may commit on it, never push. The user pushes and opens the pull request.
