# Spec: L10n (UI Localization) for AeroCMS

## Objective

Add translatable UI strings throughout the AeroCMS **Manager (admin) interface** and **NeoUI component library** using standard ASP.NET Core localization patterns — `IStringLocalizer<T>`, `.resx` resource files, view localization, and data annotations localization.

This is distinct from the existing G11n (globalization) work (`docs/localization-implementation.md`) which handles **content-level** per-culture routing, document-per-culture entities, and RTL support for **public-facing sites**. Those are already well-implemented.

The breakdown:

| Layer | What it handles | For whom | Primary mechanism |
|---|---|---|---|
| **G11n** | Culture routing, content per culture, RTL, hreflang, sitemaps | Public site visitors | `AeroRequestCultureProvider`, document-per-culture |
| **L10n** | UI string translation — buttons, labels, menus, validation, dialogs | Admin/Manager users | `IStringLocalizer<T>`, `.resx`, view/DA localization |

**User stories:**

- As a French-speaking admin, I can switch the Manager UI to French so all buttons, labels, validation messages, and DataGrid headers appear in French.
- As a developer, I add a new Razor component with a localized button label by injecting `IStringLocalizer<MyComponent>` — no new infrastructure needed.
- As a NeoUI consumer, I can provide translated strings via standard ASP.NET Core `.resx` files instead of subclassing `DefaultLocalizer`.
- As a site admin, I can see translated validation errors when I submit an invalid form in the Manager.

## Tech Stack

- **Backend**: ASP.NET Core (.NET 10)
- **Existing patterns**: Railway-oriented programming (`Result<T>`, `Option<T>`), code-behind Razor files, no inline code in `.razor` files
- **Existing localization**: `AddLocalization()` is registered but **without** `AddViewLocalization()`, `AddDataAnnotationsLocalization()`, or any `.resx` files
- **NeoUI**: Custom `ILocalizer` / `DefaultLocalizer` (in-memory dictionary, not integrated with `IStringLocalizer<T>`)

## Architecture Decisions

### Decision: `.resx` resource files for UI strings (not JSON, not database)

**Rationale:** `.resx` is the ASP.NET Core standard. `IStringLocalizer<T>` resolves culture-specific resources automatically via `ResourceManager`. Tooling support exists in Visual Studio and `dotnet`. No custom framework needed.

Resource files live alongside the classes they localize (`ResourcesPath = "Resources"`), or embedded in the assembly via convention. We follow the standard convention: `Resources/Controllers/ControllerName.fr.resx`, `Resources/Views/ViewName.fr.resx`, etc.

For Blazor components, use `IStringLocalizer<TComponent>` with `.resx` files named after the component's full namespace path under `Resources/`.

### Decision: Bridge NeoUI `ILocalizer` to `IStringLocalizer<T>`

**Rationale:** NeoUI has its own `ILocalizer` interface. Rather than rewriting every NeoUI component to use `IStringLocalizer<T>` directly, create a bridge implementation `StringLocalizerBridge : ILocalizer` that wraps `IStringLocalizer<SharedNeoUIResources>` and looks up keys from `.resx` files. This keeps NeoUI components unchanged while plugging into the ASP.NET Core localization system.

The `DefaultLocalizer` remains as the fallback (key not found → returns key).

### Decision: Manager UI is the L10n scope; public sites remain G11n-driven

**Rationale:** Public-facing content is authored per-culture by editors — the content itself *is* the translation. The public site UI chrome (nav, footer, layout) is managed per-culture as NavMenuDocument / FooterDocument, which is already handled by G11n. Only the **Manager/admin interface** (which is the same app regardless of content culture) needs L10n of its own UI strings.

Some public-facing framework strings (search placeholder, pagination labels, cookie consent, "Powered by AeroCMS") could benefit from L10n but are out of scope for this phase.

### Decision: Register all three default culture providers as fallbacks after `AeroRequestCultureProvider`

**Rationale:** Currently `AddInitialRequestCultureProvider(new AeroRequestCultureProvider())` removes all default providers. For the Manager UI, which is not content-culture-scoped, a broader provider chain makes sense:

1. `AeroRequestCultureProvider` (URL prefix — highest priority)
2. `CookieRequestCultureProvider` (user-persisted preference)
3. `QueryStringRequestCultureProvider` (debug/testing override)
4. `AcceptLanguageHeaderRequestCultureProvider` (browser language — lowest)

The custom provider still wins when a URL culture is present. The fallback chain only activates for URLs without a culture prefix (e.g., `/manager/sites`).

### Decision: Scope `SupportedCultures` to actual site-supported cultures at middleware level

