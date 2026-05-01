# Aero CMS Theming Roadmap

## Purpose

This document defines a practical theming roadmap for **Aero CMS** that stays close to standard ASP.NET Core patterns while still providing strong customization capabilities.

The design goal is to avoid the complexity of shape engines, heavy runtime builders, or CMS-specific rendering systems unless those become necessary later.

Aero CMS should favor:

- predictable file-based overrides
- standard Razor Pages / MVC conventions
- CSS-first branding and visual theming
- optional layout, partial, and page overrides
- future support for named themes and per-site selection

---

## Core Principles

1. **Stay ASP.NET Core-native first**  
   The default theming story should feel familiar to any developer who knows Razor Pages, MVC views, layouts, partials, static files, and Razor Class Libraries.

2. **Use the lightest override mechanism that solves the problem**  
   - CSS for visual design changes
   - layout overrides for shell changes
   - partial/slot overrides for regional changes
   - page overrides for structural changes

3. **Do not require new UI controls for simple theming**  
   Theming should not force consumers to create replacement RCL components unless they are truly changing behavior or reusable UI structure.

4. **Make the default UI package reusable, but not rigid**  
   Aero CMS should ship a default UI through a Razor Class Library or equivalent package structure, while allowing consuming applications to replace parts of it.

5. **Grow into a full theme system only when needed**  
   Support V1 now with standard file-based overrides. Add V2 only when the product requires named themes, inheritance, runtime selection, or multi-site theme switching.

---

# Version 1: ASP.NET Core-Native Theming

## V1 Goals

Version 1 should solve the majority of theming needs with minimal infrastructure.

### V1 must support

- host-controlled CSS theme files
- packaged default layouts
- packaged partials / regions / slots
- consuming app overrides for layouts, partials, and pages
- clear documentation and conventions

### V1 does not need to support

- named themes
- runtime theme switching
- theme inheritance
- database-driven templates
- custom rendering engines
- Orchard-style shape systems

---

## V1 Customization Layers

### Layer 1: CSS Token and Stylesheet Overrides

This is the default theming mechanism.

Use CSS for:

- color palette
- typography
- spacing
- borders and radius
- buttons
- cards
- tables
- forms
- nav styling
- dark/light appearance
- branding

### Recommendation

Ship Aero UI with semantic CSS variables rather than hardcoded styling values.

Example:

```css
:root {
  --aero-bg: #0f1115;
  --aero-surface: #171a21;
  --aero-surface-2: #1f2430;
  --aero-text: #e8ecf1;
  --aero-muted: #97a3b6;
  --aero-accent: #58a6ff;
  --aero-accent-contrast: #ffffff;
  --aero-border: #2b3444;
  --aero-danger: #d9534f;
  --aero-warning: #f0ad4e;
  --aero-success: #198754;
  --aero-radius-sm: 6px;
  --aero-radius-md: 12px;
  --aero-radius-lg: 18px;
  --aero-space-1: 0.25rem;
  --aero-space-2: 0.5rem;
  --aero-space-3: 0.75rem;
  --aero-space-4: 1rem;
  --aero-space-5: 1.5rem;
  --aero-shadow-1: 0 2px 8px rgba(0, 0, 0, 0.12);
}
```

### Recommended file structure

```text
Aero.Cms.Web/
  wwwroot/
    css/
      aero-base.css
      aero-components.css
      aero-theme-default.css
```

Consuming app:

```text
MyHostSite/
  wwwroot/
    css/
      aero-theme.css
      aero-overrides.css
```

### Recommendation for load order

```html
<link rel="stylesheet" href="~/css/aero-base.css" />
<link rel="stylesheet" href="~/css/aero-components.css" />
<link rel="stylesheet" href="~/css/aero-theme.css" />
<link rel="stylesheet" href="~/css/aero-overrides.css" />
```

### Design rule

All packaged markup should use semantic CSS classes and CSS variables. Avoid deeply coupling visuals to hardcoded inline style decisions.

---

### Layer 2: Layout Overrides

Layouts should control the page shell, not page-specific business logic.

Use layout overrides for:

