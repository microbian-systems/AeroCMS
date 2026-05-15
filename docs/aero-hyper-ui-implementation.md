# Aero HyperUI Migration Plan

> Migrate 21 HyperUI marketing component types (122 variants) from static HTML/Tailwind into an ASP.NET Core Blazor RCL (`Aero.Cms.Ui.Hyper`) targeting **Static SSR** with **Tailwind CSS v4 via CDN**, **`dark:` class-based dark mode**, and **logical-property-based RTL support**.

---

## 1. Architecture Alignment

### 1.1 Relationship to Existing Aero CMS Architecture

This RCL delivers **public-facing static SSR Blazor components** — marketing page sections that produce read-only DOM. These components are consumed by the public rendering pipeline defined in:

- [`aero-page-document-refactor.md`](aero-page-document-refactor.md) — The published `PageDocument.LayoutRegions` manifest drives public rendering via `BlockPlacement` references
- [`aero-page-refactor-plans.md`](aero-page-refactor-plans.md) — Phase 8 (Neo Blocks Expansion) calls for Feature Grid, Pricing, Testimonials, FAQ, Blog Grid, etc.

**Integration point:** Each HyperUI component type maps to a candidate `BlockBase` subtype or a reusable Razor component that can be invoked from `BlockPlacementRenderer` or composed into Neo blocks.

### 1.2 Constraints from AGENTS.md

| Rule | Application |
|---|---|
| **CDN first** | Tailwind CSS v4 via Play CDN (`<script src="https://cdn.jsdelivr.net/npm/@tailwindcss/browser@4">`) — no npm, no build tool |
| **Prefer Blazor/Razor over JS** | Pure `.razor` components; no JavaScript for core rendering |
| **Code-behind preferred** | Each `.razor` file paired with `.razor.cs` code-behind where parameters or logic exist |
| **Tailwind CSS** | Already the project's CSS framework; HyperUI is Tailwind-native |
| **FluentValidation** | Validation for form components (ContactForms, NewsletterSignup, Polls) |
| **Source generators** | If component discovery/catalog metadata is needed, use source generators (not reflection) |
| **SOLID / GoF** | Strategy pattern for variant selection; Decorator for dark-mode wrapping |

---

## 2. RCL Project Setup

### 2.1 Project File

**Path:** `src/Aero.Cms.Ui.Hyper/Aero.Cms.Ui.Hyper.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">

  <PropertyGroup>
    <RootNamespace>Aero.Cms.Ui.Hyper</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <SupportedPlatform Include="browser" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Components.Web" />
  </ItemGroup>

  <!-- Static assets served via _content path -->
  <ItemGroup>
    <None Include="wwwroot\**" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

</Project>
```

No direct project references needed — this is a leaf RCL. Components receive data via `[Parameter]` only. The consuming project (`Aero.Cms.Web` or `Aero.Cms.Shared`) references this RCL.

### 2.2 Solution Registration

Add to `src/Aero.Cms.slnx`:
```xml
<Project Path="Aero.Cms.Ui.Hyper/Aero.Cms.Ui.Hyper.csproj" />
```

### 2.3 _Imports.razor

```razor
@using Microsoft.AspNetCore.Components.Web
@using Aero.Cms.Ui.Hyper
@using Aero.Cms.Ui.Hyper.Components
```

### 2.4 Static Assets

```
wwwroot/
└── hyper/
    ├── hyper.css             (optional: RCL-bundled Tailwind utilities)
    └── README.md             (asset versioning notes)
```

Tailwind CSS is delivered via CDN by the **host page** (not the RCL). The RCL documents its CDN requirement. If a bundled CSS is needed, the host project includes the Tailwind CDN or build output.

---

## 3. Folder Structure

```
src/Aero.Cms.Ui.Hyper/
├── Aero.Cms.Ui.Hyper.csproj
├── _Imports.razor
├── wwwroot/
│   └── hyper/
├── Components/
│   ├── Announcements/
│   │   ├── Announcement1.razor        (Base)
│   │   ├── Announcement1.razor.cs
│   │   ├── Announcement2.razor        (Base with dismiss)
│   │   ├── Announcement2.razor.cs
│   │   ├── Announcement3.razor        (Fixed)
│   │   ├── Announcement3.razor.cs
│   │   ├── Announcement4.razor        (Fixed with dismiss)
│   │   ├── Announcement4.razor.cs
│   │   ├── Announcement5.razor        (Floating)
│   │   ├── Announcement5.razor.cs
│   │   ├── Announcement6.razor        (Floating with dismiss)
│   │   └── Announcement6.razor.cs
│   ├── Banners/
│   │   ├── Banner1.razor              (Center)
│   │   ├── Banner1.razor.cs
│   │   ├── Banner2.razor              (Left)
│   │   ├── Banner2.razor.cs
│   │   ├── Banner3.razor              (Left with image)
│   │   └── Banner3.razor.cs
│   ├── BlogCards/
│   │   ├── BlogCard1.razor            (Bordered w/ image, shadow on hover)
│   │   ├── BlogCard1.razor.cs
│   │   ├── BlogCard2.razor            (Floating image w/ title & excerpt)
│   │   ├── BlogCard2.razor.cs
│   │   ├── BlogCard3.razor            (Bordered w/ image, CTA)
│   │   ├── BlogCard3.razor.cs
│   │   ├── BlogCard4.razor            (Gradient border, animated hover)
│   │   ├── BlogCard4.razor.cs
│   │   ├── BlogCard5.razor            (Bordered w/ icon, shadow on hover)
│   │   ├── BlogCard5.razor.cs
│   │   ├── BlogCard6.razor            (Artistic rotated date)
│   │   ├── BlogCard6.razor.cs
│   │   ├── BlogCard7.razor            (Background image w/ overlay)
│   │   └── BlogCard7.razor.cs
│   ├── Buttons/
│   │   ├── Button1.razor              (Base, solid & bordered)
│   │   └── ... (12 variants total)
│   ├── Cards/
│   │   ├── Card1.razor                (Title, author, excerpt)
│   │   └── ... (9 variants total)
│   ├── Carts/
│   │   ├── Cart1.razor                (Popup)
│   │   └── ... (3 variants total)
│   ├── ContactForms/
│   │   ├── ContactForm1.razor         (Base)
│   │   └── ... (5 variants total)
│   ├── Ctas/
│   │   ├── Cta1.razor                 (Content left, image right)
│   │   └── ... (4 variants total)
│   ├── EmptyContent/
│   │   ├── EmptyContent1.razor        (No search results)
│   │   └── ... (5 variants total)
│   ├── Faqs/
│   │   ├── Faq1.razor                 (Base with chevrons)
│   │   ├── Faq2.razor                 (Divided with chevrons)
│   │   └── Faq3.razor                 (Background)
│   ├── FeatureGrids/
│   │   ├── FeatureGrid1.razor         (Grid with content)
│   │   ├── FeatureGrid1.razor.cs
│   │   ├── FeatureGrid2.razor         (List with content)
│   │   ├── FeatureGrid3.razor         (Simple grid)
│   │   └── FeatureGrid4.razor         (Grid with list items)
│   ├── Footers/
│   │   ├── Footer1.razor              (Large w/ newsletter)
│   │   └── ... (12 variants total)
│   ├── Headers/
│   │   ├── Header1.razor              (Icon left, CTAs right)
│   │   └── ... (4 variants total)
│   ├── LogoClouds/
│   │   ├── LogoCloud1.razor           (Base)
│   │   └── ... (4 variants total)
│   ├── NewsletterSignup/
│   │   ├── NewsletterSignup1.razor    (Simple signup)
│   │   └── NewsletterSignup2.razor    (Simple signup centered)
│   ├── Polls/
│   │   ├── Poll1.razor                (Single question)
│   │   ├── Poll2.razor                (Multiple choice survey)
│   │   └── Poll3.razor                (Rating poll)
│   ├── Pricing/
│   │   ├── Pricing1.razor             (Tier, price, features, CTA)
│   │   └── Pricing2.razor             (Tier, description, price, CTA, features)
│   ├── ProductCards/
│   │   ├── ProductCard1.razor         (Image, title, price)
│   │   └── ... (8 variants total)
│   ├── ProductCollections/
│   │   ├── ProductCollection1.razor   (Base)
│   │   └── ... (4 variants total)
│   ├── Sections/
│   │   ├── Section1.razor             (Content+image, 1/2 grid)
│   │   └── ... (4 variants total)
│   └── TeamSections/
│       ├── TeamSection1.razor         (Base)
│       ├── TeamSection2.razor         (Base with description)
│       └── TeamSection3.razor         (Small)
```

