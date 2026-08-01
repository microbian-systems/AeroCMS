# Aero.Cms Spec: The "Triple Threat" Caching Strategy

## Goal

Achieve maximum performance and Native AOT compatibility for Aero CMS through a multi-layered caching architecture.

## Caching Profiles

Aero CMS uses two caching profiles:

| Surface | Cache Layers | Rule |
|---|---|---|
| Public-facing CMS pages, blog, docs, and public headless reads | Response caching + Output caching + FusionCache | Use the full stack: HTTP/client/proxy cache semantics, server-side full-response caching, and data/object caching. |
| Manager/admin UI and manager API data | FusionCache only | Do not apply ASP.NET response or output caching to manager routes. Cache manager data through FusionCache-backed services and invalidate through Wolverine messages. |

The public stack should be read as:

```text
Response cache / HTTP headers
  -> Output cache / server full response
    -> FusionCache / data and object cache
      -> AeroDB.Sable / SurrealDB source of truth
```

## Public Triple Threat Stack

### Layer 1: Response Caching (HTTP/Client/Proxy)
Response caching controls public HTTP cache semantics for browsers, CDNs, and intermediate proxies.
- **Implementation:** Use ASP.NET Core response caching middleware and response metadata/headers where public routes are safe to cache.
- **Scope:** Public-facing CMS routes and public headless reads only.
- **Manager Rule:** Never use response caching as a manager/admin data cache.

### Layer 2: Output Caching (Full HTML/Response)
Responses are cached at the edge of the application pipeline to bypass the entire API logic for repetitive requests.
- **Backing Store:** Redis (via `AddStackExchangeRedisCache`).
- **Policy A (Pages):** Applied to the root group `app.MapGroup("")`. Default duration: 24 Hours.
- **Policy B (Blogs):** Applied to `/blog`. Default duration: 1 Hour. Includes `VaryByQuery("tag", "page")`.
- **Implementation:** Use ASP.NET Core Output Caching.

### Layer 3: Application Caching (Data Objects)
Used inside public API handlers, public render services, and manager/admin data services via **FusionCache** to prevent redundant database hits during output cache misses or for partial data retrieval.
- **L1 Cache:** Local Memory (fastest, per-node).
- **L2 Cache:** Distributed Redis (shared across instances).
- **Fail-Safe:** Serves stale data if the underlying database is unavailable or timing out.
- **Stampede Protection:** Built-in request coalescing.

### Source of Truth: Persistent Store
**AeroDB.Sable** (SurrealDB document store — embedded SurrealKV or remote server).
- **AOT Optimization:** Must be configured for Native AOT using code-generation features during build time to avoid reflection at runtime.

## Cache Invalidation

### Programmatic Invalidation
- **Output Cache:** Use `IOutputCacheStore.EvictByTagAsync` to purge specific content or groups when updates occur.
- **FusionCache:** Use `RemoveAsync`, tag eviction, or background refresh mechanisms.
- **Manager/Admin:** Use the same Wolverine event-driven invalidation concept, but only evict FusionCache keys/tags. Manager routes must not rely on ASP.NET response or output cache invalidation.

### Administrative Invalidation
Implement a dedicated admin endpoint for emergency or manual purges:
- **Route:** `POST /admin/clear-cache`
- **Logic:** For public cache entries, evict relevant output cache tags and FusionCache tags/keys. For manager/admin data, evict only FusionCache tags/keys.

## Native AOT Requirements
- All cached Headless API DTOs must be decorated with `[JsonSerializable]` for the `SourceGeneratedContext`.
- Core infrastructure services should remain reflection-free where possible to support optional AOT modes.

## Key Rules

1. Every key must be tenant-scoped.
2. Include culture and theme where relevant.
3. Policy-driven expiration based on content type (Pages vs. Blogs).
4. Serve stale data on failure (Fail-Safe).
5. Keep manager/admin routes out of response and output caching policies; use FusionCache for manager data only.

