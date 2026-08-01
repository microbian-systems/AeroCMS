
> [!IMPORTANT]
> **STORAGE SUPERSEDED — MARTEN IS NO LONGER USED.** The backend database is now
> **SurrealDB via AeroDB.Sable** (embedded SurrealKV or remote server). Marten
> was migrated out in [`surrealdb-marten-port.md`](surrealdb-marten-port.md).
> This document is a historical implementation record; its Marten/PostgreSQL
> persistence details do not reflect the current stack.

# Footer Builder Implementation Spec

## Status

Draft for implementation.

> [!NOTE]
> This document was derived from `docs/nav-menu-implementation.md`, but it is
> not a find/replace of the header navigation feature. A footer is still
> site/layout scoped and benefits from the same Marten event-sourced draft and
> publish workflow, but the persisted model, editor surface, and renderer should
> be footer-first.
>
> Keep Marten as the version history. Do not create a separate
> `FooterVersionDocument` unless a future requirement needs a queryable index of
> historical versions across all footers. `FooterDraftSaved` and
> `FooterPublished` event payloads carry immutable `FooterSnapshot` values, and
> Marten stream sequence/version numbers are the revision history.

## Purpose

Build a site-scoped footer builder for AeroCMS. The builder lives in the
manager UI, lets editors create and publish responsive public-site footers, and
lets the site layout render the active published footer under page content.

The footer should support the same core editorial workflow as the header menu:

- create a named footer for the selected site
- save drafts without changing public output
- publish explicitly
- set a site default footer
- archive old footers without destroying event history
- invalidate public render caches when a published footer changes

Footer-specific additions:

- background image URL with overlay/style controls
- brand/logo block
- copyright/legal text
- link columns
- social links
- optional newsletter/search/contact calls to action
- bottom bar content such as privacy, terms, and cookie links

This document is written for an AI agent implementing the feature. Treat it as
the implementation contract unless a newer task document supersedes it.

## Repo Constraints

Follow `AGENTS.md` and existing AeroCMS patterns:

- Use ASP.NET Core minimal APIs.
- Prefer Blazor/Razor and Radzen in the manager UI.
- Do not use npm.
- Use MartenDB for persisted CMS documents unless relational/Identity behavior
  is required.
- Use `long` IDs for persisted entities and generate IDs with
  `Snowflake.NewId()`.
- Site-owned records must have explicit `long SiteId`.
- Do not trust client-supplied `SiteId` for standard manager writes. Derive the
  site from the current manager/site context.
- Use `System.Text.Json`, not Newtonsoft.Json.
- Use FluentValidation for request validation.
- Use Aero.Core railway result types for business/data access flows.
- Preserve draft vs. published behavior.
- Use code-behind for non-trivial Blazor behavior.
- Preserve the existing manager shell/theme.
- Keep page documents lean. A footer is site/layout scoped; pages should only
  hide or optionally override the resolved footer.

## Current Repo Anchors

The repo already has a few footer-related concepts:

- `PageDocument.HideFooter` already exists and is editable through the page
  editor metadata flow.
- `docs/content-type-implementation.md` includes a reusable footer block
  example with company name, logo, link columns, and social links.
- `docs/aero_cms_theming_roadmap.md` references a layout slot named
  `_Footer.cshtml`.
- `PageDocument.HeaderImageUrl` exists for page header/hero imagery. Do not
  reuse that field for footer background imagery; the footer needs its own
  site-level background setting.

These are anchors, not final architecture. The footer builder should become the
site-level owner of the public footer, while `HideFooter` remains the page-level
escape hatch.

## Design Summary

Use a site-owned footer aggregate with a draft/published event stream and inline
read projection.

Core model:

- `FooterDocument`: site-owned current read model for footer metadata and latest
  draft/published snapshots.
- `FooterSnapshot`: immutable renderable structure for one saved/published
  state.
- `SiteFooterSettingsDocument`: one per site, owns the default footer
  relationship.