---

## 4. Component Conversion Pattern

### 4.1 Simple Parameterized Component

Most HyperUI components are presentation-only with fixed text. Parameters extract CMS-injectable content.

**Source (HyperUI HTML):** `public/examples/marketing/feature-grids/1.html`
```html
<div class="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
  <div class="mx-auto max-w-lg text-center">
    <h2 class="text-3xl/tight font-bold text-gray-900 sm:text-4xl">
      Features for growth
    </h2>
    <p class="mt-4 text-lg text-pretty text-gray-700">
      Lorem ipsum dolor sit amet...
    </p>
  </div>
  <div class="mt-8 grid grid-cols-1 gap-8 md:grid-cols-3">
    <!-- feature cards -->
  </div>
</div>
```

**Target (Blazor .razor with code-behind):**

`FeatureGrid1.razor`:
```razor
@* Grid with content - HyperUI marketing/feature-grids/1 *@
<div class="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
    <div class="mx-auto max-w-lg text-center">
        <h2 class="text-3xl/tight font-bold text-gray-900 dark:text-white sm:text-4xl">
            @Title
        </h2>
        <p class="mt-4 text-lg text-pretty text-gray-700 dark:text-gray-200">
            @Description
        </p>
    </div>
    <div class="mt-8 grid grid-cols-1 gap-8 md:grid-cols-3">
        @ChildContent
    </div>
</div>
```

`FeatureGrid1.razor.cs`:
```csharp
namespace Aero.Cms.Ui.Hyper.Components.FeatureGrids;

public partial class FeatureGrid1
{
    [Parameter] public string Title { get; set; } = "Features";
    [Parameter] public string Description { get; set; } = "";
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
```

### 4.2 Light + Dark Variant Merge Strategy

HyperUI stores dark variants in separate `1-dark.html` files. We merge them using Tailwind's `dark:` prefix:

| Light class | Dark class | Merged class |
|---|---|---|
| `text-gray-900` | `dark:text-white` | `text-gray-900 dark:text-white` |
| `text-gray-700` | `dark:text-gray-200` | `text-gray-700 dark:text-gray-200` |
| `bg-white` | `dark:bg-gray-900` | `bg-white dark:bg-gray-900` |
| `border-gray-200` | `dark:border-gray-700` | `border-gray-200 dark:border-gray-700` |
| `bg-gray-100` | `dark:bg-gray-800` | `bg-gray-100 dark:bg-gray-800` |

### 4.3 Form Components (Static SSR Compatible)

Forms use plain `<form>` + `<EditForm>` patterns compatible with static SSR (MS Learn: forms work across render modes when using `@onsubmit`).

`ContactForm1.razor`:
```razor
@* Base contact form - HyperUI marketing/contact-forms/1 *@
<EditForm Model="@Model" OnValidSubmit="HandleSubmit" FormName="contact-form"
          Enhance method="post" class="...">
    <DataAnnotationsValidator />
    <div>
        <label for="name" class="block text-sm font-medium text-gray-900 dark:text-gray-100">@NameLabel</label>
        <InputText id="name" @bind-Value="Model.Name"
            class="mt-1 w-full rounded-lg border-gray-300 focus:border-indigo-500 focus:outline-hidden dark:border-gray-600 dark:bg-gray-800 dark:text-white" />
    </div>
    <div>
        <label for="email" class="block text-sm font-medium text-gray-900 dark:text-gray-100">@EmailLabel</label>
        <InputText id="email" @bind-Value="Model.Email"
            class="mt-1 w-full rounded-lg border-gray-300 ..." />
    </div>
    <div>
        <label for="message" class="block text-sm font-medium text-gray-900 dark:text-gray-100">@MessageLabel</label>
        <InputTextArea id="message" @bind-Value="Model.Message" rows="4"
            class="mt-1 w-full resize-none rounded-lg border-gray-300 ..." />
    </div>
    <button type="submit"
        class="block w-full rounded-lg border border-indigo-600 bg-indigo-600 px-12 py-3 text-sm font-medium text-white transition-colors hover:bg-transparent hover:text-indigo-600 dark:hover:text-indigo-400">
        @SubmitLabel
    </button>
</EditForm>
```

`ContactForm1.razor.cs`:
```csharp
using System.ComponentModel.DataAnnotations;
using FluentValidation;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Ui.Hyper.Components.ContactForms;

public partial class ContactForm1
{
    [Parameter] public string NameLabel { get; set; } = "Name";
    [Parameter] public string EmailLabel { get; set; } = "Email";
    [Parameter] public string MessageLabel { get; set; } = "Message";
    [Parameter] public string SubmitLabel { get; set; } = "Send Message";
    [Parameter] public EventCallback<ContactFormModel> OnSubmit { get; set; }

    [SupplyParameterFromForm] private ContactFormModel Model { get; set; } = new();

    private async Task HandleSubmit()
    {
        await OnSubmit.InvokeAsync(Model);
    }
}

public class ContactFormModel
{
    [Required, StringLength(200)] public string Name { get; set; } = "";
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, StringLength(5000)] public string Message { get; set; } = "";
}
```

### 4.4 Repeating-Item Components (Cards, Feature Grid Items)

For components with repeating child items (feature grid cards, blog cards, product cards, FAQ items), use `RenderFragment` slots or typed `[Parameter]` collections.

**Pattern A — RenderFragment slot (flexible, arbitrary content):**
```razor
@* FeatureGrid1.razor *@
<div class="mt-8 grid grid-cols-1 gap-8 md:grid-cols-3">
    @ChildContent
</div>
@code {
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
```

**Pattern B — Typed data model (structured, CMS-friendly):**
```razor
@* Pricing1.razor *@
@foreach (var tier in Tiers)
{
    <div class="@(tier.Highlighted ? "ring-2 ring-indigo-600" : "") rounded-2xl border border-gray-200 p-8 dark:border-gray-700">
        <h3 class="text-lg font-semibold text-gray-900 dark:text-white">@tier.Name</h3>
        <p class="mt-4 text-gray-700 dark:text-gray-200">@tier.Description</p>
        <p class="mt-4">
            <span class="text-4xl font-bold text-gray-900 dark:text-white">@tier.Price</span>
        </p>
        <ul class="mt-6 space-y-2">
            @foreach (var feature in tier.Features)
            {
                <li class="flex items-center gap-2 text-gray-700 dark:text-gray-200">
                    <svg class="size-5 text-green-500">...</svg>
                    @feature
                </li>
            }
        </ul>
        <a href="@tier.CtaUrl" class="mt-8 block rounded-lg bg-indigo-600 px-6 py-3 text-center text-sm font-medium text-white hover:bg-indigo-700">
            @tier.CtaText
        </a>
    </div>
}
@code {
    [Parameter] public IReadOnlyList<PricingTier> Tiers { get; set; } = [];
}

public record PricingTier(
    string Name, string Description, string Price, string CtaText, string CtaUrl,
    List<string> Features, bool Highlighted = false);
```