- admin shell
- login/auth shell
- public site shell
- dashboard shell
- top navigation
- footer structure
- global scripts/styles placement

### Recommended default layouts

```text
Areas/
  AeroCms/
    Pages/
      Shared/
        _AeroLayout.cshtml
        _AeroAdminLayout.cshtml
        _AeroAuthLayout.cshtml
```

### Layout guidance

Each major UI context should have a stable layout file:

- `_AeroLayout` for general site rendering
- `_AeroAdminLayout` for manager/admin pages
- `_AeroAuthLayout` for login, register, forgot password, etc.

### Example page usage

```cshtml
@page
@model LoginModel
@{
    Layout = "/Areas/AeroCms/Pages/Shared/_AeroAuthLayout.cshtml";
}
```

### Why this matters

A consuming host app should be able to replace the shell without needing to replace every page.

---

### Layer 3: Partial / Slot Overrides

This is the recommended mechanism for regional customization.

Instead of forcing consumers to replace a full page, expose stable partials for common regions.

### Example slot structure

```text
Areas/
  AeroCms/
    Pages/
      Shared/
        Slots/
          _TopNav.cshtml
          _Sidebar.cshtml
          _Footer.cshtml
          _PageHeader.cshtml
          _PageActions.cshtml
          _AuthAside.cshtml
          _DashboardWidgets.cshtml
```

### Example layout using slots

```cshtml
<!DOCTYPE html>
<html>
<head>
    <partial name="~/Areas/AeroCms/Pages/Shared/Slots/_Head.cshtml" />
</head>
<body class="aero-shell aero-shell-admin">
    <partial name="~/Areas/AeroCms/Pages/Shared/Slots/_TopNav.cshtml" />

    <div class="aero-shell-body">
        <aside class="aero-shell-sidebar">
            <partial name="~/Areas/AeroCms/Pages/Shared/Slots/_Sidebar.cshtml" />
        </aside>

        <main class="aero-shell-content">
            <partial name="~/Areas/AeroCms/Pages/Shared/Slots/_PageHeader.cshtml" />
            @RenderBody()
        </main>
    </div>

    <partial name="~/Areas/AeroCms/Pages/Shared/Slots/_Footer.cshtml" />
    @RenderSection("Scripts", required: false)
</body>
</html>
```

### Benefits

- smaller override surface
- less file duplication
- fewer merge issues during upgrades
- predictable extension points

### Rule

Any region that is likely to vary between installations should become a slot rather than being hardcoded into many pages.

---

### Layer 4: Full Page Overrides

This is the escape hatch for structural customization.

If a consuming application places a matching Razor Page or view at the same path as the packaged version, the consuming app should win.

### Example

Packaged page:

```text
Aero.Cms.Web/
  Areas/
    AeroCms/
      Pages/
        Login.cshtml
```

Consuming app override:

```text
MyHostSite/
  Areas/
    AeroCms/
      Pages/
        Login.cshtml
```

### Recommended use cases

Use a full page override when:

- the structure changes significantly
- the layout differs beyond CSS and slots
- additional page regions are needed
- page-specific markup is fundamentally different

### Do not use page overrides for

- simple colors
- spacing changes
- swapping one region or menu
- small visual tweaks

Those should be handled via CSS or slot overrides.

---

## V1 Recommended Folder Structure

### Packaged UI project

```text
src/
  Aero.Cms.Web/
    Areas/
      AeroCms/
        Pages/
          Shared/
            _AeroLayout.cshtml
            _AeroAdminLayout.cshtml
            _AeroAuthLayout.cshtml
            Slots/
              _Head.cshtml
              _TopNav.cshtml
              _Sidebar.cshtml
              _Footer.cshtml
              _PageHeader.cshtml
              _PageActions.cshtml
              _AuthAside.cshtml
              _DashboardWidgets.cshtml
          Login.cshtml
          Dashboard.cshtml
          Pages/
            Edit.cshtml
            Index.cshtml
          Posts/
            Edit.cshtml
            Index.cshtml
          Media/
            Index.cshtml
          Docs/
            Index.cshtml
    wwwroot/
      css/
        aero-base.css
        aero-components.css
        aero-theme-default.css
      js/
        aero.js
```