- Optional future `PageDocument.FooterOverrideId`: nullable page override. V1 can
  skip this and use only `PageDocument.HideFooter` plus the site default.

Event-sourced write model:

- `FooterCreated`: starts `footer-{id}` and materializes `FooterDocument`.
- `FooterDraftSaved`: records a draft snapshot in the footer stream.
- `FooterPublished`: records an immutable published snapshot in the footer
  stream and updates the current published projection.
- `FooterArchived`: removes the footer from public resolution.
- `SiteDefaultFooterChanged`: appends to `site-footer-settings-{siteId}` and
  materializes `SiteFooterSettingsDocument`.

Rendering resolution:

1. If `PageDocument.HideFooter` is `true`, render nothing.
2. If a future page-level footer override exists, use that published footer for
   the same site.
3. Otherwise use the site default published footer.
4. If no published footer resolves, render nothing or a minimal placeholder only
   in manager preview.

Cache invalidation:

- Draft saves do not invalidate public caches.
- Publish, archive, and default changes publish a footer-changed integration
  event.
- Cache consumers evict rendered page/list output where the footer may already
  be embedded.

## What To Remove From The Header Spec

The following header-specific concepts should not be carried into the footer
implementation as-is:

- `NavMenu`, `NavLink`, `NavSearch`, `INavMenuComponent` names.
- Left/center/right header alignment as the domain model.
- Sticky header settings.
- Mobile hamburger menu as a primary behavior.
- Header `SiteLogoUrl` as the only brand image field.
- Page-owned embedded navigation components.
- Header-specific routes such as `/manager/navigations`.
- Search as a required component.
- Dropdown/flyout depth as a central V1 concern.

Footer equivalents are allowed where they fit:

- link columns instead of header nav buckets
- social links instead of header utility items
- responsive stacked columns instead of hamburger behavior
- footer logo URL and background image URL instead of header logo alone
- newsletter/search/contact call-to-action as optional components

## Non-Goals For First Slice

Do not implement these in the first slice:

- Arbitrary editor-defined HTMX endpoint URLs.
- Arbitrary editor-defined CSS selectors.
- Arbitrary raw JavaScript.
- Raw HTML rendering without a sanitizer and explicit allow-list.
- Scriban templates.
- Feature-flag, schedule, or audience visibility engines.
- Runtime plugin loading.
- Full theme-builder integration.

Add extension points where useful, but do not block the basic footer builder on
advanced content.

## Domain Language

- **Footer**: The named site-level footer container.
- **Snapshot**: The immutable renderable footer structure saved in an event.
- **Section**: A footer area such as brand, columns, social, newsletter, legal,
  or bottom bar.
- **Component**: A renderable piece inside a section, such as a link group or
  rich text block.
- **Draft**: Editable state visible in the manager/editor preview only.
- **Published footer**: The immutable state used for public rendering.
- **Default footer**: The site-level footer used when a page does not hide or
  override it.
- **Page hide flag**: `PageDocument.HideFooter`; when true, no footer renders.
- **Background image**: An optional image URL rendered behind the footer with
  safe overlay/style tokens.

## Aggregate Boundaries

### FooterDocument

The aggregate root and inline projection for a site-owned footer.

Responsibilities:

- Own footer identity, name, key, description, lifecycle state, and current
  draft/published snapshots.
- Enforce simple invariants that do not require cross-aggregate reads.
- Keep audit metadata for the latest manager change.
- Expose the current read shape for manager list/detail screens.

It does not own:

- Site default assignment. That belongs to `SiteFooterSettingsDocument`.
- Page hide/override assignment. That belongs to `PageDocument`.
- Public cache invalidation. That belongs to infrastructure consumers.

### FooterSnapshot

The renderable structure saved inside `FooterDraftSaved` and
`FooterPublished` events.

Rules:

- Snapshots are immutable value objects.
- Published snapshots must never be edited in place.
- Public rendering must only use the current published snapshot.
- Manager preview may explicitly render a draft snapshot.
- Snapshot data must remain serialization-friendly with `System.Text.Json`.

Suggested shape:

```csharp
public sealed record FooterSnapshot
{
    public FooterBrandSettings Brand { get; init; } = new();
    public FooterLayoutSettings Layout { get; init; } = FooterLayoutSettings.Default;
    public FooterStyleSettings Style { get; init; } = FooterStyleSettings.Default;
    public FooterResponsiveSettings Responsive { get; init; } = FooterResponsiveSettings.Default;
    public List<IFooterComponent> Sections { get; init; } = [];
}
```

### SiteFooterSettingsDocument

Owns the default footer reference for a site.

Rules:

- One settings document per site.
- `DefaultFooterId` must point to a footer in the same site.
- A footer can only become default if it has a published snapshot.
- Clearing the default footer is allowed and means "render no site footer"
  unless the page has an explicit override in a future phase.

### PageDocument

Keep existing:

```csharp
public bool HideFooter { get; set; } = false;
```

Optional future field:

```csharp
public long? FooterOverrideId { get; set; }
```

Rules:

- `HideFooter == true` wins over the site default.
- `FooterOverrideId == null` means "Use site default".
- A non-null override must reference a published footer in the same site when it
  is selected in the manager UI.
- Rendering should fall back gracefully if the selected override later becomes
  archived or unpublished.

## Footer Component Model

Use a small, closed set of footer components for V1. Keep the data model
serialization-friendly with `System.Text.Json` polymorphism.

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(FooterLinkGroup), "linkGroup")]
[JsonDerivedType(typeof(FooterTextBlock), "text")]
[JsonDerivedType(typeof(FooterSocialLinks), "social")]
[JsonDerivedType(typeof(FooterNewsletterSignup), "newsletter")]
[JsonDerivedType(typeof(FooterSearch), "search")]
[JsonDerivedType(typeof(FooterSpacer), "spacer")]
public interface IFooterComponent
{
    string Key { get; }
    int Order { get; }
    FooterSectionPlacement Placement { get; }
}
```

Built-in V1 components:

- `FooterLinkGroup`: title plus links.
- `FooterTextBlock`: safe plain text or sanitized markdown-rendered content.
- `FooterSocialLinks`: platform plus URL list.
- `FooterNewsletterSignup`: configured endpoint key, placeholder, and button
  label. Do not accept arbitrary endpoint URLs in V1.
- `FooterSearch`: optional route/search endpoint key for sites that want footer
  search.
- `FooterSpacer`: visual spacing token.

Footer-specific data:

```csharp
public sealed record FooterBrandSettings
{
    public string? LogoUrl { get; init; }
    public string? LogoAltText { get; init; }
    public string CompanyName { get; init; } = string.Empty;
    public string? Tagline { get; init; }
}

public sealed record FooterStyleSettings
{
    public string? BackgroundColorToken { get; init; }
    public string? TextColorToken { get; init; }
    public string? AccentColorToken { get; init; }
    public string? BackgroundImageUrl { get; init; }
    public string BackgroundImageMode { get; init; } = "cover";
    public string? OverlayColorToken { get; init; }
    public decimal OverlayOpacity { get; init; } = 0.35m;
    public string PaddingToken { get; init; } = "footer";
}