---

## 5. RTL Support Strategy

> **Mandate:** ALL HyperUI components MUST support LTR and RTL out of the box. No component may ship with physical-only CSS classes. RTL is a first-class design constraint, not an afterthought.

### 5.1 NeoUI RTL Analysis Summary

NeoUI has **no centralized RTL service or configuration**. RTL is handled through:

1. **HTML `dir` attribute** — set `<html dir="rtl">` or `<div dir="rtl">` on containers
2. **Tailwind logical properties** — `me-*`/`ms-*`, `ps-*`/`pe-*` auto-flip with `dir`
3. **`gap-*` instead of `space-x-*`** — gap is direction-agnostic
4. **`inset-inline-start/end` instead of `left/right`** — auto-flips
5. **`IconPosition.Start`/`End`** — semantic icon positioning (RTL-aware naming)
6. **`KeyboardNavigator.IsRtl()`** — checks `CultureInfo.CurrentCulture.TextInfo.IsRightToLeft` for arrow key reversal

NeoUI does NOT have:
- A `Direction` enum or `IRtlService`
- A cascading `Direction` parameter
- Theme-integrated RTL configuration
- A `Dir` parameter on most components (only `DropdownMenu` has one)

### 5.2 HyperUI RTL Conversion Rules

Since HyperUI components are pure HTML, RTL is achieved entirely through CSS class choices:

| Original Class (physical) | RTL-Aware Replacement (logical) |
|---|---|
| `ml-2` / `ml-4` | `ms-2` / `ms-4` (margin-inline-start) |
| `mr-2` / `mr-4` | `me-2` / `me-4` (margin-inline-end) |
| `pl-2` / `pl-4` | `ps-2` / `ps-4` (padding-inline-start) |
| `pr-2` / `pr-4` | `pe-2` / `pe-4` (padding-inline-end) |
| `space-x-2` / `space-x-4` | `gap-2` / `gap-4` (direction-agnostic gap) |
| `left-0` / `left-1` | `inset-inline-start-0` / `inset-inline-start-1` |
| `right-0` / `right-1` | `inset-inline-end-0` / `inset-inline-end-1` |
| `text-left` | `text-start` |
| `text-right` | `text-end` |
| `rounded-l-*` | `rounded-s-*` |
| `rounded-r-*` | `rounded-e-*` |
| `border-l-*` | `border-s-*` |
| `border-r-*` | `border-e-*` |

### 5.3 RTL Handling in Blazor Razor

**Pattern — All CSS classes use logical properties:**
```razor
@* BEFORE (physical - breaks RTL): *@
<div class="flex items-center space-x-4">
    <span class="mr-2">Icon</span>
    <span class="ml-auto">Right-aligned</span>
</div>

@* AFTER (logical - RTL-aware): *@
<div class="flex items-center gap-4">
    <span class="me-2">Icon</span>
    <span class="ms-auto">End-aligned</span>
</div>
```

**Pattern — Directional text alignment:**
```razor
@* BEFORE: *@
<h2 class="text-left sm:text-right">Title</h2>

@* AFTER: *@
<h2 class="text-start sm:text-end">Title</h2>
```

**Pattern — Direction-agnostic grid/flex:**
```razor
@* Grid columns auto-reverse in RTL — no class change needed *@
<div class="grid grid-cols-1 md:grid-cols-3 gap-8">
    @* Items flow naturally in both LTR and RTL *@
</div>

@* Flex row auto-reverses in RTL — use gap, not space-x *@
<div class="flex items-center gap-4">
    @* Items flow naturally in both LTR and RTL *@
</div>
```

### 5.4 RTL in Code-Behind (Keyboard Navigation / JS)

If a component needs programmatic RTL awareness (rare for presentation components):

```csharp
// In a .razor.cs code-behind
using System.Globalization;

private bool IsRtl => CultureInfo.CurrentCulture.TextInfo.IsRightToLeft;

// Example: reversing arrow key navigation in a carousel
private void HandleKeyDown(KeyboardEventArgs e)
{
    int delta = e.Key switch
    {
        "ArrowLeft" => IsRtl ? 1 : -1,
        "ArrowRight" => IsRtl ? -1 : 1,
        _ => 0
    };
    // ... apply delta
}
```

For JavaScript:
```javascript
// From NeoUI's range-slider.js pattern:
const isRtl = getComputedStyle(container).direction === 'rtl';
```

### 5.5 RTL Validation Checklist (per component)

- [ ] No `ml-` / `mr-` — replaced with `ms-` / `me-`
- [ ] No `pl-` / `pr-` — replaced with `ps-` / `pe-`
- [ ] No `space-x-*` — replaced with `gap-*`
- [ ] No `left-*` / `right-*` — replaced with `inset-inline-start-*` / `inset-inline-end-*`
- [ ] No `text-left` / `text-right` — replaced with `text-start` / `text-end`
- [ ] No `rounded-l-` / `rounded-r-` — replaced with `rounded-s-` / `rounded-e-`
- [ ] No `border-l-` / `border-r-` — replaced with `border-s-` / `border-e-`
- [ ] Grid columns / flex rows use `gap` for spacing (direction-agnostic)
- [ ] Flex direction is intentional (no unintended `flex-row-reverse`)
- [ ] Any inline SVG icons use `me-`/`ms-` not `mr-`/`ml-`
- [ ] `float-left` / `float-right` addressed (rare in Tailwind, but check)

---

## 6. Tailwind CSS v4 Integration

### 6.1 Play CDN Strategy

Per AGENTS.md "CDN first" rule. Tailwind v4 uses the **Play CDN** (different from the v3 CDN URL):

**Host page includes in `<head>`:**
```html
<script src="https://cdn.jsdelivr.net/npm/@tailwindcss/browser@4"></script>
```

**Dark mode configuration (v4 CSS-first approach):**

Tailwind v4 removes the JS-based `tailwind.config = { darkMode: 'class' }` API. Dark mode is configured entirely via CSS using `@custom-variant`. The host page adds a `<style type="text/tailwindcss">` block:

```html
<style type="text/tailwindcss">
  @custom-variant dark (&:where(.dark, .dark *));
</style>
```

This tells Tailwind to compile `dark:` prefixed classes (e.g., `dark:text-white`) to apply when an ancestor element has `class="dark"`.

**How dark mode is activated:**
```html
<html lang="en" class="dark" dir="ltr">
```

The `class="dark"` on `<html>` activates all `dark:` classes. Toggling between light and dark is done by removing/adding the `dark` class on the `<html>` element — no JS config needed.

**Custom theme configuration via CSS:**

Any `tailwind.config = { theme: { ... } }` JS config from v3 is replaced with CSS `@theme` directives:

```html
<style type="text/tailwindcss">
  @custom-variant dark (&:where(.dark, .dark *));
  @theme {
    --font-sans: 'Google Sans Flex', sans-serif;
  }
</style>
```

**RTL support:**
```html
<html lang="en" class="dark" dir="ltr">
```

Tailwind v4 automatically detects the `dir` attribute and applies logical property classes (`me-*`, `ms-*`, `ps-*`, `pe-*`, `inset-inline-*`) correctly with the Play CDN.

### 6.2 Tailwind v3 → v4 Class Migration

HyperUI source HTML uses some Tailwind v3 class names. These must be migrated to v4 equivalents when creating Razor components:

