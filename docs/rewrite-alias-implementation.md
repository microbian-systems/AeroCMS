
> [!IMPORTANT]
> **STORAGE SUPERSEDED — MARTEN IS NO LONGER USED.** The backend database is now
> **SurrealDB via AeroDB.Sable** (embedded SurrealKV or remote server). Marten
> was migrated out in [`surrealdb-marten-port.md`](surrealdb-marten-port.md).
> This document is a historical implementation record; its Marten/PostgreSQL
> persistence details do not reflect the current stack.

# Rewrite Alias Middleware + Seed Fix Implementation

## Objective

1. Create a dynamic URL rewrite middleware using `Microsoft.AspNetCore.Rewrite` + custom `IRule`
2. Fix `/oops` page seed to use `pageContentService.SaveAsync()` for proper slug reservation
3. Fix blog post images to use Pexels instead of `static.photos`

---

## Why a Custom IRule?

Built‑in methods like `.AddRedirect()`, `.AddRewrite()` load rules **once at startup** and freeze them. They cannot be changed at runtime without restarting the app.

A **custom `IRule`** runs `ApplyRule(RewriteContext)` on **every request**, which means it can query a database, cache, or external source **dynamically**. This is fully documented and supported at:
[learn.microsoft.com/aspnet/core/fundamentals/url-rewriting](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/url-rewriting?view=aspnetcore-10.0)

---

## Implementation

### File 1: `src/Aero.Cms.Modules.Aliases/AliasRewriteRule.cs` (CREATE)

Custom `IRule` that:
- Queries Marten `AliasDocument` records on each request
- Caches results in `IMemoryCache` with a 30-second refresh
- Returns 301 redirect if `OldPath` matches the request path
- Uses `RuleResult.EndResponse` to stop the middleware chain

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.Caching.Memory;
using System.Net;
using Aero.Cms.Core.Entities;
using Marten;

namespace Aero.Cms.Modules.Aliases;

public sealed class AliasRewriteRule : IRule
{
    private readonly IDocumentSession _session;
    private readonly IMemoryCache _cache;
    private const string CacheKey = "rewrite-aliases";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public AliasRewriteRule(IDocumentSession session, IMemoryCache cache)
    {
        _session = session;
        _cache = cache;
    }

    public void ApplyRule(RewriteContext context)
    {
        var request = context.HttpContext.Request;
        var path = request.Path.Value?.ToLowerInvariant();
        if (string.IsNullOrEmpty(path)) return;

        var aliases = _cache.GetOrCreate(CacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return _session.Query<AliasDocument>().ToList();
        });

        if (aliases is null) return;

        foreach (var alias in aliases)
        {
            if (string.Equals(alias.OldPath, path, StringComparison.OrdinalIgnoreCase))
            {
                var response = context.HttpContext.Response;
                response.StatusCode = StatusCodes.Status301MovedPermanently;
                response.Headers["Location"] = alias.NewPath;
                context.Result = RuleResult.EndResponse;
                return;
            }
        }
    }
}
```

### File 2: `src/Aero.Cms.Modules.Aliases/AliassModule.cs` (MODIFY)

Register the rewrite middleware in `Run(...)`:

```csharp
// Add using:
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Aero.Cms.Modules.Aliases;

// In Run(IEndpointRouteBuilder builder):
public override void Run(IServiceProvider sp)
{
    // Rewrite middleware is registered at the application level
    // via the main pipeline (AeroAppServerExtensions / Program.cs)
    // The AliasRewriteRule is registered in ConfigureServices
    base.Run(sp);
}
```

Actually, the `IRule` needs to be registered differently. `IRule` is instantiated at `RewriteOptions` building time, which is at startup. Since we need `IDocumentSession` which is scoped, and the middleware pipeline is configured once, we have two options:

**Option A (Recommended):** Register a singleton `IRule` that receives an `IDocumentSession` factory or `IServiceProvider` and resolves scoped services inside `ApplyRule`.

```csharp
public sealed class AliasRewriteRule : IRule
{
    private readonly IMemoryCache _cache;
    private readonly IServiceProvider _serviceProvider;
    private const string CacheKey = "rewrite-aliases";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public AliasRewriteRule(IMemoryCache cache, IServiceProvider serviceProvider)
    {
        _cache = cache;
        _serviceProvider = serviceProvider;
    }

