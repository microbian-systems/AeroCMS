# Aero.Cms Spec: Theme Engine and Theme Selection

> **Phasing authority:** See [`aero_cms_theming_roadmap.md`](./aero_cms_theming_roadmap.md) for the full V1→V2 roadmap, rationale, non-goals, and migration plan. This document is the implementation spec, kept in sync with that roadmap.

## Goal

Define how themes render pages, override templates, supply assets, and vary by site/tenant — delivered in two phases so that V1 ships a clean ASP.NET Core-native story and V2 adds named themes, inheritance, and runtime selection only when proven necessary.

---

# Version 1: ASP.NET Core-Native Theming

V1 ships first. No custom rendering engine. Pure Razor conventions + CSS tokens + file-based overrides.

## V1 Core Principles

1. **Stay ASP.NET Core-native** — Razor Pages, MVC views, layouts, partials, static files, RCLs.
2. **Use the lightest mechanism that solves the problem** — CSS for visuals, layout overrides for shells, slot partials for regions, page overrides for structure.
3. **No new UI controls for simple theming** — consumers should not need replacement RCLs just to restyle.
4. **Packaged default UI is reusable but not rigid** — ship a default UI in an RCL; let hosts override parts of it.
5. **Grow into V2 only when needed** — named themes, inheritance, runtime switching wait for real demand.

## V1 Customization Layers

### Layer 1: CSS Tokens and Stylesheet Overrides (primary mechanism)

All packaged markup uses CSS custom properties. The host provides override stylesheets loaded after base styles.

**Recommended CSS variable convention:**

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

**File structure (packaged UI):**

```text
Aero.Cms.Web/
  wwwroot/
    css/
      aero-base.css
      aero-components.css
      aero-theme-default.css
```

**File structure (consuming app):**

```text
MyHostSite/
  wwwroot/
    css/
      aero-theme.css
      aero-overrides.css
```

**Load order:**

```html
<link rel="stylesheet" href="~/css/aero-base.css" />
<link rel="stylesheet" href="~/css/aero-components.css" />
<link rel="stylesheet" href="~/css/aero-theme.css" />
<link rel="stylesheet" href="~/css/aero-overrides.css" />
```

All packaged markup uses semantic CSS classes (`.aero-card`, `.aero-nav`, `.aero-form-group`, `.aero-page-header`), avoiding inline styles that couple visuals to implementation details.

### Layer 2: Layout Overrides

Three stable layout files control the page shell:

```text
Areas/
  AeroCms/
    Pages/
      Shared/
        _AeroLayout.cshtml       (public site)
        _AeroAdminLayout.cshtml  (manager/admin)
        _AeroAuthLayout.cshtml   (login/register/forgot password)
```

The host can replace any layout by placing the same file at the same path. Layouts control the shell, not page-specific business logic.

### Layer 3: Partial / Slot Overrides

Stable, overridable partials for common regions:

```text
Areas/
  AeroCms/
    Pages/
      Shared/
        Slots/
          _Head.cshtml
          _TopNav.cshtml
          _Sidebar.cshtml
          _Footer.cshtml
          _PageHeader.cshtml
          _PageActions.cshtml
          _AuthAside.cshtml
          _DashboardWidgets.cshtml
```

Any region likely to vary between installations becomes a slot. If a slot is not overridden, the packaged default still renders correctly.

### Layer 4: Full Page Overrides

Escape hatch for structural customization. If the host places a matching Razor Page at the same path as the packaged version, the host wins.

Use only when structure changes significantly. CSS and slot overrides cover most cases.

## V1 Options and Services

### AeroThemeOptions

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

### IAeroLayoutResolver

```csharp
public interface IAeroLayoutResolver
{
    string GetAdminLayout();
    string GetAuthLayout();
    string GetSiteLayout();
}
```

---

# Version 2: Full Theme System

V2 adds named themes, manifests, inheritance, and site-aware resolution — activated only after multi-site/theme-pack demand is proven.

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

### Theme manifest (`theme.json`)

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

Manifest rules:
- `name` must be unique
- `inheritsFrom` is optional
- `styles` identifies theme asset paths
- `areas` tells Aero which area paths the theme can override

### AeroThemeDescriptor

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

## V2 Theme Resolution

Theme resolution is based on the resolved **Site** first.

### IAeroThemeResolver

```csharp
public interface IAeroThemeResolver
{
    Task<string> GetActiveThemeAsync(HttpContext httpContext, CancellationToken cancellationToken = default);
}
```