| v3 Class (source HTML) | v4 Class (target .razor) | Notes |
|---|---|---|
| `shadow-sm` | `shadow-xs` | v4 renamed `shadow-xs` to `shadow-sm`; former `shadow-sm` → `shadow-xs` |
| `shadow` | `shadow-sm` | Default shadow |
| `shadow-md` | `shadow-md` | Unchanged |
| `shadow-lg` | `shadow-lg` | Unchanged |
| `shadow-xl` | `shadow-xl` | Unchanged |
| `shadow-2xl` | `shadow-2xl` | Unchanged |
| `rounded-sm` | `rounded-xs` | v4 renamed: `rounded-xs`→`rounded-sm`, `rounded-sm`→`rounded-xs` |
| `rounded` | `rounded-sm` | Default rounding |
| `rounded-md` | `rounded-md` | Unchanged |
| `rounded-lg` | `rounded-lg` | Unchanged |
| `rounded-xl` | `rounded-xl` | Unchanged |
| `rounded-2xl` | `rounded-2xl` | Unchanged |
| `border-gray-300` | `border-gray-300` | Can be omitted if using default border color |
| `outline-none` | `outline-hidden` | v4 renamed for clarity |
| `ring` | `ring-3` | v4 makes ring width explicit |
| `ring-1` | `ring-1` | Unchanged |
| `ring-2` | `ring-2` | Unchanged |
| `ring-offset-2` | `ring-offset-2` | Unchanged |
| `bg-gradient-to-r` | `bg-linear-to-r` | v4 renamed gradient to linear |
| `bg-gradient-to-br` | `bg-linear-to-br` | v4 renamed |
| `transition` | `transition` | Unchanged |
| `transition-colors` | `transition-colors` | Unchanged |
| `transition-all` | `transition-all` | Unchanged |
| `duration-150` | `duration-150` | Unchanged |
| `ease-in-out` | `ease-in-out` | Unchanged |
| `sr-only` | `sr-only` | Unchanged |
| `focus:ring-2` | `focus:ring-2` | Unchanged |
| `focus:ring-indigo-500` | `focus:ring-indigo-500` | Unchanged |
| `focus:outline-none` | `focus:outline-hidden` | v4 rename |
| `focus:border-transparent` | `focus:border-transparent` | Unchanged |
| `focus-within:ring-3` | `focus-within:ring-3` | v4: `ring-3` is explicit width |

**General principle:** Apply these replacements via search-and-replace when converting each `.html` file to `.razor`. Most utility classes are unchanged — only shadow, rounded, outline, and gradient class names shifted.

### 6.3 Font Loading

HyperUI uses "Google Sans Flex" font. Include via CDN in the host page:
```html
<link href="https://fonts.googleapis.com/css2?family=Google+Sans+Flex:opsz,wght@6..144,1..1000&display=swap" rel="stylesheet">
```

Configure in the Tailwind v4 CSS:
```html
<style type="text/tailwindcss">
  @theme {
    --font-sans: 'Google Sans Flex', sans-serif;
  }
</style>
```

Alternatively, the consumer app can use its own font stack — all HyperUI components use `font-sans` (Tailwind default).

### 6.4 CSS Customization Surface

Components expose no inline styles. All styling is via Tailwind utility classes. Consumer apps can:
1. Override Tailwind theme via `@theme` CSS block
2. Wrap components in containers with custom classes
3. Use Tailwind's `@layer` for custom component variants

---

## 7. Component Variant Strategy

### 7.1 When to Create Separate Components vs Parameters

| Situation | Approach |
|---|---|
| Different layout structure | **Separate component** (e.g., `Footer1` vs `Footer3`) |
| Same layout, minor visual diff | **Same component + `bool`/`enum` parameter** |
| Same layout, different content slots | **Same component + `RenderFragment` parameters** |
| Different number of items/sections | **Separate component** or **item list parameter** |
| Same HTML but with/without specific section | **Optional `RenderFragment` parameter** (`null` = hidden) |

### 7.2 Variant Discovery Convention

Each `.razor` file includes a comment header identifying the source:

```razor
@* FeatureGrid1 - Grid with content *@
@* Source: hyperui/public/examples/marketing/feature-grids/1.html (+ 1-dark.html) *@
@* Component type: marketing/feature-grids | Variant: 1 of 4 *@
```

---

## 8. Integration with Aero CMS Block System

### 8.1 Architecture: Option A — Full CMS Integration (Recommended)

The existing Aero CMS block pipeline follows this pattern:
```
BlockBase subclass ([BlockMetadata]) 
  → Renderer .razor + .cs marker ([CmsBlockRenderer]) 
      → BlockRendererGenerator (source generator)
        → auto-emits: render adapters and package render registry
```

HyperUI components integrate using **Option A** (council-verified): each HyperUI component type becomes a `BlockBase` subclass owned by the `Aero.Cms.Ui.Hyper` package, with a static SSR renderer, mapper, editor preview, modal editor, and runtime editor definition in the same vertical slice.

The stable cross-package contracts live in `Aero.Cms.Abstractions`:

- `BlockBase`
- `BlockMetadataAttribute`
- `CmsBlockRendererAttribute`
- `ICmsBlockModelProvider`
- `IPageEditorBlockDefinition`
- `IPageEditorBlockProvider`

`Aero.Cms.Shared` owns the PageEditor shell and rendering host. It should not be edited for every Hyper block. A Hyper package registers its editor definitions, block model provider, and generated renderer registry through one host call:

```csharp
services.AddAeroCmsHyperUiBlocks();
```

**Why not Option B (generic dispatcher)?** A single `HyperUiBlock` with catalog dispatch breaks the source generator's 1:1 contract between model type and renderer type. The `BlockRendererGenerator` expects `[BlockMetadata]` on each model and `[CmsBlockRenderer]` on a C# renderer marker for each renderer — a god dispatcher cannot participate in compile-time discovery.

**Why not Option C (horizontal integration)?** Requiring each block to touch `Aero.Cms.Abstractions`, `Aero.Cms.Shared`, and the web host makes third-party UI packages painful to adopt. The vertical package model keeps the block implementation, editor behavior, and public renderer together.

### 8.2 Block Model Location & Naming Convention

| Artifact | Location | File Pattern |
|---|---|---|
| **Block models** | `src/Aero.Cms.Ui.Hyper/Blocks/{Slice}/` | `{ComponentType}Block.cs` (e.g., `FeatureGrid1Block.cs`) |
| **Block item models** | Same file or `*.Items.cs` | `FeatureGridItem.cs`, `PricingTier.cs` |
| **Renderer components** | Same slice folder | `{ComponentType}BlockRenderer.razor` |
| **Renderer marker** | Same slice folder or package marker file | `.cs` partial declaration with `[CmsBlockRenderer(...)]` |
| **Mapper** | Same slice folder | `{ComponentType}BlockMapper.cs` |
| **Editor preview** | Same slice folder | `{ComponentType}BlockEditorPreview.razor` |
| **Modal editor** | Same slice folder | `{ComponentType}BlockEditor.razor` |
| **Editor definition** | Same slice folder | `{ComponentType}EditorBlockDefinition.cs` |
| **Package provider** | package root | `HyperPageEditorBlockProvider.cs` |
| **Package registration** | package root | `HyperUiServiceCollectionExtensions.cs` |

**Catalog ID convention:** `hyper.{slug}.{variant}` (e.g., `hyper.feature-grid.1`, `hyper.footer.large-with-newsletter`)

**Block metadata attributes:**
- `[BlockMetadata("hyper.feature-grid.1", "Feature Grid 1", Category = "Marketing")]` on the model
- `[CmsBlockRenderer(typeof(FeatureGrid1Block))]` on a `.cs` partial marker for the renderer component

### 8.3 Concrete Example: Feature Grid 1 Block

#### Step 1 — Block Model (`src/Aero.Cms.Ui.Hyper/Blocks/FeatureGrids/`)