    public void ApplyRule(RewriteContext context)
    {
        var path = context.HttpContext.Request.Path.Value?.ToLowerInvariant();
        if (string.IsNullOrEmpty(path)) return;

        var aliases = _cache.GetOrCreate(CacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            using var scope = _serviceProvider.CreateScope();
            var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
            return session.Query<AliasDocument>().ToList();
        });

        if (aliases is null) return;

        foreach (var alias in aliases)
        {
            if (string.Equals(alias.OldPath, path, StringComparison.OrdinalIgnoreCase))
            {
                var response = context.HttpContext.Response;
                response.StatusCode = StatusCodes.Status301MovedPermanently;
                response.Headers["Location"] = alias.NewPath;
                context.Result = RuleResult.EndResponse;
                return;
            }
        }
    }
}
```

Registration in the Aliases module:

```csharp
// In ConfigureServices:
services.AddMemoryCache();
services.AddSingleton<IRule, AliasRewriteRule>();
```

And in the main application pipeline (AeroAppServerExtensions or Program.cs):

```csharp
var rewriteOptions = new RewriteOptions()
    .Add(app.Services.GetRequiredService<IRule>());
app.UseRewriter(rewriteOptions);
```

**Option B (Simpler for now):** Use a method-based rule inline.

```csharp
options.Add(context =>
{
    var cache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
    var session = context.HttpContext.RequestServices.GetRequiredService<IDocumentSession>();
    // ... same logic
});
```

**Decision:** Use Option A — cleaner separation and testable.

### File 3: `src/Aero.Cms.Modules.Setup/SeedDataService.cs` (MODIFY)

Fix `/oops` page — replace raw `session.Store(oopsPage)` with `pageContentService.SaveAsync()`:

```csharp
// BEFORE (line 322):
session.Store(oopsPage);

// AFTER:
await pageContentService.SaveAsync(oopsPage, ct);
```

This ensures `ContentSlugReservation` creates the slug record so the PagesModule can find `/oops`.

### File 4: `src/Aero.Cms.Modules.Setup/SeedDataService.cs` (MODIFY)

Fix blog images — update `BuildStarterBlogContent` to use `IPexelsService`:

```csharp
// BEFORE: posts use staticPhotosClient.GetPhotoUrl(...)
// AFTER:
// 1. Fetch 5-10 Pexels photos per category (technology, nature, office, etc.)
// 2. Download to wwwroot/media/blog/post-{id}.jpg
// 3. Register as MediaAsset
// 4. Assign local path to ImageUrl
// 5. Fall back to staticPhotosClient if Pexels API key is missing
```

Changes needed:
- Pass `IPexelsService` to `BuildStarterBlogContent`
- Before the post loop, fetch photos in batches
- Replace `staticPhotosClient.GetPhotoUrl(...)` with Pexels-downloaded local path
- Register each image as `MediaAsset` in Marten

---

## Registration

The `AliasRewriteRule` must be registered BEFORE the MVC/Razor Pages middleware in the pipeline. This is typically in `AeroAppServerExtensions` or `Program.cs`:

```csharp
var rewriteOptions = new RewriteOptions()
    .Add(sp.GetRequiredService<AliasRewriteRule>());
app.UseRewriter(rewriteOptions);
```

---

## Caching Strategy

| Aspect | Detail |
|--------|--------|
| Cache store | `IMemoryCache` (in-memory) |
| Cache key | `"rewrite-aliases"` |
| Duration | 30 seconds absolute expiration |
| Why 30s | Short enough for admin edits to take effect quickly; long enough to avoid DB load |
| Refresh | Automatic — next request after expiry reloads from Marten |

---

## Testing

- Seed → `/404` request → 301 redirect to `/oops`
- Seed → `/oops` request → 200 with 404 page content
- Seed → Blog posts → images load from `/media/blog/post-{id}.jpg` instead of `static.photos`

---

## Open Items

1. Where exactly to register `UseRewriter`? Options:
   - `AeroAppServerExtensions.cs` (centralized middleware pipeline)
   - `AliasModule.cs` (decentralized, module-owns)
   - `Program.cs` (application-level)

   **Recommendation:** Centralized in `AeroAppServerExtensions` alongside other middleware registrations like `app.UseStaticFiles()`, `app.UseRouting()`.

2. For blog images, how many Pexels API calls? 5 categories × 5 photos = ~5 search API calls + 25 downloads. Need to add delays between calls to respect Pexels rate limits (200 req/hr).
