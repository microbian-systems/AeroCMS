# Aero.Cms Spec: API Layer, Endpoint Composition, and Contract Boundaries

## Goal

Define how modules expose APIs and how the host composes them consistently.

## Principles

- Use `IEndpointRouteBuilder` for module endpoint registration.
- **Minimal APIs**: Core CMS features (Pages, Blog) **MUST** use Minimal APIs for public-facing content delivery to ensure maximum performance and AOT compatibility.
- **Traditional MVC/Razor**: Reserved for the Admin UI and complex application modules where model binding and controller-based orchestration are established patterns.
- Prefer route groups for isolation.
- Version external APIs.
- Keep module boundaries explicit.
- Apply tenant context before endpoint execution.

## Base Pattern (Minimal APIs)

```csharp
public interface IModule
{
    void Init(IEndpointRouteBuilder endpoints);
}
```

Public CMS content discovery:

```csharp
public void Init(IEndpointRouteBuilder endpoints)
{
    // Public Content Discovery (Minimal API + Razor Slices)
    endpoints.MapGet("{culture}/{**slug}", async (string culture, string slug, PageService cms) => 
    {
        var page = await cms.GetPageAsync(slug, culture);
        return page is not null 
            ? Results.Extensions.RazorSlice<PageSlice>(page) 
            : Results.NotFound();
    });
}
```

## Route Grouping

Recommended groups:
- `/api/admin/...`
- `/api/public/...`
- `/auth/...`
- `/webhooks/...`

Module example:

```csharp
public void Init(IEndpointRouteBuilder endpoints)
{
    var group = endpoints.MapGroup("/api/admin/blog");
    group.RequireAuthorization("Permission:Blog.View");

    group.MapGet("/posts", ...);
    group.MapPost("/posts", ...);
}
```

## Public vs Admin Contracts

### Public API (Minimal APIs)
Used by websites, headless consumers, and the core CMS frontend. 
**Constraint**: Must use Minimal APIs for performance and AOT compatibility.

Example:
```csharp
endpoints.MapGet("/api/v1/public/pages/{culture}/{**slug}", async (string culture, string slug, PageService cms) => {
    var page = await cms.GetPageAsync(slug, culture);
    return page is not null ? Results.Ok(page) : Results.NotFound();
});
```

### Admin API (Traditional MVC)
Used by the CMS admin UI. These remain as standard Controllers within Razor Class Libraries to leverage existing model binding and filter infrastructure.

Examples:
- `PageAdminController.cs`
- `MediaAdminController.cs`
- `ModuleAdminController.cs`

## DTO Discipline

Do not expose raw `ContentItem` internals directly unless the product is explicitly headless-first.
Use DTO mappers per module.

## Auth Schemes

Support multiple schemes concurrently:
- Cookies for admin browser UI
- JWT for API and SPA/mobile
- API keys for integrations
- optional OpenID Connect / external SSO

Endpoints should declare required auth scheme/policy explicitly.

## Endpoint Metadata

Consider a module endpoint descriptor for diagnostics/docs.

```csharp
public sealed class ModuleEndpointDescriptor
{
    public string Module { get; init; }
    public string Route { get; init; }
    public string HttpMethod { get; init; }
    public string Policy { get; init; }
    public bool AdminOnly { get; init; }
}
```

## Versioning

If external APIs are long-lived, support route versioning:
- `/api/v1/public/content/...`
- `/api/v1/admin/blog/...`

## OpenAPI

Generate OpenAPI for admin/public APIs, with tagging by module.

## Deliverables

1. module endpoint conventions
2. route grouping strategy
3. admin/public API separation
4. DTO guidelines
5. OpenAPI tagging by module
6. auth scheme and policy conventions
7. tests