```csharp
using Aero.Cms.Abstractions.Blocks;

namespace Aero.Cms.Ui.Hyper.Blocks.FeatureGrids;

[BlockMetadata(
    "hyper.feature-grid.1",
    "Feature Grid 1",
    Category = "Marketing",
    Icon = "layout-grid",
    SortOrder = 100,
    SchemaVersion = 1)]
public sealed class FeatureGrid1Block : BlockBase
{
    public override string BlockType => "hyper.feature-grid.1";

    public string Title { get; set; } = "Features";
    public string Description { get; set; } = "";
    public List<FeatureGridItem> Items { get; set; } = [];

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

public sealed class FeatureGridItem
{
    public string Icon { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string? LinkUrl { get; set; }
}
```

#### Step 2 — Renderer Component (`src/Aero.Cms.Ui.Hyper/Blocks/FeatureGrids/`)

**`FeatureGrid1BlockRenderer.razor`:**
```razor
@* Wraps HyperUI FeatureGrid1 with CMS block data *@
@using Aero.Cms.Ui.Hyper.Components.FeatureGrids

@if (Block != null)
{
    <FeatureGrid1 Title="@Block.Title" Description="@Block.Description">
        @foreach (var item in Block.Items)
        {
            <FeatureGridCard Icon="@item.Icon" Title="@item.Title"
                             Description="@item.Description" LinkUrl="@item.LinkUrl" />
        }
    </FeatureGrid1>
}

@code {
    [Parameter, EditorRequired]
    public FeatureGrid1Block? Block { get; set; }
}
```

**`FeatureGrid1BlockRendererMarker.cs`:**
```csharp
using Aero.Cms.Abstractions.Blocks.Rendering;

namespace Aero.Cms.Ui.Hyper.Blocks.FeatureGrids;

[CmsBlockRenderer(typeof(FeatureGrid1Block))]
public partial class FeatureGrid1BlockRenderer;
```

The marker can also live in a package-level `RendererMarkers.cs`. The important rule is that renderer discovery uses a normal `.cs` partial declaration; do not rely on Razor `@attribute` for package-level block discovery.

#### Step 3 — Editor Definition and Provider

Each slice adds one `IPageEditorBlockDefinition` implementation. The package-level `HyperPageEditorBlockProvider` returns all definitions and block model registrations, and `services.AddAeroCmsHyperUiBlocks()` registers that provider plus the package's generated renderer registry.

#### Step 4 — Source Generator Auto-Discovery

The `BlockRendererGenerator` reads the model and renderer attributes at compile time and emits:
- `CmsBlockRendering.g.cs` — generates `FeatureGrid1BlockRenderAdapter : ICmsBlockRenderAdapter<FeatureGrid1Block>` and registers in `CmsBlockRenderRegistry`
- `GeneratedCmsBlockRenderRegistry` — exposes the package's generated registry for DI registration
- `ICmsBlockModelProvider` from the package supplies Marten subclass mapping for package-owned `BlockBase` subtypes

### 8.4 Variant Handling Strategy

Not every variant needs its own block type. The existing decision matrix from Section 7.1 applies:

| Situation | Block Model Strategy | Renderer Strategy |
|---|---|---|
| **Different layout structure** (e.g., `Footer1` vs `Footer3`) | **Separate block types** — one `BlockBase` subclass per layout | Each renderer wraps the corresponding HyperUI component |
| **Same layout, minor visual diff** (e.g., `Cta1` vs `Cta2`) | **Single block type + enum parameter** — `CtaBlock.Variant = CtaVariant.ImageRight` | Renderer switches on `Variant` to dispatch |
| **Same layout, different content slots** | **Single block type + `List<T>` parameter** | Renderer renders the list |

**Count estimate for 21 types × 122 variants:**

| Strategy | Block Models | Variants |
|---|---|---|
| 1 block type per unique layout | ~21 | — |
| Consolidated via enum parameter | ~8 | ~4 each |
| **Total concrete block models** | **~29** | — |

### 8.5 Immediate Consumption Path

Before CMS block integration, components are consumable directly:

```razor
@* In a Razor page or layout *@
<FeatureGrid1 Title="Why Choose Us" Description="Our platform delivers...">
    <FeatureGridCard Icon="zap" Title="Fast" Description="..." />
    <FeatureGridCard Icon="shield" Title="Secure" Description="..." />
</FeatureGrid1>
```

After block integration, the same markup is driven by CMS data:

```razor
@* Via BlockPlacementRenderer, the source generator creates the adapter: *@
@{
    var adapter = CmsBlockRenderRegistry.Adapters["hyper.feature-grid.1"];
    @adapter.Render(block, context)
}
```

### 8.6 Pipeline Diagram

```
HyperUI RCL package  ──owns──▶  CMS Block Model ([BlockMetadata])
     │                                  │
     │                            Renderer component + .cs marker ([CmsBlockRenderer])
     │                                  │
     │                          BlockRendererGenerator (source generator)
     │                                  │
     │         ┌────────────────────────┼────────────────────────┐
     │         ▼                        ▼                        ▼
     │  BlockBase           Block Factory          CmsBlockRegistry
     │  .Polymorphic.g.cs   .g.cs                   + Adapters
     │  (JSON poly)         (AOT switch)            (render dispatch)
     │                                              + package render registry
     │                                              │
     └────────────────── ◀ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┘
                         Renderer opens HyperUI component
                         with Block data as [Parameter]

     Package IPageEditorBlockProvider ──▶ PageEditor palette/defaults/preview/modal/save mapping
```

### 8.7 Registration Checklist (per block type)

- [ ] `BlockBase` subclass with `[BlockMetadata("hyper.{slug}", "Display Name", ...)]`
- [ ] `override string BlockType => "hyper.{slug}"`
- [ ] `override IHtmlContent Accept(IBlockVisitor visitor)` implemented
- [ ] Block properties match `[Parameter]` surface of HyperUI component
- [ ] Renderer `.razor` component
- [ ] Renderer `.cs` marker with `[CmsBlockRenderer(typeof(BlockType))]`
- [ ] Mapper, editor preview, modal editor, and `IPageEditorBlockDefinition` live beside the block
- [ ] `HyperPageEditorBlockProvider` returns the new definition
- [ ] Public/server host calls `services.AddAeroCmsHyperUiBlocks()` once
- [ ] WebAssembly client host calls `services.AddAeroCmsHyperUiBlocks()` once for PageEditor definitions
- [ ] `dotnet build` passes (source generator auto-discovers everything)
- [ ] Registered definition appears in PageEditor palette

---

## 9. Implementation Phases

### Phase 1 — Foundation (pilot: 3 types, 19 variants)

| Component Type | Variants | Rationale |
|---|---|---|
| **FeatureGrids** | 4 | Representative grid component; demonstrates card repetition and parameter extraction |
| **Headers** | 4 | Navigation pattern; demonstrates link lists and responsive layout |
| **Footers** | 12 (largest) | Most complex component type; stress-tests RCL structure and parameter surface |

**Deliverables:**
- RCL project created, added to solution
- `_Imports.razor` with proper usings
- 19 `.razor` + `.razor.cs` component files
- Build verification (0 errors)
- RTL compliance verified on all 19 components

### Phase 2 — Simple Presentation Components (10 types, ~52 variants)

| Component Type | Variants | Notes |
|---|---|---|
| Announcements | 6 | Banner-style info bars; fixed/floating positioning variants |
| Banners | 3 | Hero banners; image/text layouts |
| BlogCards | 7 | Card variations; image, gradient, overlay patterns |
| Cards | 9 | Content cards; podcast, forum post layouts |
| Ctas | 4 | Call-to-action sections; newsletter, image grid |
| EmptyContent | 5 | Empty state displays; search, stock, coming soon |
| LogoClouds | 4 | Partner/logo grids |
| ProductCards | 8 | E-commerce product cards |
| Sections | 4 | Content+image split sections |
| TeamSections | 3 | Team member profiles |

