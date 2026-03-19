# Aero.Cms Spec: API Layer, Endpoint Composition, and Contract Boundaries

## Goal

Define how modules expose APIs and how the host composes them consistently.

## Principles

- Use `IEndpointRouteBuilder` for module endpoint registration.
- **Minimal APIs:** Preferred for high-performance **Headless APIs** and JSON data delivery.
- **Traditional MVC/Razor/Blazor:** Used for the CMS frontend (Pages, Blog) and Admin UI where rich component logic or established rendering patterns are required.
- Prefer route groups for isolation.
- Version external APIs.
- Keep module boundaries explicit.
- Apply tenant context before endpoint execution.

## Route Group Configuration
Shared metadata and security are applied at the group level:
- **Global Metadata:** Apply `Produces(StatusCodes.Status401Unauthorized)` and `Produces(StatusCodes.Status500InternalServerError)` to root groups.
- **Security:** Enforce `RequireAuthorization()` and `RequireRateLimiting("api")` globally where appropriate.

## Public vs Admin Contracts

### Public API (Minimal APIs)
Used by headless consumers and data-driven integrations. 
**Constraint**: Use Minimal APIs for performance and clarity.

Example:
```csharp
endpoints.MapGet("/api/v1/public/pages/{culture}/{**slug}", async (string culture, string slug, PageService cms) => {
    var page = await cms.GetPageAsync(slug, culture);
    return page is not null ? Results.Ok(page) : Results.NotFound();
});
```

### CMS Frontend (MVC/Razor)
The primary website rendering engine for Pages and Blogs.

### Admin API & UI (Traditional MVC/Blazor)
Used by the CMS admin UI. These leverage standard Controllers or Blazor components.

