
> [!IMPORTANT]
> **STORAGE SUPERSEDED — MARTEN IS NO LONGER USED.** The backend database is now
> **SurrealDB via AeroDB.Sable** (embedded SurrealKV or remote server). Marten
> was migrated out in [`surrealdb-marten-port.md`](surrealdb-marten-port.md).
> This document is a historical implementation record; its Marten/PostgreSQL
> persistence details do not reflect the current stack.

# Tenant & Site Integration: Making Content Site-Conscious

## Problem Statement

Page, Blog Post, and Docs entities currently have **zero site or tenant awareness**. They lack `SiteId`, making multi-site content isolation impossible. All queries are global/unscoped, and the slug registry (`ContentSlugDocument`) is flat — two sites cannot have a page at `/about` without collision.

Only `AliasDocument` implements proper site-scoping with `long SiteId` + Marten index — use it as the reference pattern.

---

## Affected Files & Directories

### Entities (add `SiteId` + `ParentId` scoping)

| Entity | File | Current State | Changes Needed |
|--------|------|---------------|----------------|
| `PageDocument` | `src/Aero.Cms.Core.Entities/PageDocument.cs` | No SiteId | Add `long SiteId { get; set; }` |
| `BlogPostDocument` | `src/Aero.Cms.Core.Entities/BlogPostDocument.cs` | No SiteId | Add `long SiteId { get; set; }` |
| `DocsPage` | `src/Aero.Cms.Core.Entities/DocsPage.cs` | No SiteId | Add `long SiteId { get; set; }` |
| `ContentSlugDocument` | `src/Aero.Cms.Modules.Pages/SlugRegistry.cs` | No SiteId | Add `long SiteId` to make slugs unique per-site |
| `ContentSlugReservation` | `src/Aero.Cms.Modules.Pages/SlugRegistry.cs` | No site scoping | Thread SiteId through slug reservation logic |

### Marten Schema Configuration (add indexes)

| Module File | Current Indexes | Changes Needed |
|-------------|----------------|----------------|
| `src/Aero.Cms.Modules.Pages/PagesModule.cs:46-53` | Slug, PublishedOn, CreatedOn, ModifiedOn | Add `.Index(x => x.SiteId)` and `.UniqueIndex(x => x.SiteId, x => x.Slug)` |
| `src/Aero.Cms.Modules.Blog/BlogModule.cs:56-63` | Slug, PublishedOn, CreatedOn, ModifiedOn | Add `.Index(x => x.SiteId)` and `.UniqueIndex(x => x.SiteId, x => x.Slug)` |
| `src/Aero.Cms.Modules.Docs/DocsModule.cs:26-34` | Slug, ParentId, Order, PublishedOn, CreatedOn, ModifiedOn | Add `.Index(x => x.SiteId)` and `.UniqueIndex(x => x.SiteId, x => x.Slug)` |

### Content Services (inject site context, scope queries)

| Service File | Current State | Changes Needed |
|-------------|---------------|----------------|
| `src/Aero.Cms.Modules.Pages/PageContentService.cs:31` | `IDocumentSession`, `IBlockService`, `IMessageBus` only | Inject `ISiteContext`, scope all queries by SiteId |
| `src/Aero.Cms.Modules.Blog/BlogPostContentService.cs:33` | `IDocumentSession` only | Inject `ISiteContext`, scope all queries by SiteId |
| `src/Aero.Cms.Modules.Docs/DocsService.cs:11` | `IDocumentSession`, `IMessageBus` only | Inject `ISiteContext`, scope all queries by SiteId |

### Validators (add SiteId rules)

| Validator File | Current Rules | Changes Needed |
|----------------|---------------|----------------|
| `src/Aero.Cms.Modules.Pages/Validators/PageModelvalidator.cs` | Id, Slug, Title | Add `RuleFor(x => x.SiteId).GreaterThan(0)` |
| `src/Aero.Cms.Modules.Docs/IDocsService.cs` | N/A (interface only) | Add `SaveAsync` overload or SiteId param (see notes) |
| `src/Aero.Cms.Abstractions/Validators/DocRequestValidator.cs` | Title, Content, SiteId (already!) | `CreateDocRequestValidators` already validates `SiteId > 0` — but field is unused — update to actually use it |

### Request DTOs (add SiteId where missing)

| DTO File | Current State | Changes Needed |
|----------|---------------|----------------|
| `src/Aero.Cms.Abstractions/Requests/CreatePageRequest.cs` | No SiteId | Add `long SiteId` field |
| `src/Aero.Cms.Abstractions/Requests/CreateDocRequest.cs` | **Has `long SiteId` already!** | No change needed — just wire it through `DocsService` |

### Docs Service Fix (the half-finished refactor)

