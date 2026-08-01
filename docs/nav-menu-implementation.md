# Nav Menu Builder Implementation Spec

## Status

Draft for implementation.

> [!IMPORTANT]
> **STORAGE SUPERSEDED — MARTEN IS NO LONGER USED.** The backend database is now
> **SurrealDB via AeroDB.Sable** (embedded SurrealKV or remote server). See
> [`surrealdb-marten-port.md`](surrealdb-marten-port.md). Event-sourcing here is
> AeroDB.Sable event streams, not Marten.

> [!NOTE]
> Current event-sourcing decision: do not create or write
> `NavMenuVersionDocument`. Marten event streams are the version history for
> navigation menus. `NavMenuDraftSaved` and `NavMenuPublished` event payloads
> carry the immutable `NavMenuSnapshot`; stream versions replace the earlier
> `Revision`, `MenuRevision`, `DraftVersionId`, and `PublishedVersionId`
> fields. `NavMenuDocument` remains an inline projection for the current read
> model, and `SiteNavigationSettingsDocument` remains the site/layout-scoped
> owner of the default nav-menu relationship.
>
> The current snapshot model is component based: `NavMenuSnapshot` has `Left`,
> `Center`, and `Right` buckets of `INavMenuComponent`. Built-in components are
> `NavLink`, `NavMenu`, `NavHtml`, and `NavSearch`, discriminated with
> `System.Text.Json` polymorphism. Renderer substitution belongs at the renderer
> layer, not in the persisted model. Page documents should not embed nav
> components; if a page-level override is later needed, model it as a nullable
> nav-menu ID override that resolves before the site default.
>
> Public rendering should resolve navigation outside the page and outside the
> layout. The layout invokes an `AeroNavBar` ViewComponent, the ViewComponent
> resolves `page override -> site default -> no menu` through `INavMenuService`,
> and a scoped `NavMenuContext` carries the resolved snapshot to renderer code.
> This keeps `PageDocument` lean, keeps `_CmsLayout.cshtml` out of Marten/query
> logic, and leaves recursive component rendering behind a renderer/visitor
> seam rather than scattered through the layout.

## Purpose

Build a site-scoped navigation menu builder for AeroCMS. The builder lives in the
manager UI, lets editors create and publish responsive public-site navigation
menus, and lets pages either inherit the site default menu or override it with a
specific published menu.

This document is written for an AI agent implementing the feature. Treat it as
the implementation contract unless a newer task document supersedes it.

## Repo Constraints

Follow `AGENTS.md` and existing AeroCMS patterns:

- Use ASP.NET Core minimal APIs.
- Prefer Blazor/Razor and Radzen in the manager UI.
- Do not use npm.
- Use MartenDB for persisted CMS documents unless relational/Identity behavior
  is required.
- Use `long` IDs for persisted entities and generate IDs with `Snowflake.NewId()`.
- Do not use GUID primary keys for persisted CMS entities.
- Site-owned records must have explicit `long SiteId`.
- Do not trust client-supplied `SiteId` for standard manager writes. Derive the
  site from the current manager/site context.
- Use `System.Text.Json`, not Newtonsoft.Json.
- Use FluentValidation for request validation.
- Use Aero.Core railway result types where this feature adds business/data
  access flows.
- Preserve draft vs. published behavior. Editors must be able to continue
  changing a draft without changing the public navbar until they publish.
- Support role-based item visibility when a public user/member role context is
  available. The visibility model must be safe when no user is authenticated.
- Apply DDD lite: clear aggregate boundaries, value objects, and invariants, but
  do not add ceremony that does not protect the feature.
- Preserve the existing manager shell/theme. Extend `/manager/navigations`; do
  not build a separate backend portal shell.
- Advanced dynamic/template behavior comes last.

## Current Repo Anchors

The repo already has a simple navigation surface:

- Manager placeholder: `src/Aero.Cms.Shared/Pages/Manager/Navigations.razor`
- Existing simple block model: `src/Aero.Cms.Abstractions/Blocks/ConcreteBlocks.cs`
  contains `NavigationBlock`
- Existing simple renderer: `src/Aero.Cms.Shared/Blocks/Rendering/NavigationBlockRenderer.razor`
- Existing admin API: `src/Aero.Cms.Modules.Headless/Areas/Api/v1/NavigationsApi.cs`
- Existing page flags: `PageDocument.ShowInNavMenu` and
  `PageDocument.ShowHeaderNavigation`
- Existing manager nav entry: `src/Aero.Cms.Shared/Layout/NavMenu.razor`
- Manager left-hand navigation currently labels this area "Navigations". Rename
  that menu item to "Header Menu" while keeping the feature focused on public
  header navigation.

The current navigation API stores/query simple `NavigationBlock` documents
globally. This feature should replace or wrap that behavior with explicit
site-scoped navigation documents. Existing simple navigation blocks should be
migrated or adapted, not silently broken.

## Design Summary

Use a site-owned navigation aggregate with draft and published versions. The
implementation must follow the current `Aero.Cms.Modules.Pages` event-sourced
model: command services append Marten events, inline projections materialize the
read documents, and manager APIs call the service layer instead of mutating
documents directly.

Core model:

- `NavMenuDocument`: site-owned aggregate root metadata.
- `NavMenuVersionDocument`: draft/published snapshots.
- `NavMenuSnapshot`: immutable value object used for rendering and audit.
- `SiteNavigationSettingsDocument`: one per site, owns the default menu
  reference.
- `PageDocument.HeaderNavigationMenuId`: optional page-level override.

Event-sourced write model:

- `NavMenuCreated`: starts `nav-menu-{id}` and materializes
  `NavMenuDocument`.
- `NavMenuDraftSaved`: creates/replaces the editable draft snapshot and updates
  `DraftVersionId` plus revision metadata.
- `NavMenuPublished`: records an immutable published snapshot, updates
  `PublishedVersionId`, clears or supersedes the draft pointer, and invalidates
  render caches.
- `NavMenuArchived`: removes the menu from public resolution without deleting
  historical versions.
- `SiteDefaultNavMenuChanged`: starts or appends `site-nav-settings-{siteId}`
  and materializes `SiteNavigationSettingsDocument`.

Read model:

- `NavMenuDocument`, `NavMenuVersionDocument`, and
  `SiteNavigationSettingsDocument` are projected documents. They may be queried
  directly for manager lists and public resolution, but writes should append
  events first.
- Use custom `IProjection` implementations when the stream identity is a
  Snowflake `long`, mirroring `PageDocumentProjection`, because the repo uses
  stream keys like `page-{id}` rather than `Guid` identities.
- Keep `NavigationBlock` compatibility as an adapter/migration path only. Do
  not continue the global `NavigationBlock` admin API as the primary write
  model.

Rendering resolution:

1. If the page hides header navigation, render nothing.
2. If the page has a nav override, use that published menu for the same site.
3. Otherwise use the site default published menu.
4. If no published menu resolves, render nothing.

Implementation priorities:

1. Site-owned model, manager CRUD, draft/publish, default assignment.
2. Builder UI and public server-side rendering for links, dropdowns, search,
   spacer, divider.
3. Role-based item visibility when public/member roles are available.
4. Page override integration.
5. Caching and invalidation.
6. Advanced dynamic/template content.

## Non-Goals For First Slice

Do not implement these in the first slice:

- Arbitrary editor-defined HTMX endpoint URLs.
- Arbitrary editor-defined CSS selectors.
- Arbitrary raw HTML rendering.
- Scriban templates.
- Feature-flag, schedule, or audience visibility engines.
- GraphQL or public JSON render API.
- Collaborative editing or fractional indexing.
- Runtime plugin loading.

