# .NET Targeting and Upgrades

**Target for all new .NET work: `net10.0` (LTS, released Nov 2025, supported until Nov 2028).**

| Version | Status (verified 2026-10-25) |
|---|---|
| **.NET 10** (`net10.0`) | **LTS — use this.** C# 14, EF Core 10. |
| .NET 9 (`net9.0`) | STS. Support was extended to 24 months; **ends 10 Nov 2026**. |
| .NET 8 (`net8.0`) | LTS. **Ends 10 Nov 2026.** |
| .NET 11 | STS, expected Nov 2026. Do not move production work to an STS release by default. |

In an existing project, the project's current version is what you compile against. Never upgrade as a side effect of a feature request.

A project still on .NET 8 or 9 should be upgraded to `net10.0` now — as its own planned task (`plan-feature` → `implement-feature`, procedure in §4), checked for the blockers in §3 first, verified like any other change.

> **Reference, not rule.** The binding constraints (same framework across a solution, nullable on in new solutions, no upgrade as a side effect, write against the version the project builds with) live in `backend.md` §10 and `coding-standards.md` §3.

> **Time-sensitive.** Support dates move every November. Re-check Microsoft's support policy page before relying on this table.

---

## 1. Targeting

```xml
<TargetFramework>net10.0</TargetFramework>
<LangVersion>14.0</LangVersion>      <!-- implied by net10.0; state it only if you pin it -->
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
```

Every project in a solution targets the same version. A mixed-TFM solution is a deliberate decision, not a side effect.

Enable `<Nullable>enable</Nullable>` in **all** projects of a new solution. A data-access project with nullability disabled leaves every entity string property untyped for nullability — a common and avoidable omission.

Centralise versions in `Directory.Build.props` and `Directory.Packages.props` (central package management).

---

## 2. .NET 10 features worth using

The recipes in `dotnet-patterns.md` (beside this file) already use named query filters and `HybridCache`. These are the remaining places where .NET 10 offers something better than the older idiom.

### 2.1 API documentation — built-in OpenAPI replaces Swashbuckle

Swashbuckle was removed from the Web API project template in .NET 9. ASP.NET Core now generates OpenAPI documents itself, and emits **OpenAPI 3.1** by default in .NET 10.

```csharp
builder.Services.AddOpenApi();        // Microsoft.AspNetCore.OpenApi
// …
app.MapOpenApi();                      // serves /openapi/v1.json
```

**Choose deliberately:**
- *New project* → use built-in OpenAPI. Pair it with a UI package (Scalar, or Swagger UI standalone) if you want a browsable page — the built-in support generates the document, not the UI.
- *Existing project already on Swashbuckle* → keep it. Swashbuckle supports current .NET; migrating is not free, and consistency matters more.

Do not run both.

> Soft delete with **named query filters** and reference-data caching with **HybridCache** are shown in the recipes themselves — `dotnet-patterns.md`, Recipe 5 and Recipe 9. They are not repeated here.

### 2.2 Property with normalisation — the `field` keyword

C# 14's `field` keyword removes the explicit backing field. A search request that normalises its own keyword:

```csharp
// C# 12
private string _keyword = string.Empty;
public string Keyword
{
    get => _keyword;
    set => _keyword = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}

// C# 14
public string Keyword
{
    get;
    set => field = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
} = string.Empty;
```

Use it in new code where a property needs one piece of logic on get or set. Do not sweep through an existing codebase converting properties — that is churn, not improvement.

### 2.3 Bulk operations — `ExecuteUpdateAsync` / `ExecuteDeleteAsync`

A row-by-row import that loads each entity, mutates it in memory and saves is fine at modest volume. For large set-based updates, EF Core's bulk operations issue one statement:

```csharp
await context.Products
    .Where(i => i.CategoryId == categoryId)
    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, ProductStatus.InActive), ct);
```

Use only where the set is genuinely large. Important caveats, and they are why this is contextual, not mandatory:

- bulk operations **bypass the change tracker**, so the central audit stamping and soft-delete conversion in `UnitOfWork.SaveChanges()` **do not run**. You must set audit columns and `IsDeleted` explicitly in the `SetProperty` chain.
- they execute immediately, outside the `SaveChanges()` commit point. Inside a transaction, order them deliberately.

For ordinary single-aggregate writes, keep the tracked-entity + `SaveChanges()` pattern.

### 2.4 Extension members

C# 14 allows extension blocks, including extension properties and static extension members. Codebases of this shape accumulate helper classes full of extension methods.

```csharp
public static class QueryExtensions
{
    extension<T>(IQueryable<T> source) where T : class
    {
        public IQueryable<T> ApplySorting(string sortColumn, bool ascending) => /* … */;
    }
}
```