| File | Problem | Fix |
|------|---------|-----|
| `src/Aero.Cms.Modules.Docs/DocsService.cs` | `CreateDocRequest.SiteId` is **validated but never used** — `DocsService.SaveAsync()` doesn't set it | Thread `request.SiteId` into `DocsPage.SiteId` |
| `src/Aero.Cms.Modules.Docs/DocsService.cs:98-117` | `ToViewModel()` maps everything except SiteId | Add `SiteId = page.SiteId` to the view model mapping |

### ViewModels (ensure SiteId surfaces)

| ViewModel | File | SiteId? |
|-----------|------|---------|
| `AeroEntityViewModel` | `src/Aero.Cms.Abstractions/Models/AeroEntityViewModel.cs:13` | ✅ Has `long SiteId { get; set; }` |
| `DocViewModel` | `src/Aero.Cms.Abstractions/Models/DocViewModel.cs` | ❌ Missing — inherits `AeroEntityViewModel` which has SiteId, but `DocsService.ToViewModel()` doesn't map it |

### Seed Data (propagate SiteId on initial content creation)

| File | Changes Needed |
|------|----------------|
| `src/Aero.Cms.Modules.Setup/SeedDataService.cs:227-288` (`SeedStarterContentAsync`) | All PageDocuments, BlogPostDocuments, and DocsPages created during seeding must have `SiteId = site.Id` set |
| `src/Aero.Cms.Modules.Setup/SeedDataService.cs:342-645` (BuildXxx methods) | Builder methods should accept `long siteId` parameter and assign it to each entity |

---

## Implementation Order

The work should proceed in this order (each step depends on the previous):

### Step 1 — Add `SiteId` to Entities

Edit the three entity files:

**`PageDocument.cs`** — add after `ModifiedBy` (or after line 11):
```csharp
public long SiteId { get; set; }
```

**`BlogPostDocument.cs`** — add after line 9:
```csharp
public long SiteId { get; set; }
```

**`DocsPage.cs`** — add after `ModifiedBy` (after `Entity` line 8):
```csharp
public long SiteId { get; set; }
```

### Step 2 — Add SiteId to `ContentSlugDocument`

**`src/Aero.Cms.Modules.Pages/SlugRegistry.cs:13-47`**
- Add `public long SiteId { get; set; }` property
- Update `ContentSlugDocument.Create()` method signature: accept `long siteId`, assign it
- Update `ContentSlugReservation.ReserveAsync()` to accept and pass `long siteId`

### Step 3 — Configure Marten Indexes

**`src/Aero.Cms.Modules.Pages/PagesModule.cs:46-53`** — replace the block:
```csharp
// Before
opts.Schema.For<PageDocument>().Index(x => x.Slug);

// After
opts.Schema.For<PageDocument>().Index(x => x.SiteId);
opts.Schema.For<PageDocument>().UniqueIndex(x => x.SiteId, x => x.Slug);
```
(Keep the existing PublishedOn/CreatedOn/ModifiedOn indexes)

**`src/Aero.Cms.Modules.Blog/BlogModule.cs:56-63`** — same pattern:
```csharp
opts.Schema.For<BlogPostDocument>().Index(x => x.SiteId);
opts.Schema.For<BlogPostDocument>().UniqueIndex(x => x.SiteId, x => x.Slug);
```

**`src/Aero.Cms.Modules.Docs/DocsModule.cs:26-34`** — same pattern:
```csharp
opts.Schema.For<DocsPage>().Index(x => x.SiteId);
opts.Schema.For<DocsPage>().UniqueIndex(x => x.SiteId, x => x.Slug);
```

### Step 4 — Inject `ISiteContext` into Content Services

**`MartenPageContentService`** (`PageContentService.cs:31`):
- Add constructor parameter: `ISiteContext siteContext`
- Store as private field `_siteContext`
- **All query methods** need `Where(x => x.SiteId == _siteContext.SiteId)` appended:
  - `GetAllPagesAsync` — add `.Where(x => x.SiteId == _siteContext.SiteId)` after `session.Query<PageDocument>()`
  - `FindBySlugAsync` — the slug lookup uses `ContentSlugDocument` — scope both the slug query and the page load
  - `LoadAsync` — verify the loaded page belongs to the current site (optional safety check)
  - `LoadHomepageAsync` / `LoadBlogListingAsync` — flow through `FindBySlugAsync` (already routes through slug registry)
  - **`SaveAsync` / `CreateAsync`** — populate `page.SiteId = _siteContext.SiteId` before persisting
  - `DeleteAsync` — scope the slug reservation deletion

**`MartenBlogPostContentService`** (`BlogPostContentService.cs:33`):
- Add constructor parameter: `ISiteContext siteContext`
- Store as `_siteContext`
- Scope all queries: `GetAllPostsAsync`, `FindBySlugAsync`, `GetLatestPostsAsync`, `GetPagedPostsAsync`, `GetByTagAsync`, `GetByCategoryAsync` — add `.Where(x => x.SiteId == _siteContext.SiteId)`
- `SaveAsync` — populate `post.SiteId = _siteContext.SiteId`