public sealed record FooterLegalSettings
{
    public string? CopyrightText { get; init; }
    public bool AutoAppendCurrentYear { get; init; } = true;
    public List<FooterLink> LegalLinks { get; init; } = [];
}
```

Prefer known design tokens over arbitrary CSS strings. URLs should be validated
as relative URLs or absolute HTTP/HTTPS URLs.

## Project Structure

Create a normal AeroCMS module:

```text
src/Aero.Cms.Modules.Footer/
  FooterModule.cs
  Domain/
    FooterDocument.cs
    SiteFooterSettingsDocument.cs
    FooterSnapshot.cs
    FooterComponents.cs
    FooterLayout.cs
    FooterEvents.cs
  Services/
    IFooterService.cs
    FooterService.cs
    IFooterResolver.cs
    FooterResolver.cs
  Rendering/
    FooterContext.cs
    AeroFooterViewComponent.cs
    FooterRenderer.cs
  Areas/Api/v1/
    FooterAdminApi.cs
    FooterPublicApi.cs
  Serialization/
    FooterJsonContext.cs
  Validators/
    FooterRequestValidators.cs

src/Aero.Cms.Abstractions/
  Http/Clients/FootersClient.cs
  Models/Footer/
    FooterContracts.cs

src/Aero.Cms.Shared/
  Pages/Manager/Footers.razor
  Pages/Manager/Footers.razor.cs
  Pages/Manager/CreateFooterDialog.razor
  Pages/Manager/CreateFooterDialog.razor.cs
  Pages/Manager/FooterEditor.razor
  Pages/Manager/FooterEditor.razor.cs
  Components/Footer/
    FooterCanvas.razor
    FooterPropertiesPanel.razor
    FooterPreview.razor
```

If the module creation skill is used later, adapt this to the generated module
layout.

## API Shape

Manager API:

```text
GET    /api/v1/admin/footers?skip=0&take=20&search=
GET    /api/v1/admin/footers/{id:long}
POST   /api/v1/admin/footers
PUT    /api/v1/admin/footers/{id:long}/draft?expectedVersion={version}
POST   /api/v1/admin/footers/{id:long}/publish
POST   /api/v1/admin/footers/{id:long}/archive
POST   /api/v1/admin/footers/default
```

Public API, if needed:

```text
GET    /api/v1/footer/default/render
GET    /api/v1/footer/{key}/render
```

Prefer layout/ViewComponent rendering for public site pages. Public render
endpoints are optional and should not expose drafts.

Contracts should include:

- `CreateFooterRequest`
- `UpdateFooterDraftRequest`
- `PublishFooterRequest`
- `SetDefaultFooterRequest`
- `FooterSummary`
- `FooterDetail`
- `FooterSnapshot`

## Marten And Serialization

Use stream keys:

```text
footer-{footerId}
site-footer-settings-{siteId}
```

Projection rules:

- `FooterDocumentProjection` processes only `footer-{id}` streams.
- `SiteFooterSettingsProjection` processes only `site-footer-settings-{siteId}`
  streams.
- Do not extract a footer ID from a site-settings stream or a site ID from a
  footer stream. Inline projections receive the full pending event batch, so
  stream-type filtering is required.

Serializer rules:

- Add `FooterJsonContext`.
- Register `FooterSnapshot`, all concrete footer component types, request/response
  contracts, and supporting DTOs.
- Compose the footer JSON context with the existing AeroCMS/Marten resolver
  chain. Do not replace the block/nav JSON resolver with a footer-only resolver.
- Set/keep `AllowOutOfOrderMetadataProperties = true` where polymorphic payloads
  need it.

## Manager UI Requirements

Add a manager left-hand menu item:

- Label: `Footer`
- Suggested route: `/manager/footers`
- Keep styling consistent with the existing manager shell.

Routes:

```text
/manager/footers
/manager/footers/editor/{id:long}
```

List page:

- Use a Radzen grid.
- Keep it separate from `FooterEditor.razor`, matching Posts, Pages, and Nav
  Menu.
- Columns: name, key, state, default indicator, updated date, published date,
  actions.
- `New Footer` opens a modal dialog for `Name` and `Description`.
- After OK, call the Footer API, read the returned `long` ID, and navigate to
  `/manager/footers/editor/{id}`.

Editor:

- Central footer canvas.
- Right-hand palette for footer components.
- Properties panel for selected footer/component.
- Preview controls for desktop/tablet/mobile.
- Header actions: save draft, publish, set default, archive.
- Section controls for brand, columns, social, newsletter/search, legal/bottom
  bar.
- Background controls:
  - background color token
  - background image URL
  - image mode: cover, contain, repeat
  - overlay color token
  - overlay opacity

The editor should not present a header-menu canvas. Use footer regions such as:

- Brand area
- Main link columns
- Utility/call-to-action area
- Bottom bar/legal area

## Rendering Requirements

Rendering is server-side first.

The public renderer must:

- Render semantic `<footer>`.
- Render nested `<nav aria-label="Footer">` elements for link groups.
- Use the resolved published snapshot.
- Respect `PageDocument.HideFooter`.
- Use safe Tailwind/design-token classes generated from known tokens.
- Render responsive stacked columns.
- Render background images with safe inline style or generated class output.
- Include accessible alt text for footer logos.
- Avoid draft state.
- Avoid arbitrary raw class strings.

Renderer flow:

```text
_CmsLayout.cshtml
  -> @await Component.InvokeAsync("AeroFooter")
      -> IFooterResolver.ResolveAsync(siteId, pageId/page context)
          -> FooterSnapshot?
      -> FooterRenderer.Render(snapshot)
