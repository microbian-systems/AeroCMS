# Aero.Cms Spec: Permissions, Roles, Claims, and Authorization

## Goal

Define the authorization architecture for a modular, multi-tenant CMS.

Authorization must support:
- per-tenant users
- roles
- claims
- module-defined permissions
- endpoint authorization
- content operation authorization
- admin UI visibility rules

## Core Concepts

### Permission
A named capability.

Examples:
- `Blog.View`
- `Blog.Edit`
- `Blog.Publish`
- `Media.Manage`
- `Users.Manage`
- `Tenants.Manage`

### Role
A named grouping of permissions.

Examples:
- Administrator
- Editor
- Author
- Reviewer
- ApiClient

### Policy
An ASP.NET Core authorization policy that maps to one or more permissions or claims.

## Permission Registration

Modules register permissions.

```csharp
public interface IPermissionProvider
{
    IEnumerable<PermissionDefinition> GetPermissions();
}

public sealed class PermissionDefinition
{
    public string Name { get; init; }
    public string DisplayName { get; init; }
    public string Category { get; init; }
    public string Description { get; init; }
}
```

Example provider:

```csharp
public sealed class BlogPermissionProvider : IPermissionProvider
{
    public IEnumerable<PermissionDefinition> GetPermissions()
    {
        yield return new() { Name = "Blog.View", DisplayName = "View blog" };
        yield return new() { Name = "Blog.Edit", DisplayName = "Edit blog posts" };
        yield return new() { Name = "Blog.Publish", DisplayName = "Publish blog posts" };
    }
}
```

## Role Model

Recommended normalized model:

```csharp
public sealed class Role
{
    public string Id { get; set; }
    public string TenantId { get; set; }
    public string Name { get; set; }
    public List<string> Permissions { get; set; } = new();
}
```

User-role assignment:
- many users to many roles
- tenant scoped

## Claims Model

Claims still matter for:
- subject ID
- tenant ID
- email
- display name
- external identity source
- coarse-grained role claim projection

Do not rely only on baked-in JWT role claims for all authorization because tenant role changes may need faster effect than token lifetime. Consider server-side permission lookup or token versioning.

## Authorization Service

Provide a CMS-level service:

```csharp
public interface IPermissionService
{
    Task<bool> HasPermissionAsync(ClaimsPrincipal user, string permission, string tenantId, CancellationToken ct = default);
}
```

Use this for:
- content operations
- admin UI visibility
- menu filtering
- widget visibility
- background job permission checks if needed

## ASP.NET Core Policy Integration

Dynamic policy provider pattern recommended.

Policy naming examples:
- `Permission:Blog.Edit`
- `Permission:Media.Manage`

Custom policy provider can parse these names and create requirements dynamically.

```csharp
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }
    public PermissionRequirement(string permission) => Permission = permission;
}
```

Handler delegates to `IPermissionService`.

## UI Visibility

Menu contributors and dashboard widgets can be permission-aware.

```csharp
public interface IAdminMenuContributor
{
    void BuildMenu(AdminMenuBuilder builder, ClaimsPrincipal user);
}
```

Builder can expose filtered add methods:

```csharp
builder.AddIfAuthorized("Blog", "/admin/blog", "Blog.View");
```

## Endpoint Examples

### Minimal API
```csharp
endpoints.MapGet("/admin/blog", ...)
    .RequireAuthorization("Permission:Blog.View");
```

### MVC / Razor Pages
Use `[Authorize(Policy = "Permission:Blog.Edit")]`

## Content Authorization

Content operations need finer checks than simple endpoint authorization.

Examples:
- edit own draft
- publish any post
- delete only within current tenant
- edit only content type X

Recommended contract:

```csharp
public interface IContentAuthorizationService
{
    Task<bool> CanViewAsync(ClaimsPrincipal user, ContentItem item, CancellationToken ct = default);
    Task<bool> CanEditAsync(ClaimsPrincipal user, ContentItem item, CancellationToken ct = default);
    Task<bool> CanPublishAsync(ClaimsPrincipal user, ContentItem item, CancellationToken ct = default);
    Task<bool> CanDeleteAsync(ClaimsPrincipal user, ContentItem item, CancellationToken ct = default);
}
```

## Multi-Tenant Requirements

Every permission check must include tenant context.
Never allow cross-tenant role or permission leakage.

Recommended claim:
- `tenant_id`

Still validate against current tenant shell.

## Admin Defaults

Suggested starter roles per tenant:
- Administrator: all permissions
- Editor: edit/publish content, manage media
- Author: create/edit own content, cannot publish
- Viewer: read-only admin access if desired

## Auditing

Every sensitive operation should log:
- tenant
- user ID
- permission evaluated
- target content/resource
- allow/deny outcome

## Testing Requirements

- permission provider registration
- dynamic policy resolution
- role assignment by tenant
- menu visibility filtering
- endpoint access correct per role
- cross-tenant access denied
- content ownership scenarios validated

## Deliverables

1. permission definition model
2. permission provider interface
3. role and role assignment model
4. permission service
5. ASP.NET Core dynamic policy provider
6. permission-aware UI/menu filtering
7. content authorization service
8. tests