**`DocsService`** (`DocsService.cs:11`):
- Add constructor parameter: `ISiteContext siteContext`
- Store as `_siteContext`
- Scope all queries:
  - `GetAllAsync` — add `.Where(x => x.SiteId == _siteContext.SiteId)`
  - `GetBySlugAsync` — add `.Where(x => x.SiteId == _siteContext.SiteId)`
  - `GetByIdAsync` — verify (optional)
  - `GetChildrenAsync` — add `.Where(x => x.SiteId == _siteContext.SiteId)`
  - `GetTopLevelCategoriesAsync` — add `.Where(x => x.SiteId == _siteContext.SiteId)`
- `SaveAsync` — populate `page.SiteId = _siteContext.SiteId` before storing

**SlugRegistry (`ContentSlugReservation.ReserveAsync`)**:
- Add `long siteId` parameter
- Store `SiteId` on created `ContentSlugDocument`
- Update all callers in `PageContentService.SaveAsync()` and `BlogPostContentService.SaveAsync()`

### Step 5 — Wire `CreateDocRequest.SiteId` Through DocsService

**`src/Aero.Cms.Modules.Docs/DocsService.cs`**:
- Currently `IDocsService.SaveAsync(DocsPage)` — the service doesn't have a `CreateAsync` that accepts `CreateDocRequest`
- Option A: Add `CreateAsync(CreateDocRequest request)` method to `IDocsService` and `DocsService`
  - Map `request.SiteId` → `page.SiteId = request.SiteId` (rather than relying on `ISiteContext`)
- Option B: In `SaveAsync`, use `ISiteContext` like the other services
- **Recommendation: Use Option B** for consistency — `ISiteContext` is the canonical source of the current site

Also update `DocsService.ToViewModel()` to include:
```csharp
SiteId = page.SiteId
```

### Step 6 — Update Validators

**`src/Aero.Cms.Modules.Pages/Validators/PageModelvalidator.cs`**:
```csharp
RuleFor(x => x.SiteId).GreaterThan(0).WithMessage("SiteId must be a positive integer.");
```

**`src/Aero.Cms.Abstractions/Validators/DocRequestValidator.cs`**:
- `CreateDocRequestValidators` already has `RuleFor(x => x.SiteId).GreaterThan(0)` — this is correct, no change needed

### Step 7 — Update SeedDataService to Pass SiteId

**`src/Aero.Cms.Modules.Setup/SeedDataService.cs`**:

The `SeedStarterContentAsync` method at line 227 creates pages, blog posts, and docs. It currently has access to the `site.Id` from `CreateTenantAndSiteAsync` (line 120).

Changes needed:

1. `SeedStarterContentAsync` must accept `long siteId` parameter
2. Pass `site.Id` to it at line 141
3. All builder methods (`BuildHomepage`, `BuildBlogListingPage`, `BuildAboutPage`, `BuildContactPage`, `BuildStarterBlogContent`, `BuildStarterDocsContent`) must accept `long siteId` and set it on every entity they create:
   - `PageDocument.SiteId = siteId` (homepage, about, contact, blog listing)
   - `BlogPostDocument.SiteId = siteId` (all 30 blog posts)
   - `DocsPage.SiteId = siteId` (all 10 doc pages)

4. The `NavigationBlock` entries don't need SiteId (they reference PageIds which are already linked).

5. The `BuildPost` helper (line 742) needs a `long siteId` parameter:
```csharp
private static BlogPostDocument BuildPost(long id, string slug, string title, string excerpt, string markdown, long siteId, List<long>? tagIds = null, ...)
```

6. The `BuildStarterDocsContent` method needs a `long siteId` parameter, set on each `DocsPage`.

### Step 8 — Add SiteId to Request DTOs (CreatePageRequest)

**`src/Aero.Cms.Abstractions/Requests/CreatePageRequest.cs`**:
- Add `long SiteId` as the first or second parameter in the record:
```csharp
public record CreatePageRequest(
+   long SiteId,
    string Title,
    ...
```
- This is an Orleans `[GenerateSerializer]` record, so adding a new positional parameter is a **breaking change** for any grains or serialized messages. Update all callers.

**`src/Aero.Cms.Abstractions/Requests/UpdatePageRequest.cs`**:
- Add `long SiteId` to enable cross-site safety checks.

### Step 9 — Verify `ISiteContext` Registration

Ensure `ISiteContext` and its default implementation are registered in DI.

Check: `src/Aero.Cms.Web/Infrastructure/DefaultSiteContext.cs` is the implementation. Confirm it's registered as scoped in the DI setup (likely in `Aero.Cms.Web/Program.cs` or a module bootstrapper):