**Deliverables:**
- 52 component files
- All light variants with dark `dark:` class merging
- RTL logical properties applied
- Build verification (0 errors)

### Phase 3 — Interactive Components (4 types, ~13 variants)

| Component Type | Variants | Notes |
|---|---|---|
| ContactForms | 5 | Form components with `EditForm` + FluentValidation |
| Carts | 3 | Shopping cart UI (popup, page layouts) |
| NewsletterSignup | 2 | Email signup forms |
| Polls | 3 | Poll/survey/rating components |

**Deliverables:**
- 13 component files
- `EditForm` + `DataAnnotationsValidator` integration
- `[SupplyParameterFromForm]` for static SSR compatibility
- FluentValidation validators where needed
- RTL compliance

### Phase 4 — Remainder (5 types, ~38 variants)

| Component Type | Variants | Notes |
|---|---|---|
| Buttons | 12 | Button styles (solid, bordered, gradient, icon, hover effects) |
| Pricing | 2 | Pricing tier tables |
| ProductCollections | 4 | Product collection grids with filtering |
| Faqs | 3 | FAQ accordion lists |
| Stats (marketing) | — | Consumed from application stats |

**Deliverables:**
- ~38 component files
- RTL compliance
- Complete component library

### Summary

| Phase | Types | Variants | Files (~) |
|---|---|---|---|
| 1 — Foundation | 3 | 19 | 38 |
| 2 — Simple | 10 | 52 | 104 |
| 3 — Interactive | 4 | 13 | 26 |
| 4 — Remainder | 4+ | 38 | 76 |
| **Total** | **21** | **122** | **~244** |

---

## 10. RTL Conversion Reference Table

### 10.1 Complete CSS Class Migration Map

All HyperUI component HTML must be audited for these replacements:

| Physical Class | Logical Class | Tailwind v4 Equivalent |
|---|---|---|
| `mr-0`, `mr-1`, ..., `mr-96` | `me-0`, `me-1`, ..., `me-96` | `me-*` (margin-inline-end) |
| `ml-0`, `ml-1`, ..., `ml-96` | `ms-0`, `ms-1`, ..., `ms-96` | `ms-*` (margin-inline-start) |
| `pr-0`, `pr-1`, ..., `pr-96` | `pe-0`, `pe-1`, ..., `pe-96` | `pe-*` (padding-inline-end) |
| `pl-0`, `pl-1`, ..., `pl-96` | `ps-0`, `ps-1`, ..., `ps-96` | `ps-*` (padding-inline-start) |
| `space-x-0` through `space-x-96` | `gap-0` through `gap-96` | `gap-*` |
| `-space-x-0` through `-space-x-96` | `-gap-0` through `-gap-96` | `-gap-*` |
| `left-0`, `left-1/2`, `left-full`, etc. | `inset-inline-start-0`, `-1/2`, `-full`, etc. | `inset-inline-start-*` |
| `right-0`, `right-1/2`, `right-full`, etc. | `inset-inline-end-0`, `-1/2`, `-full`, etc. | `inset-inline-end-*` |
| `text-left` | `text-start` | `text-start` |
| `text-right` | `text-end` | `text-end` |
| `rounded-l`, `rounded-l-sm`, etc. | `rounded-s`, `rounded-s-sm`, etc. | `rounded-s-*` |
| `rounded-r`, `rounded-r-sm`, etc. | `rounded-e`, `rounded-e-sm`, etc. | `rounded-e-*` |
| `border-l`, `border-l-2`, etc. | `border-s`, `border-s-2`, etc. | `border-s-*` |
| `border-r`, `border-r-2`, etc. | `border-e`, `border-e-2`, etc. | `border-e-*` |
| `float-left` | `float-start` | `float-start` |
| `float-right` | `float-end` | `float-end` |
| `clear-left` | `clear-start` | `clear-start` |
| `clear-right` | `clear-end` | `clear-end` |

### 10.2 Classes That Need NO Change (Already RTL-Aware)

These Tailwind classes are direction-agnostic and work correctly in RTL without modification:

- `gap-*` — already logical (gap between flex/grid items)
- `grid-cols-*` — grid column order reverses automatically with `dir="rtl"`
- `flex`, `flex-row` — flex items reverse automatically with `dir="rtl"`
- `justify-start`, `justify-end`, `justify-center`, `justify-between` — RTL-aware
- `items-start`, `items-end`, `items-center` — RTL-aware
- `self-start`, `self-end` — RTL-aware
- `p-*`, `px-*`, `py-*` — symmetric (no direction)
- `m-*`, `mx-*`, `my-*` — symmetric
- `text-center`, `text-justify` — symmetric
- `rounded`, `rounded-full`, `rounded-t-*`, `rounded-b-*` — symmetric (top/bottom)
- `border`, `border-t-*`, `border-b-*` — symmetric (top/bottom)
- `w-*`, `h-*`, `max-w-*` — size only
- `bg-*`, `text-*`, `shadow-*` — color/visual only

### 10.3 Audit: HyperUI Physical Classes Found

Based on sample analysis, these physical classes appear in HyperUI marketing components and need RTL replacement:

| Component Type | Physical Classes Found |
|---|---|
| Feature Grids | `space-x-*` (none found, but check all 4 variants) |
| Headers | `flex-1`, `justify-end`, `justify-between` (already RTL-aware) |
| Footers | `lg:justify-end` (already RTL-aware), `justify-start` (already RTL-aware) |
| Ctas | `sm:grid-cols-2`, `ltr:sm:text-left rtl:sm:text-right` (already RTL-aware with explicit `ltr:`/`rtl:` variants) |
| Contact Forms | `ml-*`/`mr-*` possible in form field spacing |

---

## 11. Quick Reference: Component-to-File Mapping

### Marketing Components (21 types)

| # | Type | Slug | Variants | File Pattern |
|---|---|---|---|---|
| 1 | Announcements | announcements | 6 | `Announcement1.razor` – `Announcement6.razor` |
| 2 | Banners | banners | 3 | `Banner1.razor` – `Banner3.razor` |
| 3 | Blog Cards | blog-cards | 7 | `BlogCard1.razor` – `BlogCard7.razor` |
| 4 | Buttons | buttons | 12 | `Button1.razor` – `Button12.razor` |
| 5 | Cards | cards | 9 | `Card1.razor` – `Card9.razor` |
| 6 | Carts | carts | 3 | `Cart1.razor` – `Cart3.razor` |
| 7 | Contact Forms | contact-forms | 5 | `ContactForm1.razor` – `ContactForm5.razor` |
| 8 | CTAs | ctas | 4 | `Cta1.razor` – `Cta4.razor` |
| 9 | Empty Content | empty-content | 5 | `EmptyContent1.razor` – `EmptyContent5.razor` |
| 10 | FAQs | faqs | 3 | `Faq1.razor` – `Faq3.razor` |
| 11 | Feature Grids | feature-grids | 4 | `FeatureGrid1.razor` – `FeatureGrid4.razor` |
| 12 | Footers | footers | 12 | `Footer1.razor` – `Footer12.razor` |
| 13 | Headers | headers | 4 | `Header1.razor` – `Header4.razor` |
| 14 | Logo Clouds | logo-clouds | 4 | `LogoCloud1.razor` – `LogoCloud4.razor` |
| 15 | Newsletter Signup | newsletter-signup | 2 | `NewsletterSignup1.razor` – `NewsletterSignup2.razor` |
| 16 | Polls | polls | 3 | `Poll1.razor` – `Poll3.razor` |
| 17 | Pricing | pricing | 2 | `Pricing1.razor` – `Pricing2.razor` |
| 18 | Product Cards | product-cards | 8 | `ProductCard1.razor` – `ProductCard8.razor` |
| 19 | Product Collections | product-collections | 4 | `ProductCollection1.razor` – `ProductCollection4.razor` |
| 20 | Sections | sections | 4 | `Section1.razor` – `Section4.razor` |
| 21 | Team Sections | team-sections | 3 | `TeamSection1.razor` – `TeamSection3.razor` |