**Rationale:** Currently `CultureInfo.GetCultures(CultureTypes.SpecificCultures)` registers ~350 cultures. This bloats the cookie/set-culture UI and the `Accept-Language` matching. Instead, aggregate unique cultures across all active sites and use that as the middleware-level `SupportedCultures` list.

For sites without persisted cultures yet, default to `["en-US"]`.

## Implementation Plan

### Phase 1 — Infrastructure: Register missing localization services

**Task 1.1:** Add `AddViewLocalization()` and `AddDataAnnotationsLocalization()` to the service registration pipeline.

- **Acceptance:** Both registered. `IViewLocalizer` injectable in Razor views. `[Display]` and `[Required]` validation messages can be localized via `.resx` files.
- **Files:** `src/Aero.Cms.Web.Bootstrap/AeroCmsExtensions.cs`
- **Scope:** XS (1 file, 2 lines added)

```csharp
// Current:
services.AddLocalization();

// After:
services.AddLocalization(options => options.ResourcesPath = "Resources");
services.AddRazorPages()
    .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization();
```

**Task 1.2:** Add fallback culture providers to `AeroRequestCultureProvider` registration.

- **Acceptance:** `CookieRequestCultureProvider`, `QueryStringRequestCultureProvider`, and `AcceptLanguageHeaderRequestCultureProvider` are in the provider list after the custom provider. Provider order: custom → cookie → query → accept-language.
- **Files:** `src/Aero.Cms.Web.Bootstrap/AeroCmsExtensions.cs`
- **Scope:** XS (1 file, same location)

```csharp
// Replace the current single-provider approach:
options.AddInitialRequestCultureProvider(new AeroRequestCultureProvider());
// →
// The custom provider replaces the default list. Re-add defaults as fallbacks:
options.RequestCultureProviders.Clear();
options.RequestCultureProviders.Insert(0, new AeroRequestCultureProvider());
options.RequestCultureProviders.Insert(1, new CookieRequestCultureProvider());
options.RequestCultureProviders.Insert(2, new QueryStringRequestCultureProvider());
options.RequestCultureProviders.Insert(3, new AcceptLanguageHeaderRequestCultureProvider());
```

### Phase 2 — NeoUI: Bridge `ILocalizer` to ASP.NET Core localization

**Task 2.1:** Create a shared NeoUI resource marker class for `.resx`-based string resolution.

- **Acceptance:** A new `NeoUI.SharedResources` class exists that `IStringLocalizer<NeoUI.SharedResources>` can reference. No methods, just a marker.
- **Files:** `NeoUI/src/NeoUI.Blazor/Localization/NeoUI.SharedResources.cs` (new)
- **Scope:** XS (1 new file, ~5 lines)

**Task 2.2:** Create `StringLocalizerBridge : ILocalizer` that wraps `IStringLocalizer<NeoUI.SharedResources>`.

- **Acceptance:** `StringLocalizerBridge` implements `ILocalizer` and delegates to `IStringLocalizer<NeoUI.SharedResources>`. Falls back to the key string if `.resx` has no matching entry. Keys follow `ComponentName.PropertyName` convention (same as `DefaultLocalizer`).
- **Files:** `NeoUI/src/NeoUI.Blazor/Localization/StringLocalizerBridge.cs` (new)
- **Scope:** S (1 new file)

**Task 2.3:** Create `.resx` resource files for all NeoUI component strings.

- **Acceptance:** One `.resx` file per NeoUI component category (or a single shared file) containing all keys from `DefaultLocalizer._strings`. The `.resx` file(s) are placed in `NeoUI/src/NeoUI.Blazor/Resources/` and follow the `NeoUI.Blazor.Localization.NeoUI.SharedResources` naming convention.
- **Files:** `.resx` files in `NeoUI/src/NeoUI.Blazor/Resources/` (multiple new files, one per component category or one large shared file)
- **Scope:** M (generating `.resx` entries for ~260 keys)

**Task 2.4:** Register `StringLocalizerBridge` as the `ILocalizer` implementation in DI, overriding the scoped `DefaultLocalizer` registration.

- **Acceptance:** `services.AddScoped<ILocalizer, StringLocalizerBridge>()` replaces the current `AddScoped<ILocalizer, DefaultLocalizer>()`. `DefaultLocalizer` still exists as the fallback behavior inside the bridge when a key isn't found in `.resx`.
- **Files:** `NeoUI/src/NeoUI.Blazor/Extensions/ServiceCollectionExtensions.cs`
- **Scope:** XS (1 file, 1 line changed)

### Phase 3 — Manager UI: Localize all admin Razor components with `IStringLocalizer<T>`

**Task 3.1:** Create a `SharedResource` marker class for Manager-wide strings.