Reasonable for new helper files. Do not rewrite existing helpers to this syntax as part of unrelated work.

### 2.5 Time abstraction — `TimeProvider`

Mixing a UTC helper with `DateTime.Now` and hardcoded offsets is listed as a defect in `.claude/rules/backend.md` §10–§11. In a new project, inject `TimeProvider` (built in since .NET 8):

```csharp
public class ProductService(IUnitOfWork unitOfWork, TimeProvider timeProvider)
{
    private DateTimeOffset Now => timeProvider.GetUtcNow();
}
```

It fixes the inconsistency and makes time-dependent logic testable without a static helper.

---

## 3. Package upgrades

Move each package to the release that matches the target framework. Majors to expect on a `net8.0 → net10.0` move:

| Package | On .NET 8 | For .NET 10 |
|---|---|---|
| `Microsoft.EntityFrameworkCore.*` | 8.0.x | 10.0.x |
| `Microsoft.AspNetCore.*` / `Microsoft.Extensions.*` | 8.0.x | 10.0.x |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 8.0.x | 10.0.x |
| `System.IdentityModel.Tokens.Jwt` | 8.x | 8.x+ — check for breaking changes in the Microsoft.IdentityModel 8.x line |
| `Swashbuckle.AspNetCore` | 6.x | current, **or** replace with built-in OpenAPI (§2.1) |
| `Microsoft.Extensions.Caching.StackExchangeRedis` | 8.0.x | 10.0.x, **or** adopt `HybridCache` (`dotnet-patterns.md` Recipe 9) |
| OpenTelemetry packages | 1.9.x | current 1.x — these version independently of .NET |
| Serilog.AspNetCore | 8.x | current |
| Quartz | 3.13.x | current 3.x |

### Verify before committing to an upgrade

These are the items that actually block .NET upgrades on a layered Web API of this shape. **Check each against the live package feed — do not assume.**

1. **`Pomelo.EntityFrameworkCore.MySql`** — the MySQL provider historically lags EF Core by months. Confirm a stable release supporting EF Core 10 exists **before** planning the upgrade. This is the single most likely blocker. If none exists, either stay on the last supported TFM or evaluate Oracle's `MySql.EntityFrameworkCore`.
2. **`AutoMapper`** — its licensing model changed in 2025; recent majors may require a commercial licence depending on organisation size. Confirm the terms, or budget for replacing it with explicit mapping (which the kit's rules already prefer for entities).
3. **`DinkToPdf`** — unmaintained, wraps native `wkhtmltox` binaries that are themselves archived. Verify it loads on the target runtime; prefer a maintained alternative for new work.
4. **`itext7`** — AGPL unless commercially licensed. Confirm the licence position independently of the version.
5. **Native dependencies** — `libwkhtmltox`, `libgdiplus` in the Dockerfile: confirm they work on the base image for the new runtime.
6. **Docker base images** — `mcr.microsoft.com/dotnet/aspnet:8.0` → `:10.0`, and the matching `sdk:10.0`.

---

## 4. Upgrade procedure

If you are moving an existing solution rather than starting fresh:

1. **Check the blockers in §3 first.** A provider or licence problem makes everything after this pointless.
2. Upgrade one TFM at a time (`8.0 → 10.0` directly is supported; both are LTS).
3. Bump the SDK via `global.json` if the repository pins one.
4. Update `TargetFramework` in every project, then packages, then the Docker base images (`sdk:10.0` / `aspnet:10.0`) and the CI SDK version (e.g. `actions/setup-dotnet` → `10.0.x`). Infrastructure files follow `.claude/rules/devops.md`.
5. Build and fix compiler errors; read the warnings — nullable and analyser rules tightened across versions.
6. **Run the tests.** If the project has none, this upgrade is unverifiable beyond "it compiles", and you must say so.
7. Verify behaviour that the framework owns and tests rarely cover: JSON serialisation shape, model binding, date/time formatting, authentication and token validation, EF-generated SQL for the hot queries.
8. Check `dotnet list package --deprecated` and `--vulnerable`.

An upgrade is a change like any other: it needs verification evidence, and `NOT VERIFIED` where none is available. Do not report a framework upgrade as done because the build succeeded.

---

## 5. Applying this in a destination project

The destination's current version wins. Do not upgrade a project's framework as a side effect of a feature request — propose it as its own piece of work.

Write code against the language version the destination actually uses. `field`, extension blocks and collection expressions do not compile on an older `LangVersion`; check before using them.

When the destination is on .NET 8 or 9, most recipes in `dotnet-patterns.md` apply as written; the two that use a newer API (named query filters, `HybridCache`) show the older equivalent alongside.
