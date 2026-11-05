# Slice 3.3 — `GET /users`

Branch: `feature/f02-move-capacity` (shared with 3.1/3.2/3.4, user decision 2026-11-05 — no new branch, no rebase, commits only added on top).

## Plan (written before building, per the batch-mode instruction — build proceeded without a separate approval round)

**Goal restated:** add `GET api/v1/users` returning every non-deleted user's `{ id, userName, role }`, ordered by `userName`, behind the `ViewTasks` policy — closing `docs/ROADMAP.md`'s *Technical contract sign-off* item 6 (approved 2026-10-25, never built; stopgap in place since slice 1.7's `useKnownAssignees`).

**Contract (unchanged from `docs/features/f02-capacity.md` T4):** `GET api/v1/users` → `ResultRes<List<UserRes>>`, `{ id, userName, role }`, policy `ViewTasks`, soft-deleted excluded, ordered by `userName`. No new error code beyond `User.GetList.Exception` (the one real failure path, same pattern as `TaskHistoryControllerMS`/`NotificationControllerMS`).

**Files expected to change:** a new `UserControllerMS` (`BE/TaskFlow.Common/Constants/Messages/`), a new `IUserService`/`UserService` (`BE/TaskFlow.BusinessLogic/Services/`), a new `UserResDto` (`BE/TaskFlow.BusinessLogic/Dtos/Users/`), a new `UserRes` (`BE/TaskFlow.Api/Models/Responses/Users/`), a new `UserMapper` AutoMapper profile, a new `UsersController`, one line in `CoreDependencyConfiguration.AddServices`, and one new HTTP test file. No migration — `Users`, `UserRoles`, `Roles` all already exist; this is a read-only endpoint on data already modeled.

**Role resolution:** followed `AuthService.GetRolePolicyPermissions`'s precedent of joining `Users`/`UserRoles`/`Roles` through `IUnitOfWork.GetRepository<T>()` rather than inventing a new mechanism. Every user seeded today (`admin` via migration, `member1`/`member2`/`manager1` via `DevelopmentSeedConfiguration`) carries exactly one `UserRole` row — confirmed by reading the seed code before building — so this is a plain one-role-per-user join, not the "user has multiple roles" case the brief said to stop and ask about. Implemented as a left join (defensive: a user with zero or more than one role still returns a deterministic single string — lowest `RoleId` — instead of throwing or duplicating a row) rather than an inner join that would assume the 1:1 invariant always holds.

**Validation order / permission:** `[Authorize(Policy = Permissions.Tasks.ViewTasks)]`, same policy as `TaskHistoryController`/`NotificationsController` — reused, no new permission declared.

**Test approach:** one new `BE/TaskFlow.Tests/Http/UsersControllerTests.cs`, modelled on `TaskHistoryControllerTests.cs`/`NotificationsControllerTests.cs` (same fixture/collection pattern, same `CreateAuthenticatedClientAsync` shape). Three tests: seeded users list with correct role + userName ordering, a soft-deleted user excluded, 401 anonymous — matching the ROADMAP line's stated coverage exactly.

## Changes

- `BE/TaskFlow.Common/Constants/Messages/UserControllerMS.cs` (new): `GetList.Exception` only — the one real failure path (unexpected exception), same shape as `TaskHistoryControllerMS`.
- `BE/TaskFlow.BusinessLogic/Dtos/Users/UserResDto.cs` (new): `{ Id, UserName, Role }`.
- `BE/TaskFlow.BusinessLogic/Services/UserService.cs` (new): `IUserService.GetList()` → `Task<List<UserResDto>>`. Left-joins `Users` (query-filtered `!IsDeleted`, no `IgnoreQueryFilters()` — deliberately, unlike `TaskHistoryService`'s actor lookups which need the deleted-user name) → `UserRoles` → `Roles` through three `IUnitOfWork.GetRepository<T>().QueryAll()` calls, materializes, groups by user picking the lowest `RoleId` per user (handles a hypothetical multi-role user deterministically without being the ROADMAP's "multiple roles" open question — today's data never produces one), orders by `UserName` with `StringComparer.Ordinal` after materializing (MySQL's default collation is case-insensitive per `CLAUDE.md`; the explicit ordinal comparer makes the order deterministic regardless, matching the contract's "ordered by userName" without relying on collation behavior).
- `BE/TaskFlow.Api/Models/Responses/Users/UserRes.cs` (new): the wire shape, `{ Id, UserName, Role }` — matches T4 exactly.
- `BE/TaskFlow.Api/Mappers/UserMapper.cs` (new): `CreateMap<UserResDto, UserRes>()`, no custom members needed (property names already match).
- `BE/TaskFlow.Api/Controllers/V1/UsersController.cs` (new): `GET api/v1/users`, `[Authorize(Policy = Permissions.Tasks.ViewTasks)]`, `ResultRes<List<UserRes>>`, the project's standard try/catch → `UserControllerMS.GetList.Exception` → `InternalServerError`. No business failure path besides the exception case — the service has no validation to fail (no route parameter, no body).
- `BE/TaskFlow.Api/Configurations/CoreDependencyConfiguration.cs`: one line, `collection.AddScoped<IUserService, UserService>();`, alongside its peers.
- `BE/TaskFlow.Tests/Http/UsersControllerTests.cs` (new): the three tests below.

No migration in this slice — `Users`/`UserRoles`/`Roles` and the `!IsDeleted` query filter all predate this slice; `GET /users` is a pure read.

## Database

No migration generated or needed. Confirmed before building: `UserConfiguration.HasQueryFilter(e => !e.IsDeleted)` already exists (`BE/TaskFlow.DataAccess/Configurations/UserConfiguration.cs:20`), so excluding soft-deleted users needs no new filter — only avoiding `IgnoreQueryFilters()` in the new service.

## Verification

```
Build:   PASS   dotnet build BE/TaskFlow.sln -c Debug → 0 Error(s) (pre-existing warnings only: AV0013, CS8618
                 in Pagination<T>, CS8602 in pre-existing and this slice's test files)
Tests:   PASS   dotnet test BE/TaskFlow.Tests --filter "FullyQualifiedName~UsersControllerTests", run 2x
                 consecutively against real MySQL (mydb_test, via DatabaseFixture)
                 run 1: 3 passed, 0 failed, 0 skipped (10s)
                 run 2: 3 passed, 0 failed, 0 skipped (9s)
                 No flake. This endpoint has no concurrency surface (plain read), so the AC18-style
                 3x-consecutive bar does not apply here - 2 consecutive clean runs is proportionate.
         PASS   dotnet test BE/TaskFlow.Tests (full suite, no filter), 1 run
                 133 passed, 0 failed, 0 skipped (2m 57s) - up from 130 in slice 3.2 (3 new tests)
Checked: diff reviewed file-by-file before committing (git status / git diff --stat - only the files listed
         above changed); confirmed UserConfiguration's existing query filter before relying on it; confirmed
         every seeded user (admin, member1, member2, manager1) carries exactly one UserRole row by reading
         DevelopmentSeedConfiguration.cs and the Admin migration seed, so the one-role-per-user join is not
         the "user has multiple roles" case the brief said to stop and ask about.
Not covered: CI has not run this branch. No independent qa-lead review yet (scheduled by the orchestrator at
         gate B, after 3.1-3.3, per the batch-mode instruction). No manual/browser exercise beyond the xUnit
         HTTP-level test suite (same level slices 1.2-3.2 were verified at). The admin seed user and dev-only
         seed users (member1/member2/manager1) were not used directly in the new tests - each test creates
         its own throwaway users (as TaskHistoryControllerTests/NotificationsControllerTests do), so "lists
         seeded users with role" is exercised against freshly created users with real Role rows, not against
         the specific `admin`/`member1` seed rows named in the ROADMAP line's wording.
```

### AC checklist (ROADMAP slice 3.3 line: "lists seeded users with role, excludes a soft-deleted user, 401 anonymous")

| Requirement | Status | Evidence |
|---|---|---|
| Lists users with correct role | PASS | `GetList_ListsSeededUsers_WithTheirRole_OrderedByUserName` |
| Ordered by `userName` | PASS | same test — asserts a manager named to sort first appears before a member named to sort last |
| Excludes a soft-deleted user | PASS | `GetList_ExcludesASoftDeletedUser` — soft-deletes through `IUnitOfWork`/`BaseRepository<User>.Delete` + `SaveChanges()` (never sets `IsDeleted` by hand, per `backend.md` §6/§11) |
| 401 anonymous | PASS | `GetList_Returns401_WhenAnonymous` |

## Invariants held

- No push, no merge, no commit to `main`.
- No rebase, no history rewrite on `feature/f02-move-capacity` — commit added on top of 3.1/3.2's commits.
- Tests ran only against `mydb_test` (via `DatabaseFixture`); `mydb` untouched.
- No migration generated or needed in this slice.
- No `EnableRetryOnFailure`, no new dependency, no new permission (reused `ViewTasks`).
- No secret, password or token logged or committed.
- No test or validation weakened to pass.
- ROADMAP status set to **Built**, not Ready for Review or Done — those are the orchestrator's/user's calls per the batch-mode instruction.

## Risks / open items

- The role-resolution join assumes (and today's data confirms) exactly one `Role` per `User`. If a future slice ever assigns a user more than one role, this endpoint silently picks the lowest `RoleId` rather than erroring — flagged here rather than guarded against, since building multi-role support is out of scope and no code path produces it today.
- No pagination on this endpoint — matches the approved contract (`List<UserRes>`, not `PaginationResultRes`), acceptable at the current seeded-user scale; revisit only if user management (out of scope per `CLAUDE.md`) is ever added.

Status: **Done** — accepted by the user 2026-11-05, after review-change and independent `qa-lead` verification (gate B, below).

## Review

`Review: APPROVE WITH RECOMMENDATIONS on 1618f33, 2026-11-05`

Re-verified by reading `git show 1618f33` file-by-file against `docs/features/f02-capacity.md` T4 (route, response shape, policy, exclusion, ordering) — response shape, policy and exclusion all match exactly; did not re-run the test suite (already green twice for the filtered tests + once for the full suite, same commit, clean working tree).

### Must fix

None.

### Recommendations

1. `UserService.GetList()` returns `Role = string.Empty` for a hypothetical user with zero `UserRole` rows (`UserService.cs:36`, the `first.RoleName ?? string.Empty` fallback) — a case the contract doesn't define and today's data never produces. Already flagged by the author in the report's *Risks* section; no action needed now, just worth a one-line mention in `f02-capacity.md` T4 if/when user management is added.

### Pre-existing / out of scope

- None.

Not reviewed: CI run on this branch, browser/manual exercise beyond the xUnit HTTP-level suite.

## Independent QA verification (qa-lead, gate B, 2026-11-05, covering 3.1+3.2+3.3 together)

**Verdict: READY, no Must-fix.** Route, policy (`ViewTasks`, already seeded for every role, no migration needed as claimed), response shape `{id, userName, role}` (no email or other sensitive field), soft-delete exclusion and `userName` ordering all independently confirmed against T4 by reading `UsersController.cs`/`UserService.cs` directly, with the three dedicated tests (seeded-users-with-role-ordered, excludes-soft-deleted, 401-anonymous) re-run as part of the 133/133 full-suite re-runs (2 independent runs, no flake). The one recommendation already on record (undocumented empty-`Role` fallback for a hypothetical roleless user) stands as a non-blocking observation, not a defect — no code path produces it today.

See `docs/reports/slice-3.2.md` for the same gate B pass's cross-slice checks (shared `CountActiveInProgress`, no double `ConcurrencyStamp` bump, no new N+1) and `docs/ROADMAP.md`'s Unit 3 rows for the consolidated gate B verdict across 3.1/3.2/3.3.