### Source HTML Files

All source HTML is at:
```
hyperui/public/examples/marketing/{slug}/N.html        (light)
hyperui/public/examples/marketing/{slug}/N-dark.html   (dark, where applicable)
```

---

## 12. Class Migration & Verification

### 12.1 Tailwind v3 → v4 Class Audit

Before building, audit all `.razor` files for stale v3 class names:

```powershell
# Search for v3 class names that must be migrated to v4
rg '\bshadow-sm\b|\bshadow\b(?!-|\w)|\brounded-sm\b|\brounded\b(?!-|\w)|\boutline-none\b|bg-gradient-' `
    src/Aero.Cms.Ui.Hyper/Components/
```

Expected replacements:
- `shadow-sm` → `shadow-xs`
- `shadow` (alone) → `shadow-sm`
- `rounded-sm` → `rounded-xs`
- `rounded` (alone) → `rounded-sm`
- `outline-none` → `outline-hidden`
- `bg-gradient-to-*` → `bg-linear-to-*`

### 12.2 RTL Verification

After building each component, verify RTL compliance:

```powershell
# Search for physical classes that break RTL
rg '\bml-|mr-|pl-|pr-|space-x-|left-|right-|text-left|text-right|rounded-l|rounded-r|border-l|border-r|float-left|float-right\b' `
    src/Aero.Cms.Ui.Hyper/Components/
```

Zero hits expected.

### 12.3 Build Command

```powershell
dotnet build src/Aero.Cms.Ui.Hyper/Aero.Cms.Ui.Hyper.csproj
```

### 12.4 Exit Criteria per Phase

- [ ] Project builds with 0 errors
- [ ] No v3 Tailwind class names remain (rg check passes)
- [ ] No physical CSS classes in component markup (rg check passes)
- [ ] Dark variants merged (no separate `-dark.razor` files)
- [ ] Code-behind files exist for components with parameters
- [ ] Source comments reference original HyperUI file paths
- [ ] All SVG icons preserved as inline markup
- [ ] Forms use `EditForm` + `[SupplyParameterFromForm]` pattern (not `@onclick`)
- [ ] For block integration: `[BlockMetadata]` on the block model, `[CmsBlockRenderer(...)]` on a `.cs` renderer marker, and package provider registration added

---

## 13. References

- **HyperUI source:** `hyperui/public/examples/marketing/` (HTML examples)
- **HyperUI catalog:** `hyperui/src/content/collection/marketing/` (MDX catalog definitions)
- **HyperUI CSS:** `hyperui/src/styles/component.css` (Tailwind v4 source) / `hyperui/public/component.css` (compiled output)
- **Page document refactor:** `docs/aero-page-document-refactor.md`
- **Page refactor plans:** `docs/aero-page-refactor-plans.md`
- **NeoUI RTL analysis:** `NeoUI/src/NeoUI.Blazor/` (see Section 5 above)
- **MS Learn — Blazor RCL static SSR:** https://learn.microsoft.com/aspnet/core/blazor/components/class-libraries-and-static-server-side-rendering
- **MS Learn — RCL static assets:** https://learn.microsoft.com/aspnet/core/blazor/components/class-libraries
- **Tailwind v4 CDN:** https://tailwindcss.com/docs/installation/play-cdn
- **Tailwind v4 dark mode:** https://tailwindcss.com/docs/dark-mode#customizing-the-selector
- **Tailwind v4 logical properties:** https://tailwindcss.com/docs/margin#logical-properties

---

## 14. First Migration — Test Page: Pricing 1

### 14.1 First Component Migrated: `hyper.pricing.1`

**Source:** `hyperui/public/examples/marketing/pricing/1.html`  
**Status:** ✅ Complete (build verified, 0 errors)

**Files created:**
| File | Purpose |
|---|---|
| `src/Aero.Cms.Ui.Hyper/Aero.Cms.Ui.Hyper.csproj` | RCL project (Sdk: Microsoft.NET.Sdk.Razor) |
| `src/Aero.Cms.Ui.Hyper/_Imports.razor` | Global usings |
| `src/Aero.Cms.Ui.Hyper/Blocks/Pricing/Pricing1Block.cs` | Block model with `[BlockMetadata("hyper.pricing.1", "Pricing 1", Category = "Hyper")]` |
| `src/Aero.Cms.Ui.Hyper/Blocks/Pricing/Pricing1BlockMapper.cs` | Editor/public node mapper |
| `src/Aero.Cms.Ui.Hyper/Blocks/Pricing/Pricing1BlockRenderer.razor` | Public static SSR renderer |
| `src/Aero.Cms.Ui.Hyper/Blocks/RendererMarkers.cs` | Package-local source-generator marker with `[CmsBlockRenderer(typeof(Pricing1Block))]` |
| `src/Aero.Cms.Ui.Hyper/Blocks/Pricing/Pricing1BlockEditorPreview.razor` | PageEditor preview |
| `src/Aero.Cms.Ui.Hyper/Blocks/Pricing/Pricing1BlockEditor.razor` | PageEditor modal editor |
| `src/Aero.Cms.Ui.Hyper/Blocks/Pricing/Pricing1EditorBlockDefinition.cs` | Runtime editor definition |
| `src/Aero.Cms.Ui.Hyper/HyperPageEditorBlockProvider.cs` | Package editor definition provider |
| `src/Aero.Cms.Ui.Hyper/HyperUiServiceCollectionExtensions.cs` | Single host registration entry point |

**Files modified:**
| File | Change |
|---|---|
| `Aero.Cms.Abstractions` | Moved `CmsBlockRendererAttribute`, `IPageEditorBlockDefinition`, and `IPageEditorBlockProvider` into stable contracts |
| `NeoEditorCatalogSection.cs` | Added `Hyper` enum value |
| `NeoCatalogSectionMapper.cs` | Added `"HYPER"` case |
| `BlockRendererGenerator.cs` | Added `"Hyper" => "Hyper"` in `MapCatalogSection()` |
| `PageEditor.razor` | Added Hyper sidebar section toggle |
| `PageEditor.razor.cs` | Added `CategoryHyper`, `ToggleCategory("hyper")`, `NeoHyperCatalogItems`, and package-provider registration |
| `Aero.Cms.slnx` | Added `Aero.Cms.Ui.Hyper` project entry |
| `Aero.Cms.Web.csproj` | Added project reference to `Aero.Cms.Ui.Hyper` |
| `Aero.Cms.Web/Program.cs` | Calls `services.AddAeroCmsHyperUiBlocks()` for public rendering |
| `Aero.Cms.Web.Client.csproj` | Added project reference to `Aero.Cms.Ui.Hyper` |
| `Aero.Cms.Web.Client/Program.cs` | Calls `services.AddAeroCmsHyperUiBlocks()` for PageEditor palette/preview/editor definitions |

**Build results:** 0 errors for `Aero.Cms.Abstractions` and `Aero.Cms.Ui.Hyper`. Existing dependency vulnerability warnings remain in upstream projects. The block appears in PageEditor through `IPageEditorBlockProvider`, and public rendering uses the Hyper package's generated renderer registry.

### 14.2 Critical Pattern Notes

