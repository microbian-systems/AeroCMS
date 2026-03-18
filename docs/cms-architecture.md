# ASP.NET Core Block-Based CMS — Full Architecture Specification

> **Purpose:** Complete design specification for an agent swarm implementation. Covers all subsystems, data models, interfaces, code structure, flow diagrams, and implementation notes.

---

## Table of Contents

1. [System Overview](#1-system-overview)
2. [Technology Stack](#2-technology-stack)
3. [Block System](#3-block-system)
4. [Page & Layout System](#4-page--layout-system)
5. [Rendering Pipeline](#5-rendering-pipeline)
6. [Dynamic Blocks & ViewComponents](#6-dynamic-blocks--viewcomponents)
7. [Runtime Razor Compilation from Database](#7-runtime-razor-compilation-from-database)
8. [Output Caching & Cache Busting](#8-output-caching--cache-busting)
9. [Localization & Culture Routing](#9-localization--culture-routing)
10. [Module System](#10-module-system)
11. [Admin UI Shell](#11-admin-ui-shell)
12. [Content Lifecycle & Publishing Workflow](#12-content-lifecycle--publishing-workflow)
13. [Media Library](#13-media-library)
14. [Navigation & Menus](#14-navigation--menus)
15. [Users, Roles & Permissions](#15-users-roles--permissions)
16. [Multi-Tenancy](#16-multi-tenancy)
17. [Audit Log](#17-audit-log)
18. [Taxonomy](#18-taxonomy)
19. [Content Relationships](#19-content-relationships)
20. [Recycle Bin](#20-recycle-bin)
21. [Semantic & Full-Text Search (pg_vector)](#21-semantic--full-text-search-pg_vector)
22. [Headless API (Minimal APIs)](#22-headless-api-minimal-apis)
23. [Webhooks](#23-webhooks)
24. [Forms Builder](#24-forms-builder)
25. [Import / Export](#25-import--export)
26. [Redirects Manager](#26-redirects-manager)
27. [SEO Module](#27-seo-module)
28. [A/B Testing](#28-ab-testing)
29. [Personalisation](#29-personalisation)
30. [Analytics Hooks](#30-analytics-hooks)
31. [Notifications](#31-notifications)
32. [Marten Storage Conventions](#32-marten-storage-conventions)
33. [Project Structure](#33-project-structure)
34. [Agent Swarm Implementation Notes](#34-agent-swarm-implementation-notes)

---

## 1. System Overview

A block-based CMS built on ASP.NET Core where:

- **Pages** are composed of ordered **LayoutRegions**, each with 1–3 **Columns**, each containing ordered **Blocks**.
- **Blocks** are polymorphic content units.
- **Frontend Rendering (CMS/Blog)**: Uses **Razor Slices** for high-performance, lightweight rendering. Slices are used for pages, blog posts, and all block types.
- **API Strategy**: Core CMS features (Pages, Blog) use **Minimal APIs** for public delivery to ensure maximum performance and AOT compatibility.
- **Other Areas (Admin/Apps)**: Continue to use MVC/Razor/HTMX or Blazor (Server/WASM) as appropriate.
- **Storage**: PostgreSQL via Marten (document model) + `pg_vector` for semantic search.
- **The rendering pipeline**: Chain of Responsibility pattern.

### High-Level Flow (CMS/Blog)

```
HTTP Request /en/about
      │
      ├── CultureRedirectMiddleware    (ensure culture prefix)
      ├── RequestLocalizationMiddleware (set CultureInfo)
      ├── ETagMiddleware               (conditional GET handling)
      ├── OutputCacheMiddleware        (serve from cache if warm)
      │
      └── Minimal API MapGet("{culture}/{**slug}")
                │
                └── PageService.GetPageAsync()
                          │
                          └── PageReadPipeline (Chain of Responsibility)
                                    ├── [Order -10] AuthorizationHook
                                    ├── [Order -5]  CacheReadHook
                                    ├── [Order  0]  CorePageReadHook  ← Marten fetch
                                    ├── [Order  5]  SeoEnrichmentHook
                                    └── [Order  10] AnalyticsHook
                                          │
                                          └── Razor Slice Rendering
                                                    └── PageSlice<PageModel>
                                                              └── RegionSlice
                                                                        └── BlockSliceRegistry
                                                                                  └── IBlockSliceRenderer
```

---

## 2. Technology Stack

| Concern | Technology |
|---|---|
| Framework | ASP.NET Core 10+ |
| CMS Rendering | **Razor Slices** (lightweight static rendering) |
| API Layer | **Minimal APIs** (for public CMS/Blog) |
| Admin UI | Razor / cshtml + HTMX + Alpine.js |
| Styling | Tailwind CSS |
| Database | PostgreSQL via Marten (document) + **pg_vector** (semantic) |
| Identity/Auth | ASP.NET Core Identity (EF Core) |
| Caching | ASP.NET Core Output Cache + FusionCache |
| Search | PostgreSQL Full-Text Search + Vector Similarity |
| Background jobs | TickerQ |

---

## 22. Headless API (Minimal APIs)

All page/block content is accessible via a versioned JSON API. Core CMS features use Minimal APIs for these endpoints.

```csharp
public static class PagesApi
{
    public static void MapPagesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/pages");

        group.MapGet("/{culture}/{**slug}", async (string culture, string slug, PageService cms) =>
        {
            var page = await cms.GetPageAsync(slug, culture);
            return page is not null ? Results.Ok(page) : Results.NotFound();
        });

        group.MapGet("/", async (string culture, PageService cms, int page = 1, int pageSize = 20) =>
        {
            var result = await cms.ListPublishedAsync(culture, page, pageSize);
            return Results.Ok(result);
        });
    }
}
```

API responses serialize blocks polymorphically using source-generated JSON metadata.

---

[Rest of document preserved with existing specification logic]