Add extension points where useful, but do not block the basic navigation builder
on these advanced features. Role-based visibility is not considered an advanced
visibility engine for this spec: add the data model and renderer evaluation in
the first implementation pass that has access to public user/member roles.

## Domain Language

- **Menu**: The named navigation container for a site.
- **Version**: A saved draft or published snapshot of a menu.
- **Snapshot**: The immutable renderable structure for one version.
- **Item**: A logical navigation element, such as link, dropdown, search,
  divider, or spacer.
- **Draft**: An editable version that is visible in the manager/editor preview
  only.
- **Published version**: An immutable version that public rendering can use.
- **Layout slot**: Presentation placement such as left, center, right. This
  replaces "bucket" language in the domain. The editor can still present these
  as buckets.
- **Default menu**: The site-level menu used when a page does not override it.
- **Override menu**: A page-selected published menu.
- **Role visibility**: A rule on a menu item that limits rendering to users with
  one or more allowed roles.

## Aggregate Boundaries

### NavMenuDocument

The aggregate root for a site-owned menu.

Responsibilities:

- Own menu identity, name, key, lifecycle state, and current draft/published
  version references.
- Enforce simple invariants that do not require cross-aggregate reads.
- Coordinate draft replacement and publish state.
- Keep audit metadata for the latest manager change.

It does not own:

- Site default assignment. That belongs to `SiteNavigationSettingsDocument`.
- Page override assignment. That belongs to `PageDocument`.
- Public rendering caches. That belongs to infrastructure services.

### NavMenuVersionDocument

Stores a complete menu snapshot. Published versions are immutable.

Rules:

- Draft versions may be replaced atomically.
- Published versions must not be edited in place.
- Publishing a draft creates a new published version or promotes a draft copy,
  then updates `NavMenuDocument.PublishedVersionId`.
- Public rendering must only use published versions.
- Saving a draft must not modify the current published version.
- Preview rendering may render draft versions only in authorized manager
  contexts.
- Publish must be explicit. A normal save operation must never publish
  implicitly.

### Draft And Published Workflow

Editors need a safe draft workflow because navigation changes are immediately
visible and high impact.

Rules:

- A newly created menu starts with a draft version and no public version.
- `Save Draft` updates or replaces the draft snapshot and increments the menu
  revision.
- `Publish` validates the draft, creates an immutable published version, updates
  `PublishedVersionId`, clears or supersedes the draft pointer, and invalidates
  render caches.
- After a menu has been published, later edits create a new draft while the
  previous published version continues to render publicly.
- Public endpoints and public layout rendering must never read
  `DraftVersionId`.
- Manager preview must explicitly choose draft or published mode.
- Setting a menu as default requires a published version.
- Assigning a page override requires a published version.
- Archiving a menu prevents new public resolution, but historical published
  version records remain available for audit.

### SiteNavigationSettingsDocument

Owns the default navigation reference for a site.

Use this instead of an `IsDefault` boolean scattered across many menu documents.
It keeps one source of truth while allowing the navigation module to stay
self-contained.

Rules:

- One settings document per site.
- `DefaultNavMenuId` must point to a menu in the same site.
- A menu can only become default if it has a published version.

### PageDocument

Add the optional page override:

```csharp
public long? HeaderNavigationMenuId { get; set; }
```

Keep existing:

```csharp
public bool ShowHeaderNavigation { get; set; } = true;
```

Rules:

- `HeaderNavigationMenuId == null` means "Use site default".
- A non-null override must reference a published menu in the same site when it
  is selected in the manager UI.
- Rendering still falls back if the selected menu later becomes unpublished,
  archived, or deleted.

## Project Structure

Create a normal AeroCMS module:

```text
src/Aero.Cms.Modules.Navigation/
  NavigationModule.cs
  Domain/
    NavMenuDocument.cs
    NavMenuVersionDocument.cs
    SiteNavigationSettingsDocument.cs
    NavMenuSnapshot.cs
    NavMenuItems.cs
    NavMenuLayout.cs
  Services/
    INavMenuService.cs
    NavMenuService.cs
    INavMenuResolver.cs
    NavMenuResolver.cs
    INavMenuRenderer.cs
    NavMenuRenderer.cs
    INavMenuCache.cs
    MemoryNavMenuCache.cs
  Areas/Api/v1/
    NavigationAdminApi.cs
    NavigationPublicApi.cs
  Validators/
    NavigationRequestValidators.cs

src/Aero.Cms.Abstractions/
  Http/Clients/NavigationsClient.cs
  Models/Navigation/
    NavigationContracts.cs

src/Aero.Cms.Shared/
  Pages/Manager/Navigations.razor
  Pages/Manager/Navigations.razor.cs
  Pages/Manager/CreateNavMenuDialog.razor
  Pages/Manager/CreateNavMenuDialog.razor.cs
  Pages/Manager/NavMenuEditor.razor
  Pages/Manager/NavMenuEditor.razor.cs
  Components/Navigation/
    NavMenuCanvas.razor
    NavMenuPropertiesPanel.razor
    NavMenuPreview.razor
```

If the module creation skill is used later, adapt this structure to the actual
generated module layout.

## Data Model Sketch

The following samples are intentionally implementation-oriented, but still
sketches. Match actual namespaces, existing base classes, and current
`Aero.Core.Railway` result APIs during implementation.

### Aggregate Documents

```csharp
using Aero.Cms.Abstractions.Interfaces;
using Aero.Core.Entities;
using Aero.Core.Snowflake;

namespace Aero.Cms.Modules.Navigation.Domain;

public sealed class NavMenuDocument : Entity, ISiteOwned
{
    public long SiteId { get; set; }
    public string Name { get; private set; } = string.Empty;
    public string Key { get; private set; } = string.Empty;
    public NavMenuLifecycleState State { get; private set; } = NavMenuLifecycleState.Draft;
    public long? DraftVersionId { get; private set; }
    public long? PublishedVersionId { get; private set; }
    public int Revision { get; private set; } = 1;
    public long? CreatedByUserId { get; set; }
    public long? ModifiedByUserId { get; set; }

    private NavMenuDocument()
    {
    }

    public static NavMenuDocument Create(long siteId, string name, string key, long? userId)
    {
        if (siteId <= 0) throw new ArgumentOutOfRangeException(nameof(siteId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key is required.", nameof(key));

        return new NavMenuDocument
        {
            Id = Snowflake.NewId(),
            SiteId = siteId,
            Name = name.Trim(),
            Key = NormalizeKey(key),
            State = NavMenuLifecycleState.Draft,
            CreatedByUserId = userId,
            CreatedOn = DateTimeOffset.UtcNow
        };
    }

    public void Rename(string name, long? userId)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));

        Name = name.Trim();
        Touch(userId);
    }

    public void SetDraftVersion(long versionId, int expectedRevision, long? userId)
    {
        EnsureRevision(expectedRevision);
        DraftVersionId = versionId;
        State = PublishedVersionId.HasValue ? NavMenuLifecycleState.PublishedWithDraft : NavMenuLifecycleState.Draft;
        Touch(userId);
    }

    public void Publish(long versionId, int expectedRevision, long? userId)
    {
        EnsureRevision(expectedRevision);
        PublishedVersionId = versionId;
        DraftVersionId = null;
        State = NavMenuLifecycleState.Published;
        Touch(userId);
    }

    public void Archive(int expectedRevision, long? userId)
    {
        EnsureRevision(expectedRevision);
        State = NavMenuLifecycleState.Archived;
        Touch(userId);
    }

    private void EnsureRevision(int expectedRevision)
    {
        if (expectedRevision != Revision)
        {
            throw new InvalidOperationException("Navigation menu was modified by another user.");
        }
    }

    private void Touch(long? userId)
    {
        Revision++;
        ModifiedBy = userId;
        ModifiedOn = DateTimeOffset.UtcNow;
    }

    private static string NormalizeKey(string key)
        => key.Trim().ToLowerInvariant();
}

public sealed class NavMenuVersionDocument : Entity, ISiteOwned
{
    public long SiteId { get; set; }
    public long NavMenuId { get; set; }
    public NavMenuVersionState State { get; set; }
    public int Revision { get; set; }
    public NavMenuSnapshot Snapshot { get; set; } = NavMenuSnapshot.Empty;
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedOn { get; set; }
    public long? CreatedBy { get; set; }
    public long? PublishedBy { get; set; }
    public string? ChangeNote { get; set; }
}

public sealed class SiteNavigationSettingsDocument : Entity, ISiteOwned
{
    public long SiteId { get; set; }
    public long? DefaultNavMenuId { get; private set; }
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public long? ModifiedBy { get; set; }

    public static SiteNavigationSettingsDocument Create(long siteId)
    {
        if (siteId <= 0) throw new ArgumentOutOfRangeException(nameof(siteId));

        return new SiteNavigationSettingsDocument
        {
            Id = Snowflake.NewId(),
            SiteId = siteId
        };
    }

    public void SetDefault(long? navMenuId, long? userId)
    {
        DefaultNavMenuId = navMenuId;
        ModifiedBy = userId;
        ModifiedOn = DateTimeOffset.UtcNow;
    }
}
```