**Renderer registration:** New Hyper renderers use a `.cs` marker in the owning package, either beside the block or in a package-local `RendererMarkers.cs`. Do not add per-block Hyper entries to `Aero.Cms.Shared/Blocks/Rendering/RendererMarkers.cs`.

**Sidebar registration:** Adding an entirely new top-level section still requires section plumbing once. Adding another block to an existing section should only require the package's `IPageEditorBlockDefinition` and provider.

### 14.3 Pricing 1 Block API

```csharp
Pricing1Block
├── Title: string
├── Description: string
└── Plans: List<Pricing1Plan>
    ├── Name: string
    ├── Price: string
    ├── Period: string
    ├── Features: List<string>
    ├── CtaText: string
    ├── CtaUrl: string
    └── Highlighted: bool  // indigo card style when true
```

### 14.4 STEP BY STEP: Add a New Hyper Block to PageEditor

Hyper blocks follow the same public rendering contract as the rest of Aero CMS:
the saved object is a `BlockBase` subtype, and public pages render through the
generated renderer pipeline. The PageEditor uses a small runtime registry for
editor-only behavior so a new block does not require eight hardcoded switch
edits.

1. Port the HyperUI markup into a static SSR renderer.
   - Start from `hyperui/public/examples/marketing/{slug}/{n}.html`.
   - Preserve the HTML structure and Tailwind classes as much as possible.
   - Replace framework-only behavior with plain static markup unless the block is explicitly approved for interactivity.
   - Public Hyper blocks in V1 must not use interactive Blazor islands.

2. Create the block model.
   - Location: `src/Aero.Cms.Ui.Hyper/Blocks/{Slice}/{BlockName}.cs`.

```csharp
[BlockMetadata(
    "hyper.pricing.1",
    "Pricing 1",
    Category = "Hyper",
    Icon = "dollar-sign",
    SortOrder = 10,
    SchemaVersion = 1)]
public sealed class Pricing1Block : BlockBase
{
    public override string BlockType => "hyper.pricing.1";
    public string Title { get; set; } = "Pricing Plans";
    public string Description { get; set; } = "Choose the right plan for your team.";
    public List<Pricing1Plan> Plans { get; set; } = [];
}
```

3. Add the renderer component.
   - Location: `src/Aero.Cms.Ui.Hyper/Blocks/{Slice}/{BlockName}Renderer.razor`.
   - Accept `[Parameter] public {BlockName}? Block { get; set; }`.
   - Use plain HTML and Tailwind classes in the public renderer.

3a. Add the renderer marker.
   - Location: `src/Aero.Cms.Ui.Hyper/Blocks/{Slice}/{BlockName}RendererMarker.cs` or a package-local `RendererMarkers.cs`.
   - Add `[CmsBlockRenderer(typeof({BlockName}))]` to a partial class declaration matching the renderer component name.
   - This is required because the source generator discovers C# symbols; Razor `@attribute` is not sufficient for package-local renderer discovery.

4. Add a mapper when the editor preview needs `NeoPageNode`.
   - Location: `src/Aero.Cms.Ui.Hyper/Blocks/{Slice}/{BlockName}Mapper.cs`.
   - Include `ToNode({BlockName} block)` and `FromNode(NeoPageNode node)`.
   - Keep defaults in the block model and reuse them from the mapper.

5. Add the PageEditor preview component.
   - Location: `src/Aero.Cms.Ui.Hyper/Blocks/{Slice}/{BlockName}EditorPreview.razor`.
   - Registered preview components should accept `NeoPageNode Node`.
   - The preview can reuse the public renderer if the renderer is static SSR-safe.

6. Register the runtime editor definition.
   - Location: `src/Aero.Cms.Ui.Hyper/Blocks/{Slice}/{BlockName}EditorBlockDefinition.cs`.
   - Add one `IPageEditorBlockDefinition` implementation for the catalog id.
   - The definition must provide:
     - display name, description, category, icon, kind, and sort order,
     - default `EditorBlock`,
     - `EditorBlock -> BlockBase`,
     - `EditorBlock -> NeoPageNode`,
     - preview component type,
     - optional modal editor component type.

```csharp
private sealed class Pricing1EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.pricing.1";
    public string DisplayName => "Pricing 1";
    public string? Description => "Three-column pricing table with highlighted plan support.";
    public string Category => "Hyper";
    public string Kind => "Block";
    public string IconName => "credit-card";
    public int SortOrder => 10;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => typeof(Pricing1BlockEditorPreview);
    public Type? PropertyEditorComponentType => typeof(Pricing1BlockEditor);

    public EditorBlock CreateDefaultEditorBlock() => new()
    {
        Type = CatalogId,
        MainText = "Pricing Plans",
        Description = "Choose the right plan for your team.",
        PricingPlans = Pricing1Block.DefaultPlans.Select(ToEditorPlan).ToList()
    };

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock) =>
        Pricing1BlockMapper.ToNode((Pricing1Block)ToBlockBase(editorBlock)!);

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => new Pricing1Block
    {
        Title = editorBlock.MainText,
        Description = editorBlock.Description,
        Plans = editorBlock.PricingPlans.Select(ToPricingPlan).ToList()
    };
}
```

7. Add modal editing fields.
   - For collection-heavy blocks like pricing tables, expose block-specific fields in the modal editor, not a generic JSON or property bag.
   - Location: `src/Aero.Cms.Ui.Hyper/Blocks/{Slice}/{BlockName}Editor.razor`.
   - Provide the component through `PropertyEditorComponentType`.

8. Register the package provider.
   - Add the definition to `HyperPageEditorBlockProvider`.
   - Ensure `HyperUiServiceCollectionExtensions.AddAeroCmsHyperUiBlocks()` registers the editor provider, block model provider, and generated renderer registry.
   - The public/server web host should call `services.AddAeroCmsHyperUiBlocks()` once.
   - The WebAssembly client should call `services.AddAeroCmsHyperUiBlocks()` once so the PageEditor can create, preview, and edit the block.

9. Confirm the catalog and menu.
   - `IPageEditorBlockDefinition.Category = "Hyper"` maps into the `Hyper` PageEditor section.
   - If the section already exists, do not add more sidebar plumbing.
   - If a new section is introduced, update `NeoEditorCatalogSection`, `NeoCatalogSectionMapper`, the source generator section mapper, and the PageEditor sidebar once.

10. Verify the end-to-end path.
   - Drag the block from PageEditor.
   - Confirm the preview renders immediately.
   - Double-click the block and confirm modal editing works.
   - Save or publish and confirm `EditorBlockMapper` maps through `PageEditorBlockRegistry`.
   - Confirm the public `.cshtml` page renderer displays the block via generated renderer wiring.

11. Build.

```powershell
dotnet build src\Aero.Cms.Abstractions\Aero.Cms.Abstractions.csproj /p:UseSharedCompilation=false --verbosity minimal
dotnet build src\Aero.Cms.Ui.Hyper\Aero.Cms.Ui.Hyper.csproj /p:UseSharedCompilation=false --verbosity minimal
dotnet build src\Aero.Cms.Web.Client\Aero.Cms.Web.Client.csproj /p:UseSharedCompilation=false --verbosity minimal
dotnet build src\Aero.Cms.Web\Aero.Cms.Web.csproj /p:UseSharedCompilation=false --verbosity minimal
dotnet build src\Aero.Cms.Modules.Pages\Aero.Cms.Modules.Pages.csproj /p:UseSharedCompilation=false --verbosity minimal
```

Do not add future Hyper blocks by editing every runtime switch. Source generation
handles compile-time rendering registration; the package `IPageEditorBlockProvider`
handles runtime editor defaults, palette metadata, preview, modal editor, and save
mapping; and `ICmsBlockModelProvider` gives Marten the package-owned block
subtypes.
