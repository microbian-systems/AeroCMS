# Aero.Cms Spec: API Layer, Endpoint Composition, and Contract Boundaries

## Goal

Define a high-performance, Native AOT-compatible API architecture using Minimal APIs and RazorSlices.

## Principles

- **Minimal APIs First:** Core CMS features (Pages, Blog) **MUST** use Minimal APIs for public-facing content delivery.
- **Native AOT Optimized:** Avoid reflection-based patterns (like standard MVC for public routes) to ensure minimal binary size and maximum performance.
- **Route Groups vs. BaseControllers:** Use **Route Groups** to apply shared metadata, security, and policies instead of class inheritance.
- **View Engine:** Use **RazorSlices** for reflection-free, compiled templates.
- **Strict Isolation:** Reserved traditional MVC/Razor for the complex Admin UI only.

## Infrastructure & Cross-Cutting Concerns

### Route Group Configuration
Shared metadata and security are applied at the group level:
- **Global Metadata:** Apply `Produces(StatusCodes.Status401Unauthorized)` and `Produces(StatusCodes.Status500InternalServerError)` to root groups.
- **Security:** Enforce `RequireAuthorization()` and `RequireRateLimiting("api")` globally via the CMS root group.

### Global Exception & Logging Filter
Implement a custom `IEndpointFilter` to wrap all CMS routes:
- **Logic:** `try-catch` block capturing unhandled exceptions, logging via `ILogger`, and returning `Results.Problem()`.
- **Debugging:** Must inspect `context.Arguments` to log incoming DTOs or Slugs.

## Route Mapping Requirements

### 1. Pages Group (Root-Level)
- **Prefix:** `""` (Empty string)
- **Routes:**
    - `GET /` -> Home Page Slice
    - `GET /{slug}` -> Generic Page Slice
- **Caching:** Apply `AeroPageCache` (Layer 1 Output Cache).

### 2. Blogs Group
- **Prefix:** `/blog`
- **Routes:**
    - `GET /` -> List all posts
    - `GET /{slug}` -> Display single post
- **Caching:** Apply `AeroBlogCache`.

## Implementation Pattern (Minimal APIs + RazorSlices)

```csharp
public void Init(IEndpointRouteBuilder endpoints)
{
    var cmsGroup = endpoints.MapGroup("")
        .WithOpenApi()
        .AddEndpointFilter<CmsExceptionFilter>();

    // Public Pages
    cmsGroup.MapGet("/{**slug}", async (string slug, IFusionCache cache, IDocumentSession db) => 
    {
        var page = await cache.GetOrSetAsync(
            $"page:{slug}", 
            _ => db.LoadAsync<Page>(slug),
            TimeSpan.FromMinutes(30));

        return page is not null 
            ? Results.Extensions.RazorSlice<PageSlice>(page) 
            : Results.NotFound();
    }).CacheOutput("AeroPageCache");
}
```

## Implementation Guidance

1. **Strict Native AOT:** No `System.Reflection`. Use `[JsonSerializable]` for all DTOs.
2. **RazorSlices:** All views must inherit from `RazorSlice<TModel>`.
3. **DI:** Inject `IFusionCache` and `IDocumentSession` (Marten) directly into delegate handlers.
4. **Cache Invalidation:** Implement `POST /admin/clear-cache` triggering `IOutputCacheStore.EvictByTagAsync`.

## Deliverables

1. Minimal API route group definitions
2. `IEndpointFilter` for global logging/exceptions
3. RazorSlices template integration
4. Output Caching policies (AeroPageCache, AeroBlogCache)
5. Admin cache invalidation endpoint
6. Native AOT compatibility tests