### Consuming app

```text
src/
  MyHostSite/
    Areas/
      AeroCms/
        Pages/
          Shared/
            _AeroAdminLayout.cshtml
            Slots/
              _TopNav.cshtml
              _Sidebar.cshtml
          Login.cshtml
    wwwroot/
      css/
        aero-theme.css
        aero-overrides.css
```

---

## V1 Required Conventions

### Convention 1: Path-based overrides are intentional

Any page, layout, or partial that can be overridden must live at a stable and documented path.

### Convention 2: Shared layout and slot names are versioned carefully

Do not rename core layouts or slots casually once released.

### Convention 3: Aero CSS classes should be semantic

Prefer:

- `.aero-card`
- `.aero-nav`
- `.aero-form-group`
- `.aero-page-header`

Avoid classes that encode visual implementation details too tightly.

### Convention 4: Page logic should not be tightly coupled to view replacement

If a page requires custom behavior, that behavior should be injected through services or view models where possible. Consumers should not need to duplicate code-behind merely to restyle a page.

### Convention 5: Default UI should degrade gracefully

If a slot partial is not overridden, the packaged default should still render correctly.

---

## V1 Interfaces and Options

Version 1 should stay simple, but a small options model is still useful.

### Example options class

```csharp
public sealed class AeroThemeOptions
{
    public string AdminLayoutPath { get; set; } = "/Areas/AeroCms/Pages/Shared/_AeroAdminLayout.cshtml";
    public string AuthLayoutPath { get; set; } = "/Areas/AeroCms/Pages/Shared/_AeroAuthLayout.cshtml";
    public string SiteLayoutPath { get; set; } = "/Areas/AeroCms/Pages/Shared/_AeroLayout.cshtml";
    public bool UseBundledDefaultTheme { get; set; } = true;
    public List<string> AdditionalStylesheets { get; set; } = [];
}
```

### Example registration

```csharp
builder.Services.Configure<AeroThemeOptions>(options =>
{
    options.AdditionalStylesheets.Add("/css/aero-theme.css");
    options.AdditionalStylesheets.Add("/css/aero-overrides.css");
});
```

---

## V1 Startup and Rendering Guidance

### Static asset guidance

The packaged UI should expose its default assets normally. The consuming host should load its own theme files after the base package assets.

### Layout resolution guidance

Pages should use stable layout paths or resolve them through a small helper or base page model if needed.

### Example helper

```csharp
public interface IAeroLayoutResolver
{
    string GetAdminLayout();
    string GetAuthLayout();
    string GetSiteLayout();
}
```

Default implementation:

```csharp
public sealed class DefaultAeroLayoutResolver(IOptions<AeroThemeOptions> options) : IAeroLayoutResolver
{
    public string GetAdminLayout() => options.Value.AdminLayoutPath;
    public string GetAuthLayout() => options.Value.AuthLayoutPath;
    public string GetSiteLayout() => options.Value.SiteLayoutPath;
}
```

This adds some flexibility without introducing a custom rendering engine.

---

## V1 Documentation Requirements

Aero CMS must document the following clearly:

1. how to add a CSS theme file
2. how to override a layout
3. how to override a slot partial
4. how to override a full page
5. which files are considered safe extension points
6. which files are internal and may change more often

### Suggested docs pages

- `theming-overview.md`
- `css-tokens.md`
- `layout-overrides.md`
- `slot-overrides.md`
- `page-overrides.md`
- `upgrade-guidance.md`

---

# Version 2: Full Theme System

## V2 Goals

Version 2 should add support for multiple named themes, site-aware theme selection, and theme inheritance, while still preserving the V1 mental model.

### V2 must support

- named themes
- theme manifests
- theme-specific assets
- theme-aware Razor lookup
- theme inheritance / fallback
- per-site or per-tenant active theme resolution

### V2 should avoid

- full dynamic shape engines
- CMS-stored page templates as the primary model
- overly abstract rendering trees
- page-builder dependency for all layout choices

