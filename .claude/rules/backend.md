---
paths:
  # TaskFlow: the .NET solution under BE/.
  - "**/*.cs"
  - "**/*.csproj"
  - "**/*.sln"
  - "**/*.slnx"
  - "**/appsettings*.json"
---

# Backend Rules

Server-side code. The **bold principle** of each rule holds on any stack; the detail is the house style of a layered ASP.NET Core + EF Core Web API — keep it wherever a project already uses it, and propose it for new .NET projects. **Default target: `net10.0` (LTS — C# 14, EF Core 10)** for new projects; an existing project builds on its own version until it is upgraded as a separate task (procedure and blockers: `dotnet-versions.md`). Schema and migrations: `database.md`. Recipes with full code: `.claude/references/dotnet-patterns.md`. Version-dependent features: `.claude/references/dotnet-versions.md`.

## 0. What kind of rule is this

| Kind | Examples | Treatment |
|---|---|---|
| **House style** — presentation and structure preferences | Layer and suffix naming; block-scoped namespaces, 4 spaces, braces on their own lines and on every `if`; guard clauses separated by a blank line; envelope-first controller actions; per-action `try`/`catch`; error-code string protocol; interface above implementation in one file; explicit entity assignment | Keep exactly in projects that use it. In a project with a different working style, follow that project. |
| **Engineering rules** — correctness, security, performance | Validate before mutating; create/update symmetry; one commit point; authorization per action; parameterised SQL; async all the way; exception logged as exception | Apply everywhere. |
| **Project mechanisms** — exist only in some codebases | `IUnitOfWork.GetRepository<T>()`, `TrimData()`, `ResultRes<T>`, `PermissionConstants.All`, AutoMapper profiles, `ApplySorting` | Use when present. Never create them just because this file names them; use the project's equivalent. |

## 1. Layers and boundaries

**[M] Identify the layers before writing code, and keep dependencies pointing one way.** House layering:

```
Presentation (Web API) → Application (BusinessLogic) → Persistence (Repositories) → Data (DataAccess)
                       all layers may reference a leaf Common library (constants, enums, helpers, message codes)
```

| Concern | Layer |
|---|---|
| Routing, status codes, model binding, auth attributes, `…Req`/`…Res` | Presentation |
| Business rules, orchestration, business validation, `…ReqDto`/`…ResDto` | Application |
| Query composition, change tracking, commit | Persistence |
| Entities, entity configuration, migrations | Data |

- **[M]** No type crosses a boundary the wrong way: no entity in a controller signature or response; no `…Req`/`…Res` in a service signature; no `HttpContext`, `ClaimsPrincipal` or `IFormFile` logic in a service.
- **[M]** One controller and one service per feature. A controller depends on its own service only. Logic needed by three services goes into a shared helper, not a chain of service calls.
- **[M]** Keep the transport model and the DTO separate even when identical; map between them in one declared place (an AutoMapper `Profile` per feature in the house style).
- **[R]** A change that needs a currently closed boundary is a design question — stop and raise it (`architect`).

## 2. Naming and layout (house style)

| Suffix | Meaning |
|---|---|
| `…Controller` | HTTP endpoint group |
| `…Service` / `I…Service` | Application service — interface directly above the implementation, same file |
| `…Req` / `…Res` | Transport models |
| `…ReqDto` / `…ResDto` | Application DTOs |
| `…Configuration` | EF entity configuration or DI extension |
| `…ControllerMS` | Error/message code constants for a feature |
| `…Helper` | Static pure helpers |

- **[M]** `PascalCase` types, methods, properties, constants; `camelCase` parameters and locals; `_camelCase` private fields. Singular feature folders.
- **[M]** Block-scoped `namespace X { }`, 4-space indent, braces on their own line, braces on every `if` even for one statement, one blank line between consecutive guard blocks. One public type per file, except the service interface + implementation pair.
- **[R]** Member order: constants, fields, constructor, public, private. Inject in the order `ILogger<T>`, `IMapper`, then feature dependencies.
- **[C]** Primary constructors for controllers and services when the project's C# is 12 or later (.NET 8+). On older language versions use constructor injection with `_camelCase` fields.

## 3. Controllers — thin transport

**[M] An action binds, maps, calls one service method, shapes the response, and returns.** No business rules, queries or data access.

- **[M]** `public async Task<IActionResult>`, an explicit verb attribute, `[ProducesResponseType]` for the success shape, and an `[Authorize(Policy = …)]` matching the operation's sensitivity (view / create / update / delete / approve).
- **[M]** Create the response envelope first, then fill it: business failure → set `ErrorCode`, `return BadRequest(response)`; success → `Success = true`, `return Ok(response)`. Keep the project's status-code convention consistent; do not mix in 404/409 where the project uses 400 + error code.
- **[M]** Normalise input on write actions before mapping: `request = (TReq)request.TrimData();`
- **[M]** A route id is bound `[FromRoute]` and assigned onto the DTO after mapping — never accepted in the body.
- **[M]** The caller's identity is read here (`this.User.Claims.GetUserId()`) and passed inward as a plain value.
- **[M]** House style: every action body sits in `try`/`catch (Exception ex)`; the catch sets the operation's `Exception` code, logs, and returns `InternalServerError(response)`. Log the exception as the exception argument:

  ```csharp
  catch (Exception ex)
  {
      response.ErrorCode = ProductControllerMS.Create.Exception;
      Logger.LogError(ex, "Create product failed");
      return InternalServerError(response);
  }
  ```

  A project that uses exception-handling middleware instead keeps its middleware; never mix the two styles.
- **[M]** An action forwarding a user-supplied sort column handles an invalid column as a 400 with its own error code. **[R]** In new code, map sort keys through an allow-list of columns instead of reflection.

## 4. Application services

- **[M]** The interface lists only what callers use.
- **[M]** One declared result protocol. House style:

  | Operation | Signature | Success |
  |---|---|---|
  | Command | `Task<string>` | `string.Empty` |
  | Single query | `Task<(string, TResDto?)>` | `(string.Empty, dto)` |
  | Paged query | `Task<Pagination<TResDto>>` | — |

- **[M]** Business failures are returned as error codes, not thrown. Exceptions are for the unexpected.
- **[M]** Validate everything before mutating anything: existence → uniqueness → references → state, each guard returning its own code.
- **[M]** Mirror every create-path rule on the update path, and exclude the current row from uniqueness checks (`&& i.Id != requestDto.Id`). This is the most frequent defect in this style.
- **[M]** Build entities by explicit field assignment from the DTO. Never map a DTO onto an entity — a field added later would be written without review.
- **[M]** A status with rules is a state machine: allowed transitions declared once as data, every other move rejected with its own code, terminal states closed, reopening a separate permissioned operation (Recipe 13).
- **[M]** A rule that counts other rows (at most N active, only one default, a running balance) is a race. Guard the owning row with a concurrency token bumped in the same commit, or enforce it in the database (Recipe 14, `database.md`).
- **[M]** `await` every asynchronous call. No `.Result`, `.Wait()`, `async void`, `Task.FromResult` wrappers around synchronous work, or queries executed in constructors.
- **[R]** Accept a `CancellationToken` and pass it to EF calls in new code where the project's signatures allow; stay consistent with existing signatures otherwise.
- **[R]** Keep a method under ~80 lines; extract private helpers for long guard chains or row-mapping loops.
- **[C]** History the business must see is append-only, written in the same commit as the change (Recipe 15).

## 5. Error codes

- **[M]** Every user-visible failure has a constant in the feature's message class, formatted `"Feature.Operation.Reason"`, grouped by operation; each group has an `Exception` member.
- **[M]** Changing an existing code is a breaking change for clients — say so.

## 6. Data access from services

- **[M]** Reach data only through the project's persistence abstraction (`IUnitOfWork` / repositories in the house style). No `DbContext`, raw SQL or connection handling in a controller.
- **[M]** One commit point per operation: repositories stage, one `SaveChanges()` persists. An explicit transaction only for a multi-aggregate write or a read that determines a written value — inside the provider's execution strategy, with every read and write inside the retried block (Recipe 8).
- **[M]** Reads are no-tracking unless you are about to modify. Project into DTOs; never return entity graphs from the service layer.
- **[M]** Compose `IQueryable` as project → filter → search → sort → paginate. Materialise asynchronously; a paging helper must count and fetch with async calls (Recipe 1).
- **[M]** Keep filters sargable: compare the raw column with precomputed bounds (`i.CreatedAt >= fromUtc`), not a function of the column (`i.CreatedAt.Date >= …`). Convert date-only input to UTC bounds using the business time zone.
- **[M]** Parameterise all SQL. Never interpolate user input into a query string (`FromSqlInterpolated` / parameters, not string concatenation).
- **[M]** Never set audit or soft-delete fields by hand; the persistence layer stamps them. Check every referencing table before deleting — soft delete does not cascade.
- **[R]** No N+1: join or project in one query. Keep keyword search to a small, deliberate column list; `ToLower().Contains()` is acceptable only on small tables.

## 7. Validation

- **[M]** Shape rules as attributes on the `…Req` model, enforced by model validation. Business rules in the service, returning codes.
- **[M]** Required → non-nullable with `[Required]`, initialised (`public string Name { get; set; } = string.Empty;`); optional → nullable without `[Required]`. Avoid the C# `required` modifier on request models: a missing JSON property then fails deserialisation of the whole body, and the client gets no field-level error.
- **[M]** Model-validation failures reach the client in the project's envelope with field errors (one `InvalidModelStateResponseFactory` or filter, configured once). Follow the existing setup; do not hand-check shape rules inside actions.
- **[M]** `[MaxLength]` on a request never exceeds the entity column length.

## 8. API contracts

- **[M]** Routes follow the project's scheme — house style `api/v{version:apiVersion}/<kebab-case-plural>`, verbs for lifecycle actions (`PUT {id}/approve`).
- **[M]** All responses use the project's envelope (`ExecutionRes` → `ResultRes<T>` → `PaginationResultRes<T>`); binary downloads return `FileContentResult` directly.
- **[M]** A changed field name, type, enum value, error code or envelope shape is a breaking change: state it explicitly. Write the contract down before frontend and backend work proceed in parallel.

## 9. Security

- **[M]** Authenticated by default. Each `[AllowAnonymous]` is a stated decision.
- **[M]** A new permission is declared **and** registered in the permission registry (`PermissionConstants.All`) — an unregistered policy rejects every request.
- **[M]** Row-level visibility is filtered in the service query, on list endpoints as well as detail endpoints.
- **[M]** CORS allows the real origins only; never any-origin together with credentials.
- **[M]** Uploaded files: validate extension, size and row/record count at the boundary; parse inside the service with failures returned as codes; never trust the client's content type for files you parse or serve back.

## 10. Composition, configuration, time

- **[M]** Register services in the existing composition root with their peers' lifetime — scoped for anything touching the unit of work. A singleton never captures a scoped dependency; jobs and hosted services create a scope.
- **[M]** Configuration keys are constants (`ConfigKeys`); required settings fail fast at startup; a new setting has a safe default or is reported as a deployment requirement (`devops.md`).
- **[M]** All projects in a solution target the same framework. New solutions enable `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` in every project, ideally via `Directory.Build.props`.
- **[M]** Stored and compared times are UTC. Read the clock through `TimeProvider` (.NET 8+) or the project's existing time helper — never `DateTime.Now`, never a hardcoded offset.

## 11. Known defects — never reproduce

| Defect | Instead |
|---|---|
| Secret or key literal in source | Configuration key + secret store |
| Password "generator" returning a fixed value | CSPRNG (`RandomNumberGenerator`) |
| Disposing the unit of work inside a service | Let the DI scope dispose it |
| `Mapper.Map<Entity>(dto)` | Explicit field assignment |
| Uniqueness checked on create only | Same rule on update, excluding the current row |
| Delete without dependency checks | `Any(...)` on every referencing table first |
| `LogError("… {ex}", ex)` — exception as a template value | `LogError(ex, "…")` |
| Catching exceptions inside an execution-strategy block | Let them propagate so the strategy can retry; handle outside |
| Query executed in a constructor or synchronously in a helper | Build the query; materialise it with async calls |
| Function on a filtered column (`.Date`, `LOWER()`) on large tables | Precomputed bounds; indexed, prefix-friendly comparisons |
| Permissive CORS with credentials | Allow-list origins |