### Snapshot And Items

Use a discriminated union style with concrete item records. Do not persist
arbitrary endpoint URLs, CSS selectors, or raw executable expressions.

```csharp
namespace Aero.Cms.Modules.Navigation.Domain;

public sealed record NavMenuSnapshot(
    NavMenuLayout Layout,
    NavMenuResponsiveSettings Responsive,
    NavMenuStyleSettings Style,
    IReadOnlyList<NavMenuItem> Items)
{
    public static NavMenuSnapshot Empty { get; } = new(
        NavMenuLayout.Default,
        NavMenuResponsiveSettings.Default,
        NavMenuStyleSettings.Default,
        []);

    public void Validate()
    {
        var positions = new HashSet<(string SlotKey, int Order)>();

        foreach (var item in Items)
        {
            item.Validate(depth: 0);

            if (!positions.Add((item.SlotKey, item.Order)))
            {
                throw new InvalidOperationException(
                    $"Duplicate top-level order {item.Order} in layout slot '{item.SlotKey}'.");
            }
        }
    }
}

public abstract record NavMenuItem
{
    public long Id { get; init; }
    public int Order { get; init; }
    public string SlotKey { get; init; } = NavLayoutSlots.Left;
    public NavItemVisibility Visibility { get; init; } = NavItemVisibility.Default;
    public string? CssClassToken { get; init; }

    public abstract NavMenuItemType Type { get; }

    public virtual void Validate(int depth)
    {
        if (Id <= 0) throw new InvalidOperationException("Nav item Id must be set.");
        if (Order < 0) throw new InvalidOperationException("Nav item order cannot be negative.");
        if (string.IsNullOrWhiteSpace(SlotKey)) throw new InvalidOperationException("SlotKey is required.");
    }
}

public sealed record NavLinkItem : NavMenuItem
{
    public override NavMenuItemType Type => NavMenuItemType.Link;
    public string Label { get; init; } = string.Empty;
    public NavLinkTarget Target { get; init; } = NavLinkTarget.InternalUrl;
    public long? PageId { get; init; }
    public string? Url { get; init; }
    public bool OpenInNewTab { get; init; }
    public string? IconKey { get; init; }
    public string? AriaLabel { get; init; }

    public override void Validate(int depth)
    {
        base.Validate(depth);

        if (string.IsNullOrWhiteSpace(Label))
        {
            throw new InvalidOperationException("Link label is required.");
        }

        if (Target == NavLinkTarget.InternalPage && (!PageId.HasValue || PageId.Value <= 0))
        {
            throw new InvalidOperationException("Internal page links require PageId.");
        }

        if (Target != NavLinkTarget.InternalPage && string.IsNullOrWhiteSpace(Url))
        {
            throw new InvalidOperationException("URL links require Url.");
        }
    }
}

public sealed record NavDropdownItem : NavMenuItem
{
    private const int MaxDepth = 2;

    public override NavMenuItemType Type => NavMenuItemType.Dropdown;
    public string Label { get; init; } = string.Empty;
    public IReadOnlyList<NavMenuItem> Children { get; init; } = [];
    public string? IconKey { get; init; }

    public override void Validate(int depth)
    {
        base.Validate(depth);

        if (string.IsNullOrWhiteSpace(Label))
        {
            throw new InvalidOperationException("Dropdown label is required.");
        }

        if (depth >= MaxDepth)
        {
            throw new InvalidOperationException($"Dropdown nesting cannot exceed {MaxDepth} levels.");
        }

        foreach (var child in Children)
        {
            child.Validate(depth + 1);
        }
    }
}

public sealed record NavSearchItem : NavMenuItem
{
    public override NavMenuItemType Type => NavMenuItemType.Search;
    public SearchDisplayMode DisplayMode { get; init; } = SearchDisplayMode.IconPopup;
    public SearchInputStyle InputStyle { get; init; } = SearchInputStyle.Rounded;
    public string PlaceholderText { get; init; } = "Search";
    public string? SearchEndpointKey { get; init; }
}

public sealed record NavDividerItem : NavMenuItem
{
    public override NavMenuItemType Type => NavMenuItemType.Divider;
}

public sealed record NavSpacerItem : NavMenuItem
{
    public override NavMenuItemType Type => NavMenuItemType.Spacer;
    public string WidthToken { get; init; } = "sm";
}
```

### Item Visibility

Visibility is stored as a serializable value object, not as arbitrary runtime
expressions. V1 supports breakpoint visibility and role-based visibility. More
complex audience, feature-flag, and schedule rules are future extensions.

Role visibility must be evaluated during rendering. If an item has allowed
roles and the current render context has no authenticated user or no matching
role, the item is omitted from the rendered menu. Children of a hidden dropdown
must not render.

```csharp
public sealed record NavItemVisibility(
    bool HideOnMobile,
    bool HideOnDesktop,
    IReadOnlyList<string> AllowedRoles,
    RoleVisibilityMode RoleMode)
{
    public static NavItemVisibility Default { get; } = new(
        HideOnMobile: false,
        HideOnDesktop: false,
        AllowedRoles: [],
        RoleMode: RoleVisibilityMode.Any);

    public bool HasRoleRules => AllowedRoles.Count > 0;
}

public enum RoleVisibilityMode
{
    Any,
    All
}
```

Implementation rules:

- Role names are string values because they come from the active auth/member
  system.
- Normalize role comparisons with `StringComparer.OrdinalIgnoreCase`.
- Do not evaluate role visibility in the manager save path. Save validates
  shape; render evaluates against the current user.
- Manager preview should offer an optional "Preview as role" selector once role
  visibility is implemented.
- If the public/member auth system is unavailable in a render context, only
  items with no role restrictions render.

### Later Advanced Item Types

These are intentionally deferred. Add them after the core builder/rendering path
is stable.