---

## V2 Theme Model

### Theme directory structure

```text
Themes/
  Default/
    theme.json
    wwwroot/
      css/
        theme.css
    Areas/
      AeroCms/
        Pages/
          Shared/
            _AeroAdminLayout.cshtml
            Slots/
              _TopNav.cshtml
          Login.cshtml

  Midnight/
    theme.json
    wwwroot/
      css/
        theme.css
    Areas/
      AeroCms/
        Pages/
          Shared/
            _AeroAdminLayout.cshtml
            Slots/
              _TopNav.cshtml
```

### Theme manifest example

```json
{
  "name": "Midnight",
  "displayName": "Midnight",
  "inheritsFrom": "Default",
  "version": "1.0.0",
  "areas": ["AeroCms"],
  "styles": [
    "/themes/midnight/css/theme.css"
  ]
}
```

### Theme manifest rules

- `name` must be unique
- `inheritsFrom` is optional
- `styles` identifies theme asset paths
- `areas` tells Aero which area paths the theme can override

---

## V2 Theme Resolver

Introduce a resolver that determines the active theme for the current request.

### Interface

```csharp
public interface IAeroThemeResolver
{
    Task<string> GetActiveThemeAsync(HttpContext httpContext, CancellationToken cancellationToken = default);
}
```

### Possible resolution strategies

- app configuration
- current site record
- current tenant record
- hostname/domain
- route segment
- admin-selected preference

### Initial recommendation

In Aero CMS, theme resolution should be based on the resolved **Site** first.

Example:

```csharp
public sealed class SiteBasedThemeResolver(ISiteContextAccessor siteContextAccessor)
    : IAeroThemeResolver
{
    public Task<string> GetActiveThemeAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        var site = siteContextAccessor.CurrentSite;
        return Task.FromResult(site?.Theme ?? "Default");
    }
}
```

---

## V2 Theme Catalog

A service is needed to discover and expose available themes.

### Interface

```csharp
public interface IAeroThemeCatalog
{
    Task<IReadOnlyList<AeroThemeDescriptor>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<AeroThemeDescriptor?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
}
```

### Descriptor

```csharp
public sealed class AeroThemeDescriptor
{
    public string Name { get; init; } = null!;
    public string DisplayName { get; init; } = null!;
    public string? InheritsFrom { get; init; }
    public string Version { get; init; } = "1.0.0";
    public IReadOnlyList<string> Styles { get; init; } = [];
    public IReadOnlyList<string> Areas { get; init; } = [];
}
```

---

## V2 Theme Inheritance

Theme inheritance allows one theme to override only part of another.

### Example

`Midnight` inherits from `Default`.

If `Midnight` provides:

- custom `_AeroAdminLayout.cshtml`
- custom `theme.css`

but does not provide:

- `Login.cshtml`
- `_Sidebar.cshtml`

then Aero falls back to `Default` for the missing files.

### Benefits

- less duplication
- easier theme maintenance
- safer upgrades
- premium theme packs become practical

---

## V2 Theme-Aware View Lookup

This is the primary technical addition in V2.

Aero should add theme-aware view discovery so Razor searches theme folders before the default host/package locations.

### Intended lookup order

For an active theme `Midnight` inheriting from `Default`:

1. `Themes/Midnight/Areas/AeroCms/Pages/...`
2. `Themes/Default/Areas/AeroCms/Pages/...`
3. `Host app Areas/AeroCms/Pages/...`
4. `Packaged RCL default Areas/AeroCms/Pages/...`

### Result

This preserves backward compatibility with V1 while adding a first-class theme layer.

---

## V2 Theme Asset Registration

Themes should be able to register CSS and JS assets in a controlled way.

### Interface

```csharp
public interface IAeroThemeAssetService
{
    Task<IReadOnlyList<string>> GetStylesAsync(HttpContext httpContext, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetScriptsAsync(HttpContext httpContext, CancellationToken cancellationToken = default);
}
```

### Layout integration example