- **Acceptance:** `Aero.Cms.Shared.Localization.SharedResource` class exists. Used for strings shared across multiple Manager components.
- **Files:** `src/Aero.Cms.Shared/Localization/SharedResource.cs` (new)
- **Scope:** XS (1 file)

**Task 3.2:** Inject `IStringLocalizer<T>` into each Manager page/component and replace hardcoded UI strings.

- **Acceptance:** Every `.razor` and `.razor.cs` file in `src/Aero.Cms.Shared/Pages/Manager/` uses `@inject IStringLocalizer<PageName> Localizer` (or `IStringLocalizer<SharedResource>`) instead of hardcoded English strings. Pattern:

```razor
@* Before *@
<button class="btn">Save Changes</button>

@* After *@
<button class="btn">@Localizer["SaveChanges"]</button>
```

- **Files:** All files in `src/Aero.Cms.Shared/Pages/Manager/` (substantial number)
- **Scope:** L (systematic find-and-replace across dozens of files)

**Task 3.3:** Create corresponding `.resx` resource files for each Manager component.

- **Acceptance:** `.resx` files at `src/Aero.Cms.Shared/Resources/` mirroring the namespace hierarchy. Default (English) `.resx` contains all localized strings with the original English text as the value. Future translators create `.fr.resx`, `.es.resx`, etc.
- **Files:** Multiple `.resx` files under `src/Aero.Cms.Shared/Resources/`
- **Scope:** M (one `.resx` per component/page, bundled with the code changes)

**Task 3.4:** Localize DataAnnotations display names and validation messages.

- **Acceptance:** `[Display(Name = "...")]` and `[Required(ErrorMessage = "...")]` attributes on all Manager view models use named resources. `AddDataAnnotationsLocalization()` resolves them.
- **Files:** View model files and DTOs used by Manager pages
- **Scope:** M (systematic find-and-replace)

### Phase 4 — Culture provider chain + supported cultures scoping

**Task 4.1:** Scope middleware-level `SupportedCultures` to actually configured site cultures.

- **Acceptance:** Instead of `CultureInfo.GetCultures(CultureTypes.SpecificCultures)`, aggregate unique cultures from all active sites. If no sites exist yet, default to `["en-US"]`. This ensures the cookie dropdown, accept-language matching, and neutral culture resolution are scoped to cultures that actually exist.
- **Files:** `src/Aero.Cms.Web.Bootstrap/AeroCmsExtensions.cs` (the `UseRequestLocalization` lambda), possibly a new service that resolves site cultures
- **Scope:** M (requires coordinating site resolution with middleware startup)

**Task 4.2:** Add culture persistence cookie to the Manager layout.

- **Acceptance:** The Manager layout includes a culture selector that, on change, sets `CookieRequestCultureProvider.DefaultCookieName` with the selected culture. This allows the `CookieRequestCultureProvider` fallback to pick it up on subsequent requests.
- **Files:** `src/Aero.Cms.Shared/Layouts/ManagerLayout.razor.cs` (new SetLanguage method), `ManagerLayout.razor` (add culture selector inline or via existing `CultureSwitcher`)
- **Scope:** S (2-3 files)

## Files affected summary

| Phase | Files Created | Files Modified | Total |
|---|---|---|---|
| Phase 1: Infrastructure | 0 | 1 | 1 |
| Phase 2: NeoUI bridge | 3 | 1 + `.resx` files | 4+ |
| Phase 3: Manager UI L10n | ~1 (SharedResource) + `.resx` files | ~50+ | 50+ |
| Phase 4: Provider chain + scoping | 0 | 2-3 | 2-3 |
| **Total** | **4+** | **~55+** | **~60+** |

## Validation

- [ ] Build succeeds: `dotnet build` with no errors
- [ ] NeoUI components show translated strings from `.resx` instead of hardcoded English when `CurrentUICulture` is set to a non-English culture
- [ ] Manager pages show all UI text (buttons, labels, menus, column headers) localized for the selected culture
- [ ] Validation error messages in Manager forms respect `CurrentUICulture`
- [ ] Changing culture via Manager layout selector persists across page refreshes (cookie)
- [ ] URL-cultured requests (`/es-mx/manager/sites`) still work and also show localized UI from `.resx`
- [ ] Non-cultured Manager URLs (`/manager/sites`) use cookie or accept-language fallback for UI language
- [ ] Query string `?culture=fr` overrides the Manager UI culture for testing
- [ ] Existing public-site G11n (content routing, RTL, document-per-culture) is unaffected
- [ ] `rg` search across Manager Razor files for bare English string literals that should be localized → team-reviewed exceptions only
- [ ] All existing tests pass: `dotnet test`

---

*Last updated: 2026-05-31*
