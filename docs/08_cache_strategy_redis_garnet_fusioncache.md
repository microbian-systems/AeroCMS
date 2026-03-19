# Aero.Cms Spec: The "Triple Threat" Caching Strategy

## Goal

Achieve maximum performance and Native AOT compatibility for Aero CMS through a multi-layered caching architecture.

## The Triple Threat Stack

### Layer 1: Output Caching (Full HTML/Response)
Responses are cached at the edge of the application pipeline to bypass the entire API logic for repetitive requests.
- **Backing Store:** Redis (via `AddStackExchangeRedisCache`).
- **Policy A (Pages):** Applied to the root group `app.MapGroup("")`. Default duration: 24 Hours.
- **Policy B (Blogs):** Applied to `/blog`. Default duration: 1 Hour. Includes `VaryByQuery("tag", "page")`.
- **Implementation:** Use ASP.NET Core Output Caching.

### Layer 2: Application Caching (Data Objects)
Used inside API handlers via **FusionCache** to prevent redundant database hits during output cache misses or for partial data retrieval.
- **L1 Cache:** Local Memory (fastest, per-node).
- **L2 Cache:** Distributed Redis (shared across instances).
- **Fail-Safe:** Serves stale data if the underlying database (Layer 3) is unavailable or timing out.
- **Stampede Protection:** Built-in request coalescing.

### Layer 3: Persistent Store (Source of Truth)
**Marten DB** (PostgreSQL Document Store).
- **AOT Optimization:** Must be configured for Native AOT using code-generation features during build time to avoid reflection at runtime.

## Cache Invalidation

### Programmatic Invalidation
- **Output Cache:** Use `IOutputCacheStore.EvictByTagAsync` to purge specific content or groups when updates occur.
- **FusionCache:** Use `RemoveAsync` or background refresh mechanisms.

### Administrative Invalidation
Implement a dedicated admin endpoint for emergency or manual purges:
- **Route:** `POST /admin/clear-cache`
- **Logic:** Triggers `IOutputCacheStore.EvictByTagAsync` for relevant CMS tags.

## Native AOT Requirements
- All cached Headless API DTOs must be decorated with `[JsonSerializable]` for the `SourceGeneratedContext`.
- Core infrastructure services should remain reflection-free where possible to support optional AOT modes.

## Key Rules

1. Every key must be tenant-scoped.
2. Include culture and theme where relevant.
3. Policy-driven expiration based on content type (Pages vs. Blogs).
4. Serve stale data on failure (Fail-Safe).