```cshtml
@inject IAeroThemeAssetService ThemeAssets
@{
    var styles = await ThemeAssets.GetStylesAsync(Context);
}

@foreach (var href in styles)
{
    <link rel="stylesheet" href="@href" />
}
```

---

## V2 Display Variants

V2 may support limited display variants without introducing Orchard-style shapes.

### Goal

Allow different markup variants for specific UI contexts.

### Example naming

```text
Pages/Edit.cshtml
Pages/Edit.Compact.cshtml
Pages/Edit.FullWidth.cshtml
Pages/Edit.Sidebarless.cshtml
```

### Usage scenarios

- compact dashboard variant
- simplified mobile-friendly management screen
- alternate content editing layout
- different site template shells

### Important rule

Variants should remain explicit and small in scope. Avoid turning them into a full alternate/template system unless there is strong demand.

---

## V2 Site Administration Integration

Once multi-site support is present, a Site entity should be able to reference the active theme.

### Example

```csharp
public class Site
{
    public long Id { get; set; }
    public string Name { get; set; } = null!;
    public string Host { get; set; } = null!;
    public string Theme { get; set; } = "Default";
}
```

### Admin requirements

Site administration should eventually provide:

- list available themes
- choose active theme per site
- preview theme metadata
- validate missing theme references

---

# Migration Path from V1 to V2

## Phase 1

Ship V1 with:

- CSS tokens
- packaged default layouts
- slot partials
- file-based page overrides
- theming documentation

## Phase 2

Add:

- theme descriptors
- theme discovery service
- theme manifests
- theme asset registration

## Phase 3

Add:

- theme resolver
- per-site theme selection
- theme-aware view lookup
- theme inheritance

## Phase 4

Add optional:

- display variants
- packaged premium themes
- theme selection UI in manager/admin
- import/export theme metadata

---

# Non-Goals

The following are intentionally not part of this roadmap unless future requirements force them:

- database-stored page templates as the main rendering strategy
- fully dynamic page builder as the core theming approach
- large runtime visual editor for all page structure
- full shape engine and alternate resolution graph
- arbitrary runtime Razor compilation from untrusted sources
- replacing plain ASP.NET Core conventions with CMS-only abstractions

---

# Recommended Initial Implementation Checklist

## V1 Checklist

- [ ] Create semantic CSS token file
- [ ] Refactor packaged styles to use CSS variables
- [ ] Add `_AeroLayout`, `_AeroAdminLayout`, `_AeroAuthLayout`
- [ ] Identify high-change regions and extract them into slot partials
- [ ] Document stable override paths
- [ ] Add `AeroThemeOptions`
- [ ] Add `IAeroLayoutResolver`
- [ ] Update pages to use stable layout resolution
- [ ] Create docs with examples for host overrides

## V2 Checklist

- [ ] Define `AeroThemeDescriptor`
- [ ] Add `theme.json` schema
- [ ] Implement `IAeroThemeCatalog`
- [ ] Implement `IAeroThemeResolver`
- [ ] Add theme asset service
- [ ] Add theme-aware view search strategy
- [ ] Add Site.Theme support
- [ ] Add admin UI for site theme selection
- [ ] Add inheritance fallback logic

---

# Example Extension Guidance for Consumers

## Change only branding

Add a custom CSS file and override tokens.

## Change admin shell

Override `_AeroAdminLayout.cshtml`.

## Change nav/sidebar/footer only

Override slot partials in `Shared/Slots`.

## Change login page structure

Override `Areas/AeroCms/Pages/Login.cshtml`.

## Create multiple reusable themes

Use V2 theme folders and manifests.

---

# Final Recommendation

Aero CMS should launch its theming model with **V1 only**.

That gives:

- a clean and familiar developer experience
- strong customization for most real-world use cases
- low maintenance burden
- low framework magic
- clean compatibility with standard ASP.NET Core hosting

V2 should be introduced only after there is proven need for:

- multi-site branded deployments
- theme packs
- site-level theme selection
- runtime theme switching
- inheritance and reusable theme families

This keeps Aero CMS simpler than Orchard, Sitecore, Kentico, and Kooboo while still giving it a credible, extensible theming story.