```csharp
public sealed record NavDynamicFragmentItem : NavMenuItem
{
    public override NavMenuItemType Type => NavMenuItemType.DynamicFragment;
    public string Label { get; init; } = string.Empty;
    public string DataSourceKey { get; init; } = string.Empty;
}

public sealed record NavTemplatedContentItem : NavMenuItem
{
    public override NavMenuItemType Type => NavMenuItemType.TemplatedContent;
    public string TemplateKey { get; init; } = string.Empty;
    public string? DataSourceKey { get; init; }
}
```

Advanced rules:

- `DataSourceKey` maps to registered providers only.
- Do not store full endpoint URLs in menu documents.
- Do not allow editor-supplied target selectors.
- Do not pass Marten documents or domain entities into Scriban.
- Use safe DTOs and strict template contexts.
- Cache parsed templates by template id/version/hash.

### Layout Value Objects

```csharp
namespace Aero.Cms.Modules.Navigation.Domain;

public static class NavLayoutSlots
{
    public const string Left = "left";
    public const string Center = "center";
    public const string Right = "right";
}

public sealed record NavMenuLayout(
    NavLayoutType Type,
    IReadOnlyList<NavLayoutSlot> Slots,
    string GapToken,
    NavContainerMode ContainerMode)
{
    public static NavMenuLayout Default { get; } = new(
        NavLayoutType.LeftCenterRight,
        [
            new NavLayoutSlot(NavLayoutSlots.Left, "Left", 0),
            new NavLayoutSlot(NavLayoutSlots.Center, "Center", 1),
            new NavLayoutSlot(NavLayoutSlots.Right, "Right", 2)
        ],
        GapToken: "md",
        ContainerMode: NavContainerMode.Contained);
}

public sealed record NavLayoutSlot(string Key, string Label, int Order);

public sealed record NavMenuResponsiveSettings(
    Breakpoint MobileBreakpoint,
    MobileMenuMode MobileMenuMode,
    bool CollapseSlotsOnMobile)
{
    public static NavMenuResponsiveSettings Default { get; } = new(
        Breakpoint.Md,
        MobileMenuMode.SlideOver,
        CollapseSlotsOnMobile: true);
}

public sealed record NavMenuStyleSettings(
    StickyMode StickyMode,
    int StickyOffsetPx,
    string ZIndexToken,
    string? ThemeToken)
{
    public static NavMenuStyleSettings Default { get; } = new(
        StickyMode.None,
        StickyOffsetPx: 0,
        ZIndexToken: "nav",
        ThemeToken: null);
}
```

### Enums

```csharp
public enum NavMenuLifecycleState
{
    Draft,
    Published,
    PublishedWithDraft,
    Archived
}

public enum NavMenuVersionState
{
    Draft,
    Published,
    Archived
}

public enum NavMenuItemType
{
    Link,
    Dropdown,
    Search,
    Divider,
    Spacer,
    DynamicFragment,
    TemplatedContent
}

public enum NavLayoutType
{
    LeftCenterRight,
    AllLeft,
    AllRight
}

public enum NavContainerMode
{
    FullWidth,
    Contained
}

public enum NavLinkTarget
{
    InternalPage,
    InternalUrl,
    ExternalUrl
}

public enum SearchDisplayMode
{
    IconPopup,
    InlineTextbox
}

public enum SearchInputStyle
{
    Rounded,
    Square
}

public enum MobileMenuMode
{
    SlideOver,
    Dropdown,
    FullScreenOverlay
}

public enum Breakpoint
{
    Sm,
    Md,
    Lg,
    Xl,
    Xxl
}

public enum StickyMode
{
    None,
    StickyTop,
    FixedTop
}

```

## Request And Response Contracts

Manager request contracts should not include `SiteId`. The endpoint/service must
derive current site from the active manager site context.

```csharp
namespace Aero.Cms.Abstractions.Models.Navigation;

public sealed record CreateNavMenuRequest(
    string Name,
    string Key,
    NavMenuLayout Layout,
    NavMenuResponsiveSettings Responsive,
    NavMenuStyleSettings Style);

public sealed record UpdateNavMenuDraftRequest(
    int ExpectedRevision,
    string Name,
    string Key,
    NavMenuLayout Layout,
    NavMenuResponsiveSettings Responsive,
    NavMenuStyleSettings Style,
    IReadOnlyList<NavMenuItem> Items,
    string? ChangeNote);

public sealed record PublishNavMenuRequest(
    int ExpectedRevision,
    string? ChangeNote);

public sealed record SetDefaultNavMenuRequest(long? NavMenuId);

public sealed record SetPageNavigationRequest(
    bool ShowHeaderNavigation,
    long? HeaderNavigationMenuId);

public sealed record NavMenuSummary(
    long Id,
    string Name,
    string Key,
    NavMenuLifecycleState State,
    bool IsDefault,
    int Revision,
    int ItemCount,
    DateTimeOffset CreatedOn,
    DateTimeOffset? ModifiedOn);

public sealed record NavMenuDetail(
    long Id,
    long SiteId,
    string Name,
    string Key,
    NavMenuLifecycleState State,
    int Revision,
    long? DraftVersionId,
    long? PublishedVersionId,
    bool IsDefault,
    NavMenuSnapshot Draft,
    NavMenuSnapshot? Published,
    DateTimeOffset CreatedOn,
    DateTimeOffset? ModifiedOn);
```

## Service Interfaces

Keep business logic out of minimal API handlers. Handlers should validate,
resolve current site/user, call services, and map results to HTTP responses.

```csharp
using Aero.Core.Railway;
using Aero.Cms.Abstractions.Models.Navigation;

namespace Aero.Cms.Modules.Navigation.Services;

public interface INavMenuService
{
    Task<Result<NavMenuDetail, AeroError>> CreateAsync(
        long siteId,
        CreateNavMenuRequest request,
        long? userId,
        CancellationToken ct = default);

    Task<Result<NavMenuDetail, AeroError>> GetAsync(
        long siteId,
        long navMenuId,
        CancellationToken ct = default);

    Task<Result<PagedResult<NavMenuSummary>, AeroError>> ListAsync(
        long siteId,
        int skip,
        int take,
        string? search,
        CancellationToken ct = default);

    Task<Result<NavMenuDetail, AeroError>> SaveDraftAsync(
        long siteId,
        long navMenuId,
        UpdateNavMenuDraftRequest request,
        long? userId,
        CancellationToken ct = default);

    Task<Result<NavMenuDetail, AeroError>> PublishAsync(
        long siteId,
        long navMenuId,
        PublishNavMenuRequest request,
        long? userId,
        CancellationToken ct = default);

    Task<Result<bool, AeroError>> ArchiveAsync(
        long siteId,
        long navMenuId,
        int expectedRevision,
        long? userId,
        CancellationToken ct = default);

    Task<Result<bool, AeroError>> SetDefaultAsync(
        long siteId,
        SetDefaultNavMenuRequest request,
        long? userId,
        CancellationToken ct = default);
}
```

## Resolver And Cache

Resolve a renderable published menu from site/page state. Cache published render
models, not editor drafts.

