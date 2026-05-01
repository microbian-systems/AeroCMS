# Aero CMS Manager Site Tasks

## Multi-Tenancy and Site Scope

The anticipated multi-tenancy model does not combine tenants into a shared host.

- One Docker container maps to one tenant.
- A tenant container can serve multiple sites.
- Site resolution happens inside the CMS by hostname/domain lookup.
- Each tenant has its own non-shared Postgres database.
- That database can contain data for multiple sites.
- Every site-owned table/document/entity must have a `long SiteId` foreign key.

Multi-tenancy is handled at the proxy level. After load balancers hit the proxies, the proxy will resolve the incoming domain name to a tenant id, find the right service/container, and forward the request there. This is future work and is listed here for informational purposes.

## Architecture Decisions From Review

### Modules Are Per Instance

Do not implement per-site or per-tenant module enablement.

- Modules are compiled into and registered for the container instance.
- The module catalog, dependency graph, DI registration, and module lifecycle are per-instance.
- A site must not rebuild DI or run module startup separately.
- Remove/ignore tenant-level module enablement decisions from runtime module flow.
- Keep environment-level module policy if needed, such as `DisabledInProduction`; that is not the same as tenant/site module enablement.

### Site-Owned Content

Site ownership is still required for content/data.

- Pages, posts/blogs, docs, aliases, banners, taxonomy, navigation, media, and other site-owned models must have `long SiteId`.
- Site-owned reads must filter by `SiteId`.
- Site-owned writes must stamp `SiteId` from the resolved current site context.
- Normal create/update/delete requests should not trust client-supplied `SiteId`.
- Site-owned events should include `SiteId`.

### Setup Bootstrap

Setup should continue creating the tenant and default site as it does now.

- Tenants are generally managed outside the CMS in the future hosting/provisioning model.
- The setup/bootstrap flow still creates a local/default tenant record and a default site.
- Retain the created tenant id and site id in setup state.
- Seed starter pages, posts, docs, media, navigation, taxonomy, and other site-owned data with the default site's `SiteId`.

### Site Domains

The Site model must support multiple domains/CNAMEs.

- Store domains as a dictionary keyed by route host/domain, for example `mydomain.com` or `www.mynewwebsite.com`.
- Proposed shape: `Dictionary<string, AliasDocument>` where the key is the route URL/domain and the value uses the alias model in `src/Aero.Cms.Core.Entities/AliasDocument.cs`.
- Site Settings must allow editing the domain/CNAME collection.
- Site Settings must display immutable tenant id and immutable site id.

### Docs Module Fix

`Aero.Cms.Modules.Docs` currently has an incomplete `SiteId` implementation.

- `CreateDocRequest` has `long SiteId`.
- The validator requires `SiteId > 0`.
- `DocsService` ignores that value.
- `DocsPage` has no `SiteId` field.

Required work:

- Add `long SiteId` to `DocsPage`.
- Update docs create/save paths so `SiteId` is stamped from the resolved current site context.
- Update docs list, slug, child, category, and page queries to filter by `SiteId`.
- Update docs view models/events as needed so `SiteId` is preserved.
- Convert docs slug/path uniqueness to site-scoped uniqueness, such as `(SiteId, Slug)` or `(SiteId, Path)` depending on the final docs routing model.
- Update starter docs seeding to stamp the default site id.

### Alias Indexing

Aliases are site-owned.

- Use unique `(SiteId, OldPath)` or `(SiteId, SourcePath)` for redirect lookup.
- Do not make `(SiteId, NewPath)` unique.
- A non-unique `(SiteId, NewPath)` index is acceptable if reverse lookup/search needs it.
- Multiple old paths must be allowed to redirect to the same new path.
- Alias resolution must always be `SiteId + old path`, never path alone.

### Wolverine Handler Discovery

The source-generator/Wolverine decision remains unchanged.

