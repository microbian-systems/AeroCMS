# Aero.Cms Spec: Tenant Provisioning, Lifecycle, and Administration

## Goal

Define how tenants are created, configured, updated, rebuilt, disabled, and deleted.

This spec extends the tenant shell architecture.

## Tenant Lifecycle States

```csharp
public enum TenantState
{
    Uninitialized,
    Running,
    Disabled,
    Rebuilding,
    Failed
}
```

## Tenant Model

```csharp
public sealed class TenantSettings
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Hostname { get; set; }
    public string UrlPrefix { get; set; }
    public string DatabaseName { get; set; }
    public string Theme { get; set; }
    public string DefaultCulture { get; set; } = "en";
    public List<string> SupportedCultures { get; set; } = new();
    public List<string> EnabledModules { get; set; } = new();
    public TenantState State { get; set; }
}
```

## Provisioning Flow

When a new tenant is created:

1. validate tenant name and hostname uniqueness
2. allocate tenant ID
3. create database or provision tenant partition
4. seed core tenant settings
5. seed baseline modules
6. seed admin role and bootstrap admin account
7. initialize shell cache entry as uninitialized
8. build shell on first request or eagerly

## Provisioning Service

```csharp
public interface ITenantProvisioningService
{
    Task<TenantSettings> CreateAsync(CreateTenantRequest request, CancellationToken ct = default);
    Task DisableAsync(string tenantId, CancellationToken ct = default);
    Task EnableAsync(string tenantId, CancellationToken ct = default);
    Task RebuildShellAsync(string tenantId, CancellationToken ct = default);
    Task DeleteAsync(string tenantId, CancellationToken ct = default);
}
```

## Create Request

```csharp
public sealed class CreateTenantRequest
{
    public string Name { get; set; }
    public string Hostname { get; set; }
    public string DatabaseName { get; set; }
    public string Theme { get; set; }
    public string DefaultCulture { get; set; }
    public List<string> SupportedCultures { get; set; } = new();
    public List<string> EnabledModules { get; set; } = new();
    public string AdminEmail { get; set; }
    public string AdminPassword { get; set; }
}
```

## Shell Rebuild Triggers

A tenant shell must be invalidated and rebuilt when:
- enabled modules change
- theme changes
- localization settings change
- auth mode changes
- plugin installed/uninstalled
- module config requiring pipeline change changes

Rebuild strategy:
1. mark state `Rebuilding`
2. build replacement shell off-path
3. swap atomically
4. dispose old shell safely
5. mark state `Running`

## Hostname Resolution

Support:
- host-based tenancy: `site1.com`
- path-based tenancy: `/t/site1`
- optional wildcard subdomains: `site1.example.com`

Hostname uniqueness rules should be explicit.

## Database Strategy

Preferred for Aero.Cms:
- database per tenant when feasible

Alternatives:
- shared DB with `TenantId`
- schema per tenant

For Marten:
- connection string per tenant
- per-tenant document store or tenant-aware session factory

## Tenant Admin Experience

Admin UI should allow:
- create tenant
- edit hostname
- enable/disable modules
- choose theme
- configure languages
- enable auth providers
- view health/status
- rebuild shell
- disable tenant
- view audit history

## Seed Data

At minimum:
- Administrator role
- bootstrap admin user
- default site settings
- initial homepage content if desired
- media root container

## Deletion Strategy

Hard delete is dangerous. Prefer:
- disable tenant
- archive content
- export backup
- require explicit confirmation for destructive delete

## Auditing

Log all tenant lifecycle events:
- created
- updated
- shell rebuilt
- disabled
- enabled
- deleted request
- deleted completed

## Testing Requirements

- create tenant provisions store correctly
- duplicate hostname rejected
- module change rebuilds shell
- disabled tenant does not serve content
- bootstrap admin created
- tenant DB connection uses correct database
- rebuild swap is atomic

## Deliverables

1. tenant settings model
2. provisioning service
3. lifecycle state model
4. tenant admin endpoints/UI
5. shell invalidation/rebuild pipeline
6. bootstrap seeding
7. tests
