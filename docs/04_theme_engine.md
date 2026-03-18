# Aero.Cms Spec: Theme Engine and Theme Selection

## Goal

Define how themes render shapes, override templates, supply assets, and vary by tenant.

## Theme Responsibilities

A theme may provide:
- layout templates
- shape templates
- partials
- CSS/JS/images/fonts
- theme settings
- alternate templates
- admin and frontend variants

## Theme Model

```csharp
public sealed class ThemeDescriptor
{
    public string Name { get; init; }
    public string Version { get; init; }
    public string Author { get; init; }
    public string BaseTheme { get; init; }
    public string AssemblyName { get; init; }
}
```

## Tenant Theme Selection

Each tenant chooses:
- frontend theme
- optional admin theme

```csharp
public sealed class TenantThemeSettings
{
    public string TenantId { get; set; }
    public string FrontendTheme { get; set; }
    public string AdminTheme { get; set; }
}
```

## Shape Rendering Relationship

Modules produce shapes.
Themes render shapes using **Razor Slices** (for high performance CMS/Blog pages) or standard Razor views (for Admin/Custom areas).

**Native AOT:** The use of Razor Slices for public content ensures that the rendering pipeline is reflection-free and fully compatible with Native AOT compilation.

Renderer lookup order recommendation:
1. current theme alternate slice/template
2. current theme default slice/template
3. base theme alternate slice/template
4. base theme default slice/template
5. module-provided fallback
6. system fallback

## Template Naming Convention

Examples:
- `Layout.cshtml`
- `Menu.cshtml`
- `MenuItem.cshtml`
- `DashboardWidget.cshtml`
- `DashboardWidget__Blog.cshtml`
- `Content__BlogPost.cshtml`
- `Field__TextField.cshtml`

## Theme Discovery

Like modules, themes should be discoverable from:
- referenced assemblies
- RCLs
- themes folder on disk

## Theme Settings

Themes can declare configurable settings:
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

## Asset Handling

Themes can contribute static assets via `wwwroot`.

Requirements:
- cache-busting/version hashing
- CSP compatibility
- tenant-specific logo overrides
- optional CDN support

## Admin vs Frontend Themes

Support separation:
- Admin theme optimized for CMS UX
- Frontend theme optimized for public site

## Content Rendering

Rendering flow:
1. request resolves tenant
2. content item loaded
3. content display shapes constructed
4. current theme selected
5. shape templates resolved
6. HTML emitted

## Localization and Themes

Themes must respect current culture for:
- text resources
- localized menu labels
- directionality if RTL later supported
- culture-specific assets if needed

## Deliverables

1. theme descriptor model
2. theme discovery service
3. template lookup pipeline
4. tenant theme selection
5. theme settings support
6. asset versioning
7. tests for override precedence
