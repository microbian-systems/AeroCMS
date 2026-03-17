# Aero.Cms Spec: API Layer, Endpoint Composition, and Contract Boundaries

## Goal

Define how modules expose APIs and how the host composes them consistently.

## Principles

- Use `IEndpointRouteBuilder` for module endpoint registration
- Prefer route groups for isolation
- Version external APIs
- Separate admin APIs from public APIs
- Keep module boundaries explicit
- Apply tenant context before endpoint execution
- Apply localization/auth/authorization centrally where possible

## Base Pattern

```csharp
public interface IModule
{
    void Init(IEndpointRouteBuilder endpoints);
}
```

Since `WebApplication` implements `IEndpointRouteBuilder`, modules stay host-agnostic.

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

### Public API
Used by websites/headless consumers.
Examples:
- query published content
- query navigation
- search published content

### Admin API
Used by CMS admin UI.
Examples:
- create draft
- edit content
- upload media
- schedule publish
- manage users

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