```csharp
namespace Aero.Cms.Modules.Navigation.Services;

public sealed record ResolvedNavMenu(
    long SiteId,
    long NavMenuId,
    long VersionId,
    NavMenuSnapshot Snapshot);

public interface INavMenuResolver
{
    Task<ResolvedNavMenu?> ResolveForPageAsync(
        PageDocument page,
        CancellationToken ct = default);

    Task<ResolvedNavMenu?> ResolveDefaultAsync(
        long siteId,
        CancellationToken ct = default);
}

public interface INavMenuCache
{
    Task<ResolvedNavMenu?> GetResolvedAsync(string key, CancellationToken ct = default);
    Task SetResolvedAsync(string key, ResolvedNavMenu menu, TimeSpan ttl, CancellationToken ct = default);
    Task InvalidateMenuAsync(long siteId, long navMenuId, CancellationToken ct = default);
    Task InvalidateSiteAsync(long siteId, CancellationToken ct = default);
    Task InvalidatePageAsync(long siteId, long pageId, CancellationToken ct = default);
}

public sealed class NavMenuResolver : INavMenuResolver
{
    private readonly IDocumentSession _session;
    private readonly INavMenuCache _cache;

    public NavMenuResolver(IDocumentSession session, INavMenuCache cache)
    {
        _session = session;
        _cache = cache;
    }

    public async Task<ResolvedNavMenu?> ResolveForPageAsync(PageDocument page, CancellationToken ct = default)
    {
        if (!page.ShowHeaderNavigation)
        {
            return null;
        }

        if (page.HeaderNavigationMenuId is { } overrideId)
        {
            var overrideMenu = await TryResolvePublishedAsync(page.SiteId, overrideId, ct);
            if (overrideMenu is not null)
            {
                return overrideMenu;
            }
        }

        return await ResolveDefaultAsync(page.SiteId, ct);
    }

    public async Task<ResolvedNavMenu?> ResolveDefaultAsync(long siteId, CancellationToken ct = default)
    {
        var settings = await _session.Query<SiteNavigationSettingsDocument>()
            .FirstOrDefaultAsync(x => x.SiteId == siteId, ct);

        return settings?.DefaultNavMenuId is { } menuId
            ? await TryResolvePublishedAsync(siteId, menuId, ct)
            : null;
    }

    private async Task<ResolvedNavMenu?> TryResolvePublishedAsync(long siteId, long navMenuId, CancellationToken ct)
    {
        var cacheKey = $"nav:published:{siteId}:{navMenuId}";
        var cached = await _cache.GetResolvedAsync(cacheKey, ct);
        if (cached is not null)
        {
            return cached;
        }

        var menu = await _session.LoadAsync<NavMenuDocument>(navMenuId, ct);
        if (menu is null || menu.SiteId != siteId || menu.PublishedVersionId is null)
        {
            return null;
        }

        var version = await _session.LoadAsync<NavMenuVersionDocument>(menu.PublishedVersionId.Value, ct);
        if (version is null || version.SiteId != siteId || version.State != NavMenuVersionState.Published)
        {
            return null;
        }

        var resolved = new ResolvedNavMenu(siteId, menu.Id, version.Id, version.Snapshot);
        await _cache.SetResolvedAsync(cacheKey, resolved, TimeSpan.FromMinutes(30), ct);
        return resolved;
    }
}
```

Cache invalidation triggers:

- publish menu
- archive menu
- set/clear site default
- page override update
- referenced page slug/url change

## Minimal API Shape

Use route groups and typed results. Microsoft Learn recommends `TypedResults`
for minimal API responses and route groups for common prefixes/metadata.

Manager endpoints:

```text
GET    /api/v1/admin/navigations?skip=0&take=20&search=
GET    /api/v1/admin/navigations/{id:long}
POST   /api/v1/admin/navigations
PUT    /api/v1/admin/navigations/{id:long}/draft
POST   /api/v1/admin/navigations/{id:long}/publish
POST   /api/v1/admin/navigations/{id:long}/archive
POST   /api/v1/admin/navigations/default
```

Public endpoints:

```text
GET    /api/v1/navigation/{key}/render
GET    /api/v1/navigation/default/render
```

Public endpoints return HTML fragments and only render published menus for the
resolved current site. Public endpoints must not expose draft data.

Endpoint sketch:

```csharp
public static class NavigationAdminApi
{
    public static void MapNavigationAdminApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/navigations")
            .WithTags("Admin - Navigations")
            .RequireAuthorization()
            .ProducesProblem()
            .ProducesValidationProblem();

        group.MapGet("/", ListAsync).WithName("ListNavMenus");
        group.MapGet("/{id:long}", GetAsync).WithName("GetNavMenu");
        group.MapPost("/", CreateAsync).WithName("CreateNavMenu");
        group.MapPut("/{id:long}/draft", SaveDraftAsync).WithName("SaveNavMenuDraft");
        group.MapPost("/{id:long}/publish", PublishAsync).WithName("PublishNavMenu");
        group.MapPost("/{id:long}/archive", ArchiveAsync).WithName("ArchiveNavMenu");
        group.MapPost("/default", SetDefaultAsync).WithName("SetDefaultNavMenu");
    }

    private static async Task<IResult> CreateAsync(
        CreateNavMenuRequest request,
        INavMenuService service,
        ICurrentSiteAccessor currentSite,
        IUserContext user,
        CancellationToken ct)
    {
        var siteId = await currentSite.GetCurrentSiteIdAsync();
        if (siteId is null)
        {
            return TypedResults.BadRequest(new { error = "No current site is selected." });
        }

        var result = await service.CreateAsync(siteId.Value, request, user.UserId, ct);
        return result.Match<IResult>(
            ok => TypedResults.Created($"/api/v1/admin/navigations/{ok.Id}", ok),
            error => error.ToProblemResult());
    }
}
```

The exact `Result<T>` matching helper should follow current AeroCMS railway
helpers. Do not create a second custom `Result<T>` implementation for this
feature. `IUserContext` in the sample is a placeholder for the repo's actual
authenticated-user accessor or authentication-state service.

## Validation

Use FluentValidation for manager request contracts and domain validation for
aggregate invariants.

Validation rules:

- `Name`: required, max 100.
- `Key`: required, max 50, lowercase slug pattern: `^[a-z0-9][a-z0-9-_]*$`.
- `Key`: unique per site.
- `Key`: stable after create for the first implementation slice. Existing
  compatibility requests can rename the menu display name, but they should not
  derive a new key from the display name unless a dedicated rename-key command
  with duplicate checks is added.
- `ExpectedRevision`: must equal current menu revision.
- `Items`: top-level item count should have a practical limit, such as 100.
- Dropdown depth: max 2.
- Internal page links: referenced page must exist and match current site.
- Page override: referenced menu must exist, match current site, and be
  published.
- Default menu: referenced menu must exist, match current site, and be
  published.
- External links: only `http` and `https`.
- CSS class tokens: allowlist tokens only, no arbitrary class string in V1.
- Role visibility: role names must be non-empty, trimmed, and limited to a
  practical maximum count per item, such as 20 roles.
- Public render: published menus only.

Validator sketch:

```csharp
public sealed class CreateNavMenuRequestValidator : AbstractValidator<CreateNavMenuRequest>
{
    public CreateNavMenuRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Key)
            .NotEmpty()
            .MaximumLength(50)
            .Matches("^[a-z0-9][a-z0-9-_]*$");

        RuleFor(x => x.Layout)
            .NotNull();

        RuleFor(x => x.Responsive)
            .NotNull();

        RuleFor(x => x.Style)
            .NotNull();
    }
}

public sealed class UpdateNavMenuDraftRequestValidator : AbstractValidator<UpdateNavMenuDraftRequest>
{
    public UpdateNavMenuDraftRequestValidator()
    {
        RuleFor(x => x.ExpectedRevision)
            .GreaterThan(0);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Key)
            .NotEmpty()
            .MaximumLength(50)
            .Matches("^[a-z0-9][a-z0-9-_]*$");

        RuleFor(x => x.Items)
            .NotNull()
            .Must(items => items.Count <= 100)
            .WithMessage("Navigation menu cannot contain more than 100 top-level items.");
    }
}
```