```csharp
services.AddScoped<ISiteContext, DefaultSiteContext>();
services.AddHttpContextAccessor(); // required by DefaultSiteContext
```

If missing, add DI registration.

---

## Testing Strategy

Since the DB will be dropped (per your instruction), there's no migration to worry about. Verification:

1. **Build check** — `dotnet build` should succeed with all changes
2. **Validation** — run the setup flow through to completion, verify that seeded content has correct `SiteId`:
   - Query Marten directly to confirm `PageDocument.SiteId == createdSiteId`
   - Query Marten to confirm `BlogPostDocument.SiteId == createdSiteId`
   - Query Marten to confirm `DocsPage.SiteId == createdSiteId`
3. **Slug uniqueness** — attempt to create two pages with the same slug in different sites — should succeed. Same slug in the same site — should fail (unique constraint)
4. **Query scoping** — with two sites, verify that site A's content API doesn't return site B's content

---

## Risks & Notes

1. **Orleans serialization** — `CreatePageRequest` and `UpdatePageRequest` are `[GenerateSerializer]` records. Adding positional parameters changes the serialization contract. If these are persisted in queues or grain storage, the old format won't deserialize. Since we're dropping the DB, this is safe — but be aware if any Orleans reminders/queues persist across DB drops.

2. **`CreateDocRequest` already has `SiteId`** — this was added ahead of the entity support. The validator already requires `SiteId > 0`. The fix is just to wire it through the service.

3. **The `DocsService` doesn't have a `CreateAsync(CreateDocRequest)` method** — it only has `SaveAsync(DocsPage)`. You may need to add a `CreateAsync` method that maps from the request, or rely on `ISiteContext` injection.

4. **Tags and Categories** (`src/Aero.Cms.Modules.Blog/Models/Tag.cs`, `Category.cs`) are currently global. They may eventually need `SiteId` too, but are out of scope for this pass.

5. **Slug conflict behavior** — the `SlugConflictException` is thrown at the Marten unique index level. Since we're adding a composite unique index on `(SiteId, Slug)`, conflicts will be caught by Marten and bubble up as exceptions. The existing `ArgumentNullException` catch blocks in the services will convert these to `Result.Failure`. No additional conflict handling is needed.

---

## Reference: Existing Patterns

### AliasDocument (the reference implementation)
```csharp
// Entity (src/Aero.Cms.Core.Entities/AliasDocument.cs)
public class AliasDocument : Entity
{
    public long SiteId { get; set; }   // ✅ proper site scoping
    public string OldPath { get; set; } = null!;
    public string NewPath { get; set; } = null!;
    public string? Notes { get; set; } = null!;
}

// Marten config (src/Aero.Cms.Modules.Aliases/AliassModule.cs)
opts.Schema.For<AliasDocument>().Index(x => x.SiteId);
opts.Schema.For<AliasDocument>().UniqueIndex(x => x.OldPath); // site-scoped unique on old path
```

### ISiteContext interface
```csharp
// Aero/src/Aero.Core.Http/ISiteContext.cs
public interface ISiteContext
{
    long SiteId { get; }
    long TenantId { get; }
}

// Implementation (src/Aero.Cms.Web/Infrastructure/DefaultSiteContext.cs)
// Reads X-Site-Id and X-Tenant-Id from HTTP request headers
```

### Schemas (document aliases)
```csharp
// src/Aero.Cms.Core/AeroConstants.cs
public static class Tables
{
    public const string Pages = "pages";
    public const string Posts = "posts";
    // ... etc
}
```

---

## Summary Checklist

- [ ] **Step 1**: Add `long SiteId` to `PageDocument`, `BlogPostDocument`, `DocsPage`
- [ ] **Step 2**: Add `long SiteId` to `ContentSlugDocument` + thread through `ContentSlugReservation`
- [ ] **Step 3**: Add `.Index(x => x.SiteId)` + `.UniqueIndex(x => x.SiteId, x => x.Slug)` in all 3 module configs
- [ ] **Step 4**: Inject `ISiteContext` into `MartenPageContentService`, `MartenBlogPostContentService`, `DocsService` — scope queries + set on save
- [ ] **Step 5**: Wire `CreateDocRequest.SiteId` through `DocsService` + update `ToViewModel()`
- [ ] **Step 6**: Add `SiteId` validation to `PageDocumentValidator`
- [ ] **Step 7**: Update `SeedDataService` — pass `site.Id` → all builder methods → all entities
- [ ] **Step 8**: Add `long SiteId` to `CreatePageRequest` and `UpdatePageRequest`
- [ ] **Step 9**: Verify `ISiteContext` + `DefaultSiteContext` DI registration
- [ ] **Build**: `dotnet build` succeeds
- [ ] **Verify**: Setup flow creates site-scoped content