Default site-based implementation:

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

### IAeroThemeCatalog

```csharp
public interface IAeroThemeCatalog
{
    Task<IReadOnlyList<AeroThemeDescriptor>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<AeroThemeDescriptor?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
}
```

## V2 Theme-Aware View Lookup

The primary technical addition in V2. Razor searches theme folders before host/package locations.

For an active theme `Midnight` inheriting from `Default`:

1. `Themes/Midnight/Areas/AeroCms/Pages/...`
2. `Themes/Default/Areas/AeroCms/Pages/...`
3. Host app `Areas/AeroCms/Pages/...`
4. Packaged RCL default `Areas/AeroCms/Pages/...`

This preserves backward compatibility with V1 while adding a first-class theme layer.

## V2 Theme Inheritance

A theme inheriting from another overrides only what it provides; missing files fall back through the chain.

If `Midnight` (inherits `Default`) provides `theme.css` and `_AeroAdminLayout.cshtml` but not `Login.cshtml` or `_Sidebar.cshtml`, Aero falls back to `Default` for the missing files.

## V2 Theme Asset Service

```csharp
public interface IAeroThemeAssetService
{
    Task<IReadOnlyList<string>> GetStylesAsync(HttpContext httpContext, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetScriptsAsync(HttpContext httpContext, CancellationToken cancellationToken = default);
}
```

## V2 Site Entity Integration

```csharp
public class Site
{
    public long Id { get; set; }
    public string Name { get; set; } = null!;
    public string Host { get; set; } = null!;
    public string Theme { get; set; } = "Default";
}
```

Admin capabilities: list available themes, choose active theme per site, preview theme metadata, validate missing theme references.

## V2 Display Variants

Limited display variants without a full shape engine. Explicit and small in scope.

```text
Pages/Edit.cshtml
Pages/Edit.Compact.cshtml
Pages/Edit.FullWidth.cshtml
Pages/Edit.Sidebarless.cshtml
```

## V2 Theme Discovery

Themes are discoverable from:
- `Themes/` folder on disk
- referenced assemblies / RCLs
- theme manifests (`theme.json`)

Discovery is provided by `IAeroThemeCatalog`; no reflection-based scanning.

## V2 Theme Settings

Themes can declare configurable settings exposed to admin UI:

- brand color
- logo
- typography choice
- layout width
- dark mode
- menu style

```csharp
public interface IThemeSettingsProvider
{
    IEnumerable<ThemeSettingDefinition> GetSettings();
}
```

## V2 Asset Handling

Theme assets via `wwwroot` with:
- cache-busting / version hashing
- CSP compatibility
- site-specific logo overrides
- optional CDN support
- asset registration through `IAeroThemeAssetService`

## V2 Admin vs Frontend Themes

- Admin theme optimized for CMS UX
- Frontend theme optimized for public site
- Both resolved through the same theme pipeline, selected independently per site

## V2 Localization and Themes

Themes respect current culture for:
- text resources
- localized menu labels
- directionality (RTL if later supported)
- culture-specific assets if needed

---

# Non-Goals (both versions)

Intentionally excluded unless future requirements force them:

- database-stored page templates as the main rendering strategy
- fully dynamic page builder as the core theming approach
- large runtime visual editor for all page structure
- full shape engine and alternate resolution graph (beyond V2 display variants)
- arbitrary runtime Razor compilation from untrusted sources
- replacing plain ASP.NET Core conventions with CMS-only abstractions

---

# Deliverables

## V1 Deliverables

- [ ] CSS token file with semantic variables
- [ ] Packaged styles refactored to use CSS variables
- [ ] `_AeroLayout`, `_AeroAdminLayout`, `_AeroAuthLayout`
- [ ] Slot partials extracted for high-change regions
- [ ] Stable override paths documented
- [ ] `AeroThemeOptions` class
- [ ] `IAeroLayoutResolver` + default implementation
- [ ] Pages updated to use stable layout resolution
- [ ] Docs with examples for host overrides

## V2 Deliverables

- [ ] `AeroThemeDescriptor` model
- [ ] `theme.json` schema
- [ ] `IAeroThemeCatalog` implementation
- [ ] `IAeroThemeResolver` implementation (site-based)
- [ ] `IAeroThemeAssetService` implementation
- [ ] Theme-aware view search strategy
- [ ] `Site.Theme` property support
- [ ] Admin UI for site theme selection
- [ ] Theme inheritance / fallback logic
- [ ] Tests for override precedence