## Marten Configuration

Add configuration in `NavigationModule.cs` using `IConfigureMarten`.

```csharp
public sealed class NavigationModule : AeroWebModule, IUiModule, IConfigureMarten
{
    public override void Configure(IServiceProvider services, StoreOptions opts)
    {
        opts.Projections.Add(new NavMenuDocumentProjection(), ProjectionLifecycle.Inline);
        opts.Projections.Add(new SiteNavigationSettingsProjection(), ProjectionLifecycle.Inline);

        opts.Schema.For<NavMenuDocument>()
            .DocumentAlias("nav_menus")
            .Identity(x => x.Id)
            .Index(x => x.SiteId)
            .UniqueIndex(x => x.SiteId, x => x.Key)
            .Index(x => x.State)
            .Index(x => x.PublishedVersionId);

        opts.Schema.For<NavMenuVersionDocument>()
            .DocumentAlias("nav_menu_versions")
            .Identity(x => x.Id)
            .Index(x => x.SiteId)
            .Index(x => x.NavMenuId)
            .Index(x => x.State);

        opts.Schema.For<SiteNavigationSettingsDocument>()
            .DocumentAlias("site_navigation_settings")
            .Identity(x => x.Id)
            .UniqueIndex(x => x.SiteId)
            .Index(x => x.DefaultNavMenuId);
    }
}
```

The projection shape should mirror `PageDocumentProjection`, with one important
guard: each inline projection must first filter to the stream family it owns.
`NavMenuDocumentProjection` should only process `nav-menu-{id}` streams, and
`SiteNavigationSettingsProjection` should only process
`site-nav-settings-{siteId}` streams. Marten passes the full pending event batch
to inline projections, so extracting a site id from a `nav-menu-{id}` stream
will fail during create/save. After filtering, group by stream key, extract the
Snowflake `long`, load the existing aggregate in async projection mode, apply
event data, and store the materialized document. This keeps navigation
consistent with the current Pages event-sourcing implementation while keeping
menu and site-settings aggregates independent.

System.Text.Json polymorphism for item types must be configured consistently
with existing block JSON patterns. Prefer source-generated JSON metadata if the
module needs explicit serialization context.

## Serialization And Marten Polymorphism

This is a high-risk implementation area. Do not assume the `NavMenuItem`
discriminated union will round-trip through Marten just because it works with
plain `JsonSerializer.Serialize`.

Facts to account for:

- `NavMenuSnapshot.Items` is declared as `IReadOnlyList<NavMenuItem>`, so STJ
  must serialize and deserialize derived item types through the base
  `NavMenuItem` type.
- STJ polymorphism requires explicit derived-type registration and a type
  discriminator.
- Marten stores documents as PostgreSQL `jsonb`; property order is not
  guaranteed.
- Marten's own docs call out that `[JsonDerivedType]` polymorphism with jsonb
  requires `JsonSerializerOptions.AllowOutOfOrderMetadataProperties = true`.
  The local repo copy in `marten-llms-full.txt` confirms this applies to
  polymorphic types stored inside documents, which is exactly the
  `NavMenuSnapshot.Items` case.
- AeroCMS already uses that option in `GeneratedMartenConfiguration`.
- AeroCMS already has a known source-generator chaining limitation:
  `BlockRendererGenerator` can emit metadata and polymorphic base attributes,
  but it cannot safely emit `[JsonSerializable]` attributes and expect STJ's
  source generator to consume them in the same compilation.

Required implementation direction:

- Add a dedicated `NavMenuJsonContext` for navigation menu contracts.
- Register all snapshot, item, value object, enum, and collection types needed
  for nav menu persistence and API payloads.
- Configure polymorphic discriminators on `NavMenuItem`.
- Use a nav-specific discriminator property such as `$navItemType`.
- Enable `AllowOutOfOrderMetadataProperties = true` in the Marten serializer
  options used by AeroCMS.
- Compose the nav JSON context with the existing `BlockJsonContext`, do not
  replace the existing resolver with a nav-only resolver.
- Add an integration test that stores and reloads a menu containing every V1
  item type through Marten.

Example shape:

```csharp
using System.Text.Json.Serialization;

namespace Aero.Cms.Modules.Navigation.Domain;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$navItemType")]
[JsonDerivedType(typeof(NavLinkItem), "link")]
[JsonDerivedType(typeof(NavDropdownItem), "dropdown")]
[JsonDerivedType(typeof(NavSearchItem), "search")]
[JsonDerivedType(typeof(NavDividerItem), "divider")]
[JsonDerivedType(typeof(NavSpacerItem), "spacer")]
public abstract partial record NavMenuItem;
```

```csharp
using System.Text.Json.Serialization;
using Aero.Cms.Modules.Navigation.Domain;

namespace Aero.Cms.Modules.Navigation.Serialization;

[JsonSerializable(typeof(NavMenuDocument))]
[JsonSerializable(typeof(NavMenuVersionDocument))]
[JsonSerializable(typeof(SiteNavigationSettingsDocument))]
[JsonSerializable(typeof(NavMenuSnapshot))]
[JsonSerializable(typeof(List<NavMenuItem>))]
[JsonSerializable(typeof(NavMenuItem[]))]
[JsonSerializable(typeof(NavLinkItem))]
[JsonSerializable(typeof(NavDropdownItem))]
[JsonSerializable(typeof(NavSearchItem))]
[JsonSerializable(typeof(NavDividerItem))]
[JsonSerializable(typeof(NavSpacerItem))]
[JsonSerializable(typeof(NavItemVisibility))]
[JsonSerializable(typeof(NavMenuLayout))]
[JsonSerializable(typeof(NavLayoutSlot))]
[JsonSerializable(typeof(NavMenuResponsiveSettings))]
[JsonSerializable(typeof(NavMenuStyleSettings))]
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default | JsonSourceGenerationMode.Metadata)]
public partial class NavMenuJsonContext : JsonSerializerContext
{
}
```

The exact composition point depends on the final module wiring. The important
constraint is that navigation metadata must be added to the existing AeroCMS
Marten serializer pipeline. Do not call `UseSystemTextJsonForSerialization` in a
way that drops the block JSON resolver.

Preferred composition shape:

```csharp
options.UseSystemTextJsonForSerialization(configure: stj =>
{
    stj.TypeInfoResolver = JsonTypeInfoResolver.Combine(
        BlockJsonContext.Default,
        NavMenuJsonContext.Default);

    stj.AllowOutOfOrderMetadataProperties = true;
});
```

If a central AeroCMS JSON resolver registry exists by implementation time, add
`NavMenuJsonContext.Default` there instead of configuring Marten directly inside
the navigation module.

### Source Generator Guidance

Do not build a generator that manually emits a complete
`JsonSerializerContext`. That would reimplement STJ's source generator and is
too fragile.

Acceptable source-generator usage:

- Extend the existing block/source-generator pattern to emit a nav item manifest
  and diagnostics.
- Generate `[JsonDerivedType]` registrations on a partial `NavMenuItem` base if
  the implementation wants to avoid hand-maintaining discriminator attributes.
- Generate Marten mapped-type metadata if nav items are later stored as document
  subclass roots.

Not acceptable:

- Emitting `[JsonSerializable]` attributes from an AeroCMS generator and assuming
  STJ's generator will consume them in the same compilation.
- Replacing `NavMenuJsonContext` with a hand-emitted context implementation.

For V1, the lowest-risk path is a hand-authored `NavMenuJsonContext` plus tests.
The nav item hierarchy is small and closed, so this is not the same maintenance
burden as the larger CMS block system.

## Rendering Requirements

Rendering is server-side first.

The public renderer must:

- Render a semantic `<nav>`.
- Use the resolved published snapshot.
- Use Tailwind classes generated from known layout/style tokens.
- Render dropdown buttons with ARIA expanded state.
- Support keyboard navigation.
- Support touch-friendly dropdown behavior.
- Render a mobile menu toggle for configured breakpoints.
- Avoid arbitrary raw class strings.
- Avoid exposing draft state.

Renderer sketch:

```csharp
public interface INavMenuRenderer
{
    Task<string> RenderAsync(ResolvedNavMenu menu, NavRenderContext context, CancellationToken ct = default);
}

public sealed record NavRenderContext(
    long SiteId,
    long? PageId,
    string Culture,
    bool IsPreview,
    bool IsHtmxRequest,
    IReadOnlySet<string> UserRoles,
    bool IsAuthenticated);
```

Renderer role filtering sketch:

```csharp
public static bool IsVisible(NavMenuItem item, NavRenderContext context)
{
    var visibility = item.Visibility;

    if (visibility.HasRoleRules)
    {
        if (!context.IsAuthenticated)
        {
            return false;
        }

        var userRoles = new HashSet<string>(context.UserRoles, StringComparer.OrdinalIgnoreCase);
        return visibility.RoleMode == RoleVisibilityMode.All
            ? visibility.AllowedRoles.All(userRoles.Contains)
            : visibility.AllowedRoles.Any(userRoles.Contains);
    }

    return true;
}
```

Search behavior:

- V1 supports `IconPopup` and `InlineTextbox`.
- `SearchEndpointKey` maps to configured search behavior.
- Do not allow arbitrary search endpoint URLs in V1.

## Manager UI Requirements

Use existing manager styling and Radzen components.

### Manager Left-Hand Menu

Rename the existing manager left-hand menu item:

- From: `Navigations`
- To: `Header Menu`

Implementation target:

- Update `src/Aero.Cms.Shared/Layout/NavMenu.razor`.
- Preserve the existing icon/section style unless there is a clear local icon
  already used for menus/navigation.
- The route can remain `/manager/navigations` unless the user explicitly asks
  to change URLs. The visible manager label should be `Header Menu`.
- Header Menu is site-scoped. It manages the selected site's public header nav
  menus, not instance-level module navigation.

Routes:

```text
/manager/navigations
/manager/navigations/editor/{id:long}
/manager/nav-menu/editor/{id:long}
```

The list and editor should remain separate screens, matching the current Posts
and Pages manager flow. `Navigations.razor` is the Radzen grid/list page only.
Clicking a row navigates to `NavMenuEditor.razor` with the selected menu id.
Clicking `New Menu` opens a modal dialog for `Name` and `Description`; after OK,
the manager posts to the Navigation API, reads the returned `long` menu id, and
navigates to the editor route for that id.

### Editor Experience

The nav menu editor should function like the current page editor in
`src/Aero.Cms.Shared/Pages/Manager/PageEditor/PageEditor.razor`.

Match these interaction patterns:

- A main editable canvas in the center.
- A right-hand `AeroSidebar` for draggable building blocks/elements.
- Click-to-add and drag-to-add support from the right sidebar.
- Drag-and-drop reordering inside the canvas.
- Selected item toolbar with move, duplicate, and delete actions.
- Header actions for preview, save draft, publish, and archive/unpublish where
  applicable.
- Preview overlay/panel behavior should follow the PageEditor preview pattern
  where practical.
- Use code-behind for non-trivial behavior, consistent with repo guidance.

The nav editor canvas is not a full page canvas. It is a constrained header-menu
canvas:

- Show layout slots from `NavMenuLayout.Slots` as drop zones, such as left,
  center, and right.
- Dropping an element into a slot sets the item's `SlotKey`.
- Reordering within a slot normalizes item `Order`.
- Moving an item between slots updates both `SlotKey` and `Order`.
- Desktop/mobile preview toggles should show how slots collapse under the
  configured mobile breakpoint.

Right sidebar palette for V1:

- Link
- Dropdown
- Search
- Spacer
- Divider
- Text/Label, if needed for non-clickable nav text

Right sidebar palette for later advanced phase:

- HTML fragment
- Dynamic fragment
- Scriban/template content

Do not implement unsafe raw HTML in V1. If an "HTML" item appears in the editor
before the advanced renderer is implemented, it must be disabled or marked as
coming soon and must not be persisted as executable raw HTML.

List view:

- Name
- Key
- State
- Default indicator
- Last modified
- Actions: edit, duplicate, publish, set default, archive

Editor:

- Settings panel: name, key, layout, responsive, sticky.
- Bucket/canvas view mapped from `NavMenuLayout.Slots`.
- Block palette: Link, Dropdown, Search, Spacer, Divider.
- Properties panel for the selected item.
- Preview panel with desktop/mobile toggle.
- Visibility controls on each item:
  - hide on mobile
  - hide on desktop
  - allowed roles, shown only when role support is enabled/available
- Save draft.
- Publish.

Page editor integration:

- Add a header navigation setting next to the existing navigation visibility
  option.
- Dropdown values:
  - "Use site default"
  - current-site published menus only
- Persist to `PageDocument.HeaderNavigationMenuId`.

## Advanced Phase: Dynamic And Template Content

Implement after V1 is stable.

### Dynamic Providers

```csharp
public interface INavDynamicFragmentProvider
{
    string Key { get; }

    Task<string> RenderAsync(
        NavDynamicFragmentItem item,
        NavRenderContext context,
        CancellationToken ct = default);
}

public sealed class NavDynamicFragmentRegistry
{
    private readonly IReadOnlyDictionary<string, INavDynamicFragmentProvider> _providers;

    public NavDynamicFragmentRegistry(IEnumerable<INavDynamicFragmentProvider> providers)
    {
        _providers = providers.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
    }

    public bool TryGet(string key, out INavDynamicFragmentProvider provider)
        => _providers.TryGetValue(key, out provider!);
}
```

Rules:

- Providers are registered by code.
- Editors choose provider keys and safe provider options.
- Providers return sanitized HTML fragments or render through known Razor
  components.
- Public HTMX endpoints call providers by key; they do not call arbitrary
  editor-provided URLs.

### Scriban Templates

When adding Scriban:

- Use template records with IDs/versions.
- Parse/cache templates by version/hash.
- Use strict variables.
- Pass safe DTOs only.
- Never pass domain entities or Marten documents directly.
- Reject templates that fail parse/validation before publishing.
- Keep template data sources registry-driven.

Sketch:

```csharp
public interface INavTemplateRenderer
{
    Task<string> RenderAsync(
        string templateKey,
        object model,
        CancellationToken ct = default);
}
```

Do not support external URL templates in the first advanced slice.

## Events And Invalidation

Add events if the module/event conventions need them for cache invalidation,
audit, or integrations:

```csharp
public sealed record NavMenuPublishedEvent(
    long SiteId,
    long NavMenuId,
    long VersionId,
    long? UserId,
    DateTimeOffset PublishedOn);

public sealed record SiteDefaultNavMenuChangedEvent(
    long SiteId,
    long? NavMenuId,
    long? UserId,
    DateTimeOffset ChangedOn);

public sealed record PageNavMenuOverrideChangedEvent(
    long SiteId,
    long PageId,
    long? NavMenuId,
    long? UserId,
    DateTimeOffset ChangedOn);
```