- Generated handler discovery should be attribute based.
- Every generated-discovery Wolverine handler should use `[WolverineHandler]`.
- Keep analyzer coverage for intended handlers missing the attribute.
- Do not use broad interface scanning as the source-generator discovery mechanism.

## Remaining Tasks for the Aero CMS Manager

## Manager

- Dashboard UI makeover
    - UI: needs to get the UI from the dashboard path D:\html-templates\mosaic
        - don't need to make it functional but the UI replacing the current UI page would be great (markup - the .html file has to be a .razor or .cshtml1)
    - Remove Settings as a submenu and put it as an anchor at the bottom of the left-side menu.

- Sites feature
    - Site should have a `TenantId` for reference only; tenants are managed outside the CMS long term.
    - Site should support multiple domains/CNAMEs.
    - After the last top nav menu item, display the currently selected site.
    - Clicking the current site opens the site selection menu.
    - `CTRL + S` should open the site selection menu.
    - Site Settings should be positioned just under the Dashboard menu item.
    - Site Settings is site-specific, not global.
    - User can edit site name.
    - User can edit site domains/CNAMEs.
    - User can edit site description.
    - Display immutable tenant id.
    - Display immutable site id.

- Aliases Menu
    - Place directly under the new Sites menu item.
    - Main module is `Aero.Cms.Modules.Aliases`.
    - Add UI/API support for creating aliases with old URL and new URL.
    - Automatically create/update aliases when a URL/slug changes for blog, page, or doc rename.
    - Add/confirm `AliasViewModel` in `Aero.Cms.Abstractions`.
    - Create the alias API in `Aero.Cms.Modules.Headless`.
    - Add an HTTP client for the new alias API.
    - Add/confirm FluentValidation validators for the alias model/request.
    - Change alias uniqueness from global old path to unique `(SiteId, OldPath)`.
    - Keep `NewPath` non-unique; optionally add non-unique `(SiteId, NewPath)` index for reverse lookup.

- Banners Menu
    - Add a Banners item after Aliases.
    - Banners allow sites to display sitewide banners.
    - Banner feature code lives in `Aero.Cms.Modules.Banners`.
    - Banners are site-owned and need `SiteId`.
    - Create the banner API in `Aero.Cms.Modules.Headless`.
    - Add an API client for the banners API.
    - Add a FluentValidation validator for the banner model/request.

- Navigation (NavMenu) Module
    - Add a new NavMenu block registered with source generators.
    - Navigation/menu records are site-owned and need `SiteId`.
    - API and supporting code should follow the Aero CMS module creation skill.

- Global Settings
    - Under the main left-side menu, add a Settings button anchored at the bottom.
    - Global settings items are TBD.
    - Global settings are instance-level, not site-level, unless a setting is explicitly moved into Site Settings.

- Databases Menu
    - Remove the left-hand Databases menu item.
    - It is not used.

- Taxonomy Menu Item
    - Keep only two submenu items:
        - Categories
        - Tags
    - Remove the General option.
    - Implement APIs for categories and tags in `Aero.Cms.Modules.Taxonomy`.
    - Categories and tags are site-owned and need `SiteId`.
    - Category/tag uniqueness should be site-scoped, such as `(SiteId, Slug)` or `(SiteId, Name)` depending on the final business rule.

## Cross-Module SiteId Work

After adding `SiteId` to Pages, Posts, Docs, and other site-owned features:

- Ensure `SeedDataService.cs` creates and retains a tenant and a default site.
- Ensure seeded records use the created default site id.
- Update dependent modules and models with `long SiteId` where data is owned by a site.
- Update list/search/query endpoints to filter by `SiteId`.
- Update create/update/delete endpoints to derive `SiteId` from the resolved current site context.
- Update events, cache invalidation, sitemap/search projections, and alias generation to include `SiteId`.
- Review existing global uniqueness rules and convert them to site-scoped composite uniqueness where appropriate.