```

The layout should not contain Marten query logic. Keep loading/resolution inside
the ViewComponent/resolver layer.

## Cache Invalidation

Add a public integration event:

```csharp
public sealed record FooterChangedEvent(
    long FooterId,
    long SiteId,
    FooterChangeKind ChangeKind,
    DateTimeOffset ChangedOn);

public enum FooterChangeKind
{
    Published,
    DefaultChanged,
    Archived
}
```

Publish this event only after the Marten write succeeds.

Consumers should invalidate:

- public page output cache
- blog index/detail output cache if the layout includes the footer
- docs index/detail output cache if the layout includes the footer
- any FusionCache entries that store resolved footer snapshots

Do not invalidate public caches on draft save.

## Seeder Requirements

Seed an initial default footer along with seeded CMS content.

Starter footer should include:

- brand/company name from site settings or `Aero CMS`
- optional logo URL empty by default
- columns:
  - Company: About, Contact
  - Content: Blog, Docs
  - Legal: Privacy, Terms, Cookies
- social links empty by default
- copyright text:
  - `© {currentYear} Aero CMS. All rights reserved.`
- background image URL empty by default
- background token using the current site/theme footer default

The seed should:

- create a `footer-{id}` stream
- append created, draft saved, and published events
- append `site-footer-settings-{siteId}` with the default footer ID
- be idempotent by site/key

## Compatibility With Reusable Blocks

`docs/content-type-implementation.md` includes a reusable footer block example.
That example is still useful, but the site footer builder should be the public
site-level owner for the main footer.

Compatibility path:

1. Build the dedicated footer aggregate and renderer first.
2. Keep the content-type footer block example as a pattern for future reusable
   footer fragments.
3. If the content-type system matures into global layout blocks, add an adapter
   that can render a published `FooterSnapshot` as a block or import a block
   instance into a `FooterSnapshot`.

Do not duplicate footer content onto every page.

## Testing Strategy

Use TUnit for unit tests and Alba for minimal API integration tests. Use
Playwright for manager UI and public rendering checks when the UI is built.

Unit tests:

- Create footer normalizes key and stamps site/user metadata.
- Duplicate `(SiteId, Key)` is rejected.
- Same key is allowed on different sites.
- Draft save uses Marten optimistic concurrency.
- Publish makes the snapshot public.
- Draft save after publish does not change public rendering.
- Archived footer cannot become default.
- Background image URL validation accepts relative and HTTP/HTTPS URLs.
- Background image URL validation rejects script/data URLs.
- `FooterSnapshot` round-trips every V1 component subtype.
- `HideFooter` suppresses rendering.

Integration tests:

- Manager list only returns current-site footers.
- Manager get/update rejects cross-site IDs.
- Public render uses only published footer.
- Setting default for Site A does not affect Site B.
- Publishing/default/archive emits `FooterChangedEvent`.
- Cache invalidation consumer evicts public output cache tags.

UI tests:

- `/manager/footers` lists real footers.
- New footer modal creates and navigates to editor.
- Editor can add/edit/reorder link groups.
- Editor can configure background image and overlay.
- Save draft preserves snapshot.
- Publish updates public render.
- Page editor `Hide Footer` hides the footer.
- Mobile preview stacks columns without overlapping controls.

## Verification Commands

Adjust project names if the final module/test names differ.

```powershell
dotnet build .\src\Aero.Cms.Modules.Footer\Aero.Cms.Modules.Footer.csproj --no-restore
dotnet build .\src\Aero.Cms.Shared\Aero.Cms.Shared.csproj --no-restore
dotnet test .\tests\Aero.Cms.Modules.Footer.Tests\Aero.Cms.Modules.Footer.Tests.csproj --no-restore
```

If the full solution is noisy because of unrelated source-generator or package
warnings, use focused builds/tests and document unrelated failures.

## Implementation Phases

### Phase 1: Domain And Persistence

- Add footer module project.
- Add footer event records and stream naming helpers.
- Add `FooterDocument`, `FooterSnapshot`, and `SiteFooterSettingsDocument`.
- Add footer component records.
- Add inline Marten projections.
- Add Marten mappings and indexes.
- Add `FooterJsonContext` and resolver composition.
- Add validators.
- Add unit tests for aggregate invariants and snapshot serialization.

Acceptance:

- Footer documents persist with `long` IDs.
- Writes append typed Marten events.
- Inline projections materialize current footer/settings read models.
- Draft/publish state transitions are tested.

### Phase 2: Manager API

- Add manager minimal APIs.
- Add typed abstraction/client contracts.
- Add current-site scoping.
- Add optimistic concurrency.
- Add default-footer endpoint.

Acceptance:

- Manager endpoints derive `SiteId` from context.
- Cross-site access fails.
- Stale draft saves return conflict.

### Phase 3: Builder UI

- Add `/manager/footers` Radzen grid.
- Add create-footer dialog.
- Add `FooterEditor.razor` with code-behind.
- Add canvas, palette, properties panel, preview, and footer settings tabs.
- Add background image controls.

Acceptance:

- Editor can create, save draft, publish, set default, and archive.
- UI preserves existing manager shell/theme.

### Phase 4: Public Rendering

- Add resolver and ViewComponent.
- Render footer from layout through resolver.
- Respect `PageDocument.HideFooter`.
- Add cache invalidation event and consumer.
- Seed starter footer.

Acceptance:

- Public pages render the site default published footer.
- Hidden-footer pages render no footer.
- Published/default/archive changes invalidate public render caches.

### Phase 5: Advanced Content

- Add registered provider-based newsletter/contact/search actions.
- Add sanitized rich text/markdown component.
- Add optional page-level footer override.
- Consider adapter to global reusable block/content-type system.

Acceptance:

- Editors cannot store arbitrary executable behavior.
- Rich text is sanitized before public rendering.
- Page override remains nullable and site-scoped.

## Open Decisions

Ask before implementing if not already documented:

- Should V1 include page-level `FooterOverrideId`, or is `HideFooter` plus site
  default enough for now?
- Should newsletter signup be included in V1, and which registered endpoint key
  should it use?
- Should social icons use a fixed platform allow-list?
- Should the footer background image use the media library picker only, or also
  allow manual URL entry?
- Should footer styling tokens come from the current theme system immediately or
  start as a small fixed allow-list?

## References

- `AGENTS.md`
- `docs/nav-menu-implementation.md`
- `docs/content-type-implementation.md`
- `docs/aero_cms_theming_roadmap.md`
- `docs/page-hierarchy-implementation.md`
- `marten-llms-full.txt`
