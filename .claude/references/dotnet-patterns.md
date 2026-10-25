# Reference Patterns

Generalised implementation recipes extracted from the reference codebase. The business domain has been removed; examples use a neutral `Product` feature.

**These patterns describe a layered ASP.NET Core + EF Core backend.** In a destination project with a different architecture, follow the project's structure — the *principles* in `.claude/rules/backend.md` carry over, the exact file layout does not.

**This file is reference, not rule.** Read only the recipe you need. Where a recipe and a rule file disagree, the rule file wins.

Every pattern here was observed across multiple implementations in that codebase, not inferred from one.

> **Framework version.** These recipes target **`net10.0`** (LTS, C# 14, EF Core 10) — the default for new projects. Almost everything also compiles on .NET 8/9; where a newer API is used (named query filters — EF Core 10, `HybridCache` — .NET 9+, collection expressions — C# 12) the recipe names the older option. .NET 8 and 9 both reach end of support on 10 Nov 2026, so plan the upgrade of older projects as separate work. `dotnet-versions.md` (beside this file) covers targeting, package upgrades and what to verify before moving an older solution.

---

## Layer map

```
Presentation (Web API)      Controllers, Req/Res models, Mappers, Filters, Configurations
        ↓
Application (BusinessLogic) Services (interface + impl), ReqDto/ResDto, Cache, Jobs, outbound clients
        ↓
Persistence (Repositories)  IUnitOfWork, IBaseRepository<T>
        ↓
Data (DataAccess)           Entities, IEntityTypeConfiguration, DbContext, Migrations
        ↑
Common (leaf)               Constants, Enums, Helpers, Message codes — referenced by all
```

A request flows: `Req` → *mapper* → `ReqDto` → *service* → *repository* → `Entity` → `ResDto` → *mapper* → `Res`.

---

## Recipe 1 — Add a paged list endpoint

**Files touched:** 6.

### 1. Request model — `Server/Models/Requests/Product/ProductSearchReq.cs`

```csharp
using System.ComponentModel.DataAnnotations;
using Server.Models.Requests.Base;

namespace Server.Models.Requests.Product
{
    public class ProductSearchReq : SearchReq
    {
        public ProductStatus? Status { get; set; }

        public DateTime? FromDate { get; set; }

        public DateTime? ToDate { get; set; }
    }
}
```

Inheriting `SearchReq` gives `Start`, `Length`, `Keyword`, `SortColumn`, `Ascending` with their defaults already applied.

### 2. DTOs — `BusinessLogic/Dtos/Product/`

```csharp
public class ProductSearchReqDto : SearchDto
{
    public ProductStatus? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public long CurrentUserId { get; set; }   // set by the controller, never read from HttpContext in the service
}

public class ProductResDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ProductStatus Status { get; set; }
    public string? CategoryName { get; set; }
    public DateTimeOffset CreationDate { get; set; }
}
```

### 3. Response model — `Server/Models/Responses/Product/ProductRes.cs`

Mirrors `ProductResDto` one-to-one. It exists so the wire contract can change without touching the application layer.

### 4. Service — `BusinessLogic/Services/ProductService.cs`

Interface and implementation live in the same file.

```csharp
public interface IProductService
{
    Task<Pagination<ProductResDto>> GetAll(ProductSearchReqDto requestDto);
}

public class ProductService(
    IUnitOfWork unitOfWork,
    IConfiguration configuration) : IProductService
{
    private readonly TimeZoneInfo businessTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
        configuration.GetSection(ConfigKeys.App.BusinessTimeZone).Get<string>() ?? "UTC");

    public async Task<Pagination<ProductResDto>> GetAll(ProductSearchReqDto requestDto)
    {
        var query = from p in await unitOfWork.GetRepository<Product>().QueryAll()
                    join c in await unitOfWork.GetRepository<Category>().QueryAll()
                        on p.CategoryId equals c.Id into lc
                    from c in lc.DefaultIfEmpty()
                    select new ProductResDto
                    {
                        Id = p.Id,
                        Code = p.Code,
                        Name = p.Name,
                        Status = p.Status,
                        CategoryName = c != null ? c.Name : null,
                        CreationDate = p.CreatedAt,
                    };

        // Row visibility, when the feature restricts rows by owner or role (backend.md §9):
        // query = query.Where(i => i.OwnerId == requestDto.CurrentUserId);

        if (requestDto.Status.HasValue)
        {
            query = query.Where(i => i.Status == requestDto.Status.Value);
        }

        if (requestDto.FromDate.HasValue)
        {
            var fromUtc = ToUtcStartOfDay(requestDto.FromDate.Value);
            query = query.Where(i => i.CreationDate >= fromUtc);
        }

        if (requestDto.ToDate.HasValue)
        {
            var toUtcExclusive = ToUtcStartOfDay(requestDto.ToDate.Value.AddDays(1));
            query = query.Where(i => i.CreationDate < toUtcExclusive);
        }

        if (!string.IsNullOrWhiteSpace(requestDto.Keyword))
        {
            var keyword = requestDto.Keyword.Trim().ToLower();
            query = query.Where(i => i.Code.ToLower().Contains(keyword)
                || i.Name.ToLower().Contains(keyword));
        }

        query = string.IsNullOrWhiteSpace(requestDto.SortColumn)
            ? query.OrderBy(i => i.Code)
            : query.ApplySorting(requestDto.SortColumn, requestDto.Ascending);

        return await Pagination<ProductResDto>.CreateAsync(query, requestDto.Start, requestDto.Length);
    }
}
```

Order matters: **project → filter → search → sort → paginate**. Sorting must be applied before paging, or the page contains the wrong rows.

**Date filters compare the raw column** with precomputed UTC bounds. `i.CreationDate.Date >= …` applies a function to the column, so no index can serve it, and comparing a `DateTimeOffset` column with a `DateTime` input silently uses the server's local offset. Convert the calendar date in the business time zone (Recipe 10, Recipe 12):

```csharp
private DateTimeOffset ToUtcStartOfDay(DateTime date)
{
    var start = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
    return new DateTimeOffset(start, businessTimeZone.GetUtcOffset(start)).ToUniversalTime();
}
```

**Paging is asynchronous and bounded.** The house `Pagination<T>` historically executed the query inside its constructor — synchronous I/O on a request thread. New code uses an async factory and clamps the page size:

```csharp
public const int MaxPageSize = 100;

public static async Task<Pagination<T>> CreateAsync(
    IQueryable<T> query, int start, int length, CancellationToken ct = default)
{
    length = Math.Clamp(length, 1, MaxPageSize);
    var totalItems = await query.CountAsync(ct);
    var items = await query.Skip(Math.Max(start, 0)).Take(length).ToListAsync(ct);
    return new Pagination<T>(items, totalItems, start, length);
}
```

In an existing project whose `Pagination<T>` still runs in its constructor, add `CreateAsync` beside the constructor (an additive change) and use it in new code; moving the old callers is separate work. Synchronous I/O is a correctness rule, so it is not kept for consistency (`workflow.md`, precedence).

**Sorting.** `ApplySorting(SortColumn, …)` resolves a user-supplied name by reflection, hence the `InvalidFilterCriteriaException` catch in the controller. New code can map allowed keys explicitly instead (`"code" => i => i.Code, …`) and return a validation error for anything else.

> The keyword search above (`ToLower().Contains`) is the reference's style and is fine for small tables. No index can serve it, so on a table expected to grow large, use deliberate, prefix-friendly comparisons instead (`.claude/rules/backend.md` §6, `database.md` §3).

### 5. Mapper — `Server/Mappers/ProductMapper.cs`

```csharp
public class ProductMapper : Profile
{
    public ProductMapper()
    {
        CreateMap<ProductSearchReq, ProductSearchReqDto>();
        CreateMap<ProductResDto, ProductRes>();
    }
}
```

### 6. Controller action

```csharp
[HttpGet]
[Authorize(Policy = PermissionConstants.Products.ViewProducts)]
[ProducesResponseType(typeof(PaginationResultRes<List<ProductRes>>), StatusCodes.Status200OK)]
public async Task<IActionResult> GetAll([FromQuery] ProductSearchReq searchReq)
{
    var response = new PaginationResultRes<List<ProductRes>>();

    try
    {
        var searchDto = Mapper.Map<ProductSearchReqDto>(searchReq);
        searchDto.CurrentUserId = this.User.Claims.GetUserId();
        var result = await productService.GetAll(searchDto);

        response.Result = Mapper.Map<List<ProductRes>>(result.Items);
        response.Pagination = new PaginationRes
        {
            PageNumber = result.PageNumber,
            PageSize = result.PageSize,
            TotalPages = result.TotalPages,
            TotalItems = result.TotalItems,
        };
        response.Success = true;
        return Ok(response);
    }
    catch (InvalidFilterCriteriaException)
    {
        response.ErrorCode = ProductControllerMS.Get.InvalidColumnName;
        return BadRequest(response);
    }
    catch (Exception ex)
    {
        response.ErrorCode = ProductControllerMS.Get.Exception;
        Logger.LogError(ex, "Get all products failed");
        return InternalServerError(response);
    }
}
```

The `InvalidFilterCriteriaException` catch is required on any action that forwards a user-supplied `SortColumn`.

---

## Recipe 2 — Add a create endpoint

### Message constants — `Common/Messages/ProductControllerMS.cs`

```csharp
public static class ProductControllerMS
{
    public static class Get
    {
        public const string ProductNotFound = "Product.Get.ProductNotFound";
        public const string InvalidColumnName = "Product.Get.InvalidColumnName";
        public const string Exception = "Product.Get.Exception";
    }

    public static class Create
    {
        public const string CodeAlreadyExisted = "Product.Create.CodeAlreadyExisted";
        public const string CategoryNotFound = "Product.Create.CategoryNotFound";
        public const string Exception = "Product.Create.Exception";
    }

    public static class Update
    {
        public const string ProductNotFound = "Product.Update.ProductNotFound";
        public const string CodeAlreadyExisted = "Product.Update.CodeAlreadyExisted";
        public const string CategoryNotFound = "Product.Update.CategoryNotFound";
        public const string Exception = "Product.Update.Exception";
    }
}
```

Every operation group needs an `Exception` member — the controller's catch block uses it.

### Request model with validation

```csharp
public class CreateProductReq
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [EnumDataType(typeof(ProductStatus))]
    public ProductStatus Status { get; set; }

    [EmailAddress]
    [DataType(DataType.EmailAddress)]
    [MaxLength(100)]
    public string? ContactEmail { get; set; }

    public long? CategoryId { get; set; }
}
```

Required fields are non-nullable, initialised, with `[Required]` (which also rejects empty strings); optional fields use `string?` with no `[Required]`. Avoid the C# `required` modifier on request models: a missing property then fails JSON deserialisation of the whole body and the client loses the field-level error. `[MaxLength]` on the request must not exceed `[MaxLength]` on the entity column.

The service's uniqueness check gives the friendly error code; a unique index on `Code` (`database.md` §2) keeps the rule true when two requests race.

### Service

```csharp
public async Task<string> Create(CreateProductReqDto requestDto)
{
    var productRepo = unitOfWork.GetRepository<Product>();

    if (await productRepo.Any(i => i.Code == requestDto.Code))
    {
        return ProductControllerMS.Create.CodeAlreadyExisted;
    }

    if (requestDto.CategoryId.HasValue
        && !await unitOfWork.GetRepository<Category>().Any(i => i.Id == requestDto.CategoryId.Value))
    {
        return ProductControllerMS.Create.CategoryNotFound;
    }

    var product = new Product
    {
        Code = requestDto.Code,
        Name = requestDto.Name,
        Status = requestDto.Status,
        CategoryId = requestDto.CategoryId,
    };

    await productRepo.Add(product);
    await unitOfWork.SaveChanges();

    return string.Empty;
}
```

Rules visible here:
- validate first, mutate second — never partially write and then reject;
- uniqueness checks before referential checks;
- `string.Empty` is success; any other string is an error code;
- the entity is constructed field by field (no `Mapper.Map<Product>(dto)`), so unmapped columns cannot be silently set;
- never set `CreatedBy`/`CreatedAt`/`UpdatedBy`/`UpdatedAt` — `SaveChanges()` stamps them.

### Controller action

```csharp
[HttpPost]
[Authorize(Policy = PermissionConstants.Products.CreateProducts)]
[ProducesResponseType(typeof(ResultRes<string>), StatusCodes.Status200OK)]
public async Task<IActionResult> Create([FromBody] CreateProductReq request)
{
    var response = new ResultRes<string>();

    try
    {
        request = (CreateProductReq)request.TrimData();
        var errorCode = await productService.Create(Mapper.Map<CreateProductReqDto>(request));
        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            response.ErrorCode = errorCode;
            return BadRequest(response);
        }

        response.Success = true;
        return Ok(response);
    }
    catch (Exception ex)
    {
        response.ErrorCode = ProductControllerMS.Create.Exception;
        Logger.LogError(ex, "Create product failed");
        return InternalServerError(response);
    }
}
```

---

## Recipe 3 — Add an update endpoint

The route id is **not** part of the request body. Bind it separately and assign it onto the DTO:

```csharp
[HttpPut]
[Route("{id}")]
[Authorize(Policy = PermissionConstants.Products.UpdateProducts)]
public async Task<IActionResult> Update([FromRoute] long id, [FromBody] UpdateProductReq request)
{
    var response = new ResultRes<string>();
    try
    {
        request = (UpdateProductReq)request.TrimData();
        var requestDto = Mapper.Map<UpdateProductReqDto>(request);
        requestDto.Id = id;
        var errorCode = await productService.Update(requestDto);
        // … same shape as Create
    }
    // …
}
```

Service side — load, check, mutate, save:

```csharp
public async Task<string> Update(UpdateProductReqDto requestDto)
{
    var productRepo = unitOfWork.GetRepository<Product>();

    var product = await productRepo.FindById(requestDto.Id);
    if (product == null)
    {
        return ProductControllerMS.Update.ProductNotFound;
    }

    // every Create rule, with uniqueness excluding the current row
    if (await productRepo.Any(i => i.Code == requestDto.Code && i.Id != requestDto.Id))
    {
        return ProductControllerMS.Update.CodeAlreadyExisted;
    }

    if (requestDto.CategoryId.HasValue
        && !await unitOfWork.GetRepository<Category>().Any(i => i.Id == requestDto.CategoryId.Value))
    {
        return ProductControllerMS.Update.CategoryNotFound;
    }

    product.Code = requestDto.Code;
    product.Name = requestDto.Name;
    product.Status = requestDto.Status;
    product.CategoryId = requestDto.CategoryId;

    await productRepo.Update(product);
    await unitOfWork.SaveChanges();

    return string.Empty;
}
```

The `&& i.Id != requestDto.Id` clause is the single most commonly missed detail in update paths. Update mirrors **every** Create rule — uniqueness, referenced ids, state — and assigns every updatable field explicitly.

---

## Recipe 4 — Add a delete endpoint

Delete is soft by default: the entity implements `IIsDeletedEntity`, its configuration declares `HasQueryFilter(i => !i.IsDeleted)`, and `UnitOfWork.SaveChanges()` converts the delete into an `IsDeleted = true` update. The service just calls `Delete`:

```csharp
public async Task<string> Delete(long id)
{
    var productRepo = unitOfWork.GetRepository<Product>();

    var product = await productRepo.FindById(id);
    if (product == null)
    {
        return ProductControllerMS.Delete.ProductNotFound;
    }

    if (product.IsDefault)
    {
        return ProductControllerMS.Delete.CannotDeleteDefaultData;
    }

    // refuse to delete anything that is still referenced
    if (await unitOfWork.GetRepository<OrderLine>().Any(i => i.ProductId == id))
    {
        return ProductControllerMS.Delete.ProductIsStillHaveDependData;
    }

    await productRepo.Delete(product);
    await unitOfWork.SaveChanges();

    return string.Empty;
}
```

**Always check dependent data before deleting.** Soft delete does not cascade, so an unchecked delete leaves dangling references that still resolve to a hidden row.

### Status transitions instead of delete

Where the domain has a lifecycle, the reference exposes verbs rather than a delete:

```csharp
[HttpPut] [Route("{id}/approve")]  → service.ChangeStatus(id, Status.Active)
[HttpPut] [Route("{id}/reject")]   → service.ChangeStatus(id, Status.Reject)
[HttpPut] [Route("{id}/deactive")] → service.ChangeStatus(id, Status.InActive)
```

One `ChangeStatus(long id, TStatus status)` service method backs all of them. When some transitions must be refused, guard them with a transition table — Recipe 13.

---

## Recipe 5 — Add a persisted entity

### Entity — `DataAccess/Entities/Product.cs`

```csharp
[Table("Products")]
public class Product : BaseEntity, IIsDeletedEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [MaxLength(200)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public long? CategoryId { get; set; }

    public ProductStatus Status { get; set; }

    public bool IsDeleted { get; set; }

    public virtual ICollection<OrderLine> OrderLines { get; set; } = [];   // C# 12+; older: new List<OrderLine>()
}
```

- Table name is the plural of the entity.
- `long` identity primary key named `Id`.
- `BaseEntity` supplies the audit columns; do not redeclare them.
- `IIsDeletedEntity` + `IsDeleted` opts into soft delete.
- Navigations are `virtual ICollection<T>`, initialised so nullable analysis stays clean (nullable is on by default for `net10.0`).
- Schema rules — lengths, precision, delete behaviour, indexes, migration safety — are in `.claude/rules/database.md`.
- Foreign keys are `long` (required) or `long?` (optional).

### Configuration — `DataAccess/Configurations/ProductConfiguration.cs`

```csharp
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        // EF Core 10: name the filter so it can be disabled on its own
        builder.HasQueryFilter("SoftDelete", i => !i.IsDeleted);
    }
}
```

**Name the filter.** A query that must see deleted rows then disables only that one:

```csharp
await context.Products.IgnoreQueryFilters(["SoftDelete"]).ToListAsync();
```

On EF Core 9 and earlier the filter is anonymous (`HasQueryFilter(i => !i.IsDeleted)`) and `IgnoreQueryFilters()` drops **every** filter on the entity — including a tenant filter. That is a real footgun; use named filters wherever the project is on EF Core 10.

Add seed rows with `builder.HasData([...])` using ids from a constants class and fixed timestamps (never `DateTime.Now` — it makes migrations non-deterministic).

### Register it

Add `builder.ApplyConfiguration(new ProductConfiguration());` to `OnModelCreating`. **A configuration that is not applied silently does nothing — the soft-delete filter will be missing and deleted rows will appear in every query.**

### Migration

```
dotnet ef migrations add AddProduct --project <DataAccess> --startup-project <Server>
```

Review the generated migration before accepting it. Never edit an already-applied migration; add a new one.

---

## Recipe 6 — Add a permission

1. Add the constant to the feature's nested class in `PermissionConstants`.
2. Add it to the `All` array — **a permission missing from `All` has no registered policy, and `[Authorize(Policy = …)]` referencing it will reject every request.**
3. Decorate the action: `[Authorize(Policy = PermissionConstants.Products.CreateProducts)]`.
4. Grant it to the relevant roles (seed data or admin UI).

Authentication is already enforced by the base controller. Only endpoints that must be reachable anonymously should carry `[AllowAnonymous]`, and each one is a deliberate security decision.

---

## Recipe 7 — Register a new service

```csharp
// Server/Configurations/CoreDependencyConfiguration.cs → AddServices()
collection.AddScoped<IProductService, ProductService>();
```

Scoped is the default lifetime for anything that touches `IUnitOfWork`. Singletons must never capture a repository, a `DbContext` or a scoped service.

---

## Recipe 8 — Multi-aggregate write (transaction)

Use this **only** when one operation must write several aggregates atomically, or when a read determines a written value (sequence generation, balance checks).

```csharp
var strategy = unitOfWork.DbContext.Database.CreateExecutionStrategy();
try
{
    await strategy.ExecuteAsync(async () =>
    {
        // Everything this operation reads and writes happens inside the block:
        // on a transient failure the strategy runs the whole block again,
        // so start each attempt from a clean change tracker.
        unitOfWork.DbContext.ChangeTracker.Clear();
        await using var transaction = await unitOfWork.DbContext.Database.BeginTransactionAsync();

        // … load, validate, several repository writes …

        await unitOfWork.SaveChanges();
        await transaction.CommitAsync();
    });
}
catch (DbUpdateConcurrencyException)
{
    return OrderControllerMS.Create.ConcurrentUpdate;
}
```

Rules visible here:
- `CreateExecutionStrategy()` is required: the provider's retry policy cannot wrap a user-initiated transaction otherwise.
- **Do not catch exceptions inside the block.** The earlier house version caught `Exception`, rolled back and returned an error code inside the lambda — which hides transient failures from the strategy, so it never retries. Let exceptions propagate; an uncommitted transaction rolls back when disposed. Expected failures are handled outside (above: concurrency → its error code); anything else reaches the controller's catch and its `Exception` code.
- Business validation that returns an error code runs before any write inside the block; pass the code out through a local variable that is reset at the start of each attempt, without catching.
- `ChangeTracker.Clear()` discards entities staged before the block — so nothing may be staged before it. If the connection drops during `CommitAsync`, the commit may have succeeded; make the operation detectably idempotent (e.g. check for the row first) where a duplicate would matter.
- Never call `unitOfWork.Dispose()` here — the DI scope owns it (`.claude/rules/backend.md` §11).

For a single-aggregate write, skip all of this. `SaveChanges()` is already transactional.

---

## Recipe 9 — Cache reference data

Applies only to small, rarely-changing, globally-readable tables.

If the project already caches through `IMemoryCache`, `IDistributedCache` or its own service, use that and keep the two rules below. For a new cache, **`HybridCache`** (`Microsoft.Extensions.Caching.Hybrid`, stable since .NET 9) is the recommended choice — it is a new package, so it needs approval like any dependency. It gives a typed read-through, serialization, an in-memory L1 in front of the distributed L2, stampede protection and tag-based invalidation — all of which otherwise get hand-written.

```csharp
builder.Services.AddHybridCache();      // layers over the registered IDistributedCache

// read-through
var categories = await hybridCache.GetOrCreateAsync(
    $"{nameof(Category)}:all",
    async ct => await LoadCategories(ct),
    tags: ["reference-data"],
    cancellationToken: ct);

// invalidate in the same method that writes
await unitOfWork.SaveChanges();
await hybridCache.RemoveByTagAsync("reference-data", ct);
```

Two rules survive whatever the mechanism:

- **Keep an explicit allow-list of what may be cached.** Caching a table that changes per request produces stale reads that are very hard to trace.
- **Invalidate in the same method that writes.** A missed invalidation serves stale data for the full TTL — including from bulk imports and background jobs, which are the paths most often forgotten.

If the project already has its own cache service, use it rather than introducing a second mechanism.

---

## Recipe 10 — Background job

```csharp
[DisallowConcurrentExecution]
public class ExpireProductsJob(
    ILogger<ExpireProductsJob> logger,
    IServiceScopeFactory scopeFactory) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        // … work …
    }
}
```

Register with `collection.AddTransient<ExpireProductsJob>();`. A job runs outside a request, so there is **no `HttpContext`** — audit stamping falls back to user id `0`, and a scoped service must be resolved from an explicit scope.

**Time.** Inject `TimeProvider` (`dotnet-versions.md` §2.5) rather than reading `DateTime.Now`, so the job is testable with a fake clock. For a rule defined in **days** ("overdue from the day after the due date"), decide which timezone defines a day, keep it in configuration (Recipe 12), and compare dates, not timestamps:

```csharp
var zone = TimeZoneInfo.FindSystemTimeZoneById(
    configuration.GetSection(ConfigKeys.App.BusinessTimeZone).Get<string>() ?? "UTC");
var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), zone).DateTime);

// due today → not overdue yet; overdue only when DueDate < today
var overdue = query.Where(i => i.DueDate < today && i.Status != OrderStatus.Approved && !i.IsOverdue);
```

**Idempotency.** A job can run twice — a restart, a manual trigger, a missed schedule catching up. Running it twice on the same day must change nothing the second time: filter on the flag it sets (`!i.IsOverdue` above) so it never writes a second history row (Recipe 15) for the same event.

---

## Recipe 11 — File upload / download

Upload: validate on the request model, not in the service.

```csharp
public class ImportProductReq
{
    [Required]
    [AllowedExtensions([GlobalConstants.Files.ExcelExtension])]
    [ValidateFileSize(GlobalConstants.Files.MaxSize)]
    public required IFormFile File { get; set; }
}
```

Download: return a `FileContentResult` directly rather than wrapping bytes in `ResultRes<T>`.

```csharp
var bytes = await productService.ExportExcel(Mapper.Map<ExportProductReqDto>(request));
return new FileContentResult(bytes, GlobalConstants.Files.ExcelContentType)
{
    FileDownloadName = $"Products_{timeProvider.GetLocalNow().ToFormat(DateTimeFormats.DateTime2)}.xlsx",
    LastModified = timeProvider.GetUtcNow(),
};
```

Bulk import: compare the uploaded header row against the stored template, reject the whole file on the first invalid row (the reference does not do partial imports), collect inserts/updates/deletes into separate lists, then apply them in one `SaveChanges()`.

---

## Recipe 12 — Read configuration

```csharp
// Common/Constants/ConfigKeys.cs
public struct Payments
{
    public const string ApiBaseUrl = "Payments:ApiBaseUrl";
    public const string TimeoutSeconds = "Payments:TimeoutSeconds";
}

// service
private readonly string _baseUrl =
    configuration.GetSection(ConfigKeys.Payments.ApiBaseUrl).Get<string>() ?? string.Empty;
```

Never inline a configuration path string. Never commit a secret value — the key goes in `ConfigKeys`, the value goes in configuration or a secret store.

---

## Recipe 13 — Guarded status transition (state machine)

Extends the `ChangeStatus` verb pattern in Recipe 4. Use it whenever a status field has rules about which value may follow which — approval flows, order lifecycles, workflow boards.

### Allowed transitions — declared once, in Common

```csharp
// Common/Constants/OrderTransitions.cs
public static class OrderTransitions
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> Allowed = new()
    {
        [OrderStatus.Draft]     = [OrderStatus.Submitted],
        [OrderStatus.Submitted] = [OrderStatus.Approved, OrderStatus.Rejected],
        [OrderStatus.Rejected]  = [OrderStatus.Submitted],   // the one permitted step back
        [OrderStatus.Approved]  = [],                        // terminal
    };

    public static bool CanMove(OrderStatus from, OrderStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);
}
```

### Service

```csharp
public async Task<string> ChangeStatus(ChangeOrderStatusReqDto requestDto)
{
    var orderRepo = unitOfWork.GetRepository<Order>();

    var order = await orderRepo.FindById(requestDto.Id);
    if (order == null)
    {
        return OrderControllerMS.ChangeStatus.OrderNotFound;
    }

    if (order.Status == requestDto.Status)
    {
        return OrderControllerMS.ChangeStatus.AlreadyInStatus;
    }

    if (!OrderTransitions.CanMove(order.Status, requestDto.Status))
    {
        return OrderControllerMS.ChangeStatus.InvalidTransition;
    }

    var fromStatus = order.Status;
    order.Status = requestDto.Status;
    order.Version = Guid.NewGuid();   // Recipe 14 — two reviewers cannot both approve

    await orderRepo.Update(order);
    await unitOfWork.GetRepository<OrderHistory>().Add(new OrderHistory
    {
        OrderId = order.Id,
        Action = OrderHistoryAction.StatusChanged,
        FromValue = fromStatus.ToString(),
        ToValue = order.Status.ToString(),
    });

    try
    {
        await unitOfWork.SaveChanges();
    }
    catch (DbUpdateConcurrencyException)
    {
        return OrderControllerMS.ChangeStatus.ConcurrentUpdate;
    }

    return string.Empty;
}
```

Rules visible here:
- the transition table is **data in one place**, not `if` chains scattered across services and controllers — the frontend can be given the same table to hide illegal moves (presentation only);
- each rejected case has its own error code: not found, already in that status, illegal transition, concurrent update;
- a terminal status has an empty target list. **Reopening a terminal record is a separate endpoint with its own permission**, not an entry in this table;
- the history row (Recipe 15) is written in the **same** `SaveChanges()` as the change, so one cannot exist without the other;
- one test per row of the table plus one per forbidden jump is the minimum coverage (`.claude/rules/testing.md` §2).

---

## Recipe 14 — Invariant across rows (per-owner limit, concurrency guard)

"A customer may have at most 3 submitted orders", "a member may hold at most N active tasks", "only one default address" — any rule checked by **counting other rows** is a check-then-act race. Two requests both count 2, both pass, both write: the limit becomes 4. An application check alone cannot prevent it.

**Guard the row that owns the invariant with a concurrency token**, and change that token in the same `SaveChanges()` as the write. The second concurrent request then fails instead of silently breaking the rule.

### Entity — a provider-neutral token

```csharp
public class Customer : BaseEntity, IIsDeletedEntity
{
    // … existing columns …

    [ConcurrencyCheck]
    public Guid Version { get; set; }
}
```

The same token on `Order` itself (set in Recipe 13) stops two reviewers approving one order at once — a lost update rather than a counting race. A `Guid` set by the service works on every provider. `[Timestamp] byte[] RowVersion` is SQL Server–specific, and PostgreSQL's `xmin` needs provider configuration — use them only where the project already does.

### Service

```csharp
public async Task<string> Submit(SubmitOrderReqDto requestDto)
{
    var orderRepo = unitOfWork.GetRepository<Order>();
    var customerRepo = unitOfWork.GetRepository<Customer>();

    var order = await orderRepo.FindById(requestDto.Id);
    if (order == null)
    {
        return OrderControllerMS.Submit.OrderNotFound;
    }

    if (!OrderTransitions.CanMove(order.Status, OrderStatus.Submitted))
    {
        return OrderControllerMS.Submit.InvalidTransition;
    }

    var customer = await customerRepo.FindById(order.CustomerId);
    if (customer == null)
    {
        return OrderControllerMS.Submit.CustomerNotFound;
    }

    var submittedCount = await (await orderRepo.QueryAll())
        .CountAsync(i => i.CustomerId == customer.Id && i.Status == OrderStatus.Submitted);
    if (submittedCount >= OrderConstants.MaxSubmittedPerCustomer)
    {
        return OrderControllerMS.Submit.LimitReached;
    }

    order.Status = OrderStatus.Submitted;
    customer.Version = Guid.NewGuid();   // serialises concurrent submits for this customer

    await orderRepo.Update(order);
    await customerRepo.Update(customer);

    try
    {
        await unitOfWork.SaveChanges();
    }
    catch (DbUpdateConcurrencyException)
    {
        return OrderControllerMS.Submit.ConcurrentUpdate;
    }

    return string.Empty;
}
```

Use the repository's own count method if the project has one.

Rules visible here:
- the token goes on **the row the limit is counted against**. When the owner changes (a reassignment), the check and the token bump apply to the **new** owner;
- `DbUpdateConcurrencyException` is caught **in the service** and returned as an error code, keeping to the "failures are returned, not thrown" protocol; the client may retry;
- no explicit transaction is needed — a single `SaveChanges()` is already atomic (Recipe 8 is for multi-step writes);
- where the limit is exactly one ("only one default"), prefer a **filtered unique index** in the entity configuration: the database enforces it outright;
- the limit itself is a named constant (`OrderConstants.MaxSubmittedPerCustomer`), never a literal in the query.

---

## Recipe 15 — Append-only history (business audit trail)

The central audit columns on `BaseEntity` record only the **last** change. When the business needs every change — "who moved this to Done, and when" — keep a separate history table that is only ever appended to.

### Entity

```csharp
[Table("OrderHistories")]
public class OrderHistory : BaseEntity          // no IIsDeletedEntity — history is never deleted
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    public long OrderId { get; set; }

    public OrderHistoryAction Action { get; set; }   // StatusChanged, Reassigned, MarkedOverdue, …

    [MaxLength(100)]
    public string? FromValue { get; set; }

    [MaxLength(100)]
    public string? ToValue { get; set; }
}
```

**Who and when come from `BaseEntity`.** `SaveChanges()` stamps `CreatedBy` / `CreatedAt` as for any entity, so do not add separate actor or timestamp columns and do not set them by hand. A row written by a background job is stamped with user id `0` (Recipe 10) — render it as "System".

### Rules

- Written in the **same `SaveChanges()`** as the change it records (see Recipe 13). Never in a second call, never fire-and-forget.
- The service exposes **only `Add`**. No update method, no delete method, no endpoint that changes a history row — not even for administrators.
- Store **values, not sentences**: enum names and ids, not "Alice moved X to Done". Build the message when reading, joining users by `CreatedBy` — with `IgnoreQueryFilters(["SoftDelete"])` on the user join, so history by a since-deleted user still resolves.
- Read it ordered by `CreatedAt`, then `Id`, so two changes in the same instant keep their order.
- **Bulk updates bypass this.** `ExecuteUpdateAsync` (`dotnet-versions.md` §2.3) does not go through the service method, so no history row is written. Use the tracked path for any change the business needs to see in history.
- **[C]** Where tamper-proof history is a compliance requirement, enforce it in the database as well (deny UPDATE/DELETE on the table to the application's DB user, or a trigger). That is a decision for the Architect, not a default.

---

## Anti-patterns — do not reproduce these

They are defects, not conventions. The authoritative list is `.claude/rules/backend.md` §11.

| Anti-pattern | Do this instead |
|---|---|
| Secret or key literal in a constants file | Configuration key + secret store |
| Password generator returning a fixed string | Real CSPRNG generation |
| `unitOfWork.Dispose()` inside a service method | Let the DI scope dispose it |
| Mapping a request DTO straight onto an entity | Assign fields explicitly |
| Uniqueness checked on create but not on update | Mirror every rule in both paths |
| Deleting without checking dependent data | `Any(...)` on each referencing table first |
| `Task.FromResult` wrappers that look async | Keep genuinely async code async |
| `DateTime.Now` for stored values | `TimeProvider.GetUtcNow()` (or the project's existing UTC helper) |
| `LogError("… {ex}", ex)` | `LogError(ex, "…")` |
| Exceptions caught inside an execution-strategy block | Let them propagate; handle outside (Recipe 8) |
| Paging helper that runs the query synchronously | `await Pagination<T>.CreateAsync(...)` (Recipe 1) |
| A new `[AllowAnonymous]` endpoint added casually | Treat every one as a security decision |