Consumers should invalidate:

- `nav:published:{siteId}:{menuId}:*`
- `nav:default:{siteId}:*`
- `nav:page:{siteId}:{pageId}:*`

Exact key format is implementation-specific, but it must include `SiteId`.

## Migration And Compatibility

The existing `NavigationBlock` model is simpler and may already have seeded
data. Do not delete it first.

Compatibility approach:

1. Add new nav menu documents.
2. Add adapter logic so existing `NavigationBlock.NavigationId` can reference
   new nav menus or continue rendering old simple data during migration.
3. Add a migration/backfill path from seeded/simple `NavigationBlock` records
   into `NavMenuDocument` plus a published `NavMenuVersionDocument`.
4. Update seeded starter content to create one published default nav menu per
   site.
5. After manager/public paths use the new model, decide whether legacy
   inline/simple navigation remains supported as a page content block.

## Testing Strategy

Use TUnit for unit tests and Alba for minimal API integration tests. Use
Playwright for manager UI and public rendering checks when the UI is built.

### Unit Tests

Cover:

- Create menu normalizes key and stamps site/user metadata.
- Duplicate `(SiteId, Key)` is rejected.
- Same key is allowed on different sites.
- Draft save requires expected revision.
- Publish creates/uses immutable published version.
- Save draft after publish does not change public rendering until the next
  publish.
- Archived/unpublished menu cannot be default.
- Dropdown depth limit is enforced.
- Internal page link must belong to same site.
- Role-restricted item is omitted for anonymous users and users without a
  matching role.
- Role-restricted item renders for users with a matching role.
- `NavMenuSnapshot` with Link, Dropdown, Search, Divider, and Spacer items
  serializes/deserializes via `NavMenuJsonContext`.
- Page override fallback chain works.

### Integration Tests

Cover:

- Manager list only returns current-site menus.
- Manager get/update rejects cross-site IDs.
- Public render returns only published menu.
- Draft changes do not affect public render before publish.
- Setting default for Site A does not affect Site B.
- Page override for unpublished/deleted menu falls back to default.
- Public render filters role-restricted items using the current user/member
  roles.
- Marten stores and reloads a published nav menu snapshot containing every V1
  item subtype without losing concrete item types.

### UI Tests

Cover:

- `/manager/navigations` lists real menus.
- Editor can add/reorder link and dropdown items.
- Save draft preserves layout slots and item properties.
- Publish updates public preview/render.
- Page editor can choose "Use site default" or a published menu.
- Item properties panel can configure role visibility when role support is
  enabled.
- Mobile preview renders without overlapping controls.

## Verification Commands

Adjust project names if the final module/test names differ.

```powershell
dotnet build .\src\Aero.Cms.Modules.Navigation\Aero.Cms.Modules.Navigation.csproj --no-restore
dotnet build .\src\Aero.Cms.Shared\Aero.Cms.Shared.csproj --no-restore
dotnet test .\tests\Aero.Cms.Modules.Navigation.Tests\Aero.Cms.Modules.Navigation.Tests.csproj --no-restore
```

If the full solution is noisy because of unrelated source-generator issues, use
focused builds/tests for the navigation projects and document the unrelated
failure.

## Implementation Phases

### Phase 1: Domain And Persistence

- Add module project if it does not exist.
- Add nav menu documents and value objects.
- Add navigation event records and event-stream naming helpers.
- Add inline Marten projections for `NavMenuDocument` and
  `SiteNavigationSettingsDocument`, following the current
  `PageDocumentProjection` pattern.
- Add Marten mapping and indexes.
- Add serializers for item discriminators.
- Add `NavMenuJsonContext` and compose it with the existing AeroCMS/Marten JSON
  resolver configuration.
- Add service interfaces and validation.
- Add unit tests for aggregate invariants.
- Add draft/published version state transitions as first-class behavior.

Acceptance:

- `NavMenuDocument`, `NavMenuVersionDocument`, and
  `SiteNavigationSettingsDocument` persist with `long` IDs.
- Writes append typed Marten events to `nav-menu-{id}` and
  `site-nav-settings-{siteId}` streams; projected documents are the read model.
- Site-scoped uniqueness works.
- Draft/publish state transitions are tested.
- Draft saves after publish leave the published version unchanged.
- A Marten persistence test proves `NavMenuSnapshot.Items` round-trips every V1
  `NavMenuItem` subtype with `$navItemType` discriminators.

### Phase 2: Manager API

- Add manager minimal APIs.
- Add typed abstraction/client contracts.
- Add current-site scoping.
- Add revision conflict handling.
- Add request/response support for item visibility settings.
- Add integration tests with same-menu-key across two sites.

Acceptance:

- Manager endpoints never accept trusted `SiteId` in payloads.
- Cross-site get/update/delete/default attempts fail.
- Stale draft saves return conflict.

### Phase 3: Builder UI

- Wire `/manager/navigations` to real API data.
- Rename the manager left-hand menu label from `Navigations` to `Header Menu`.
- Add editor page and code-behind.
- Build the editor with the same core interaction model as `PageEditor.razor`:
  central canvas, right-hand draggable palette, selected-item toolbar, and
  preview/save/publish actions.
- Add nav-specific block/element palette, layout-slot canvas, properties panel,
  and preview.
- Support Link, Dropdown, Search, Spacer, Divider.
- Support editing breakpoint visibility and role visibility on each item when
  role support is enabled.

Acceptance:

- Editor can create, save draft, publish, and set default.
- Manager left-hand menu shows `Header Menu`.
- Header Menu editor supports dragging sidebar elements into left/center/right
  nav layout slots.
- UI preserves existing manager shell/theme.

### Phase 4: Public Rendering And Page Overrides

- Add resolver and public renderer.
- Add page model/request/client changes for `HeaderNavigationMenuId`.
- Add page editor control.
- Render public header nav using resolver.
- Filter items by role visibility using the active public user/member role
  context.
- Add cache and invalidation.

Acceptance:

- Page override wins over site default.
- Missing/unpublished override falls back to default.
- Public rendering uses published version only.
- Public rendering omits role-restricted items for users without matching roles.

### Phase 5: Advanced Content

- Add registered dynamic fragment providers.
- Add public HTMX fragment endpoint backed by provider keys.
- Add template model and Scriban renderer with strict safe DTO context.
- Add template parse/cache behavior.

Acceptance:

- Editors cannot store arbitrary endpoint URLs.
- Editors cannot store arbitrary target selectors.
- Templates cannot access domain entities directly.
- Parsed templates are cached by version/hash.

## Open Decisions

Ask before implementing these if the answer is not already documented:

- Should legacy `NavigationBlock` remain a page content block forever, or only
  exist for migration?
- Which existing current-site/user services should the navigation APIs use for
  site and user IDs?
- Which public user/member role source should navigation rendering use for
  role-based visibility?
- Should nav cache use in-memory first or plug into existing cache/output-cache
  infrastructure immediately?

## References

- `AGENTS.md`
- `docs/site-manager-tasks.md`
- `docs/aero-cms-multisite-spec.md`
- `docs/source-generator-block-renderer.md`
- `marten-llms-full.txt`
- Microsoft Learn: Minimal APIs quick reference,
  `https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-10.0`
- Microsoft Learn: Create responses in Minimal API applications,
  `https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0`
