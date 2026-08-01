# DaisyUI design system and PageEditor component catalog

## Status

Approved for incremental implementation. This document is also the task brief
for the first production slice.

The first slice adds DaisyUI to AeroCMS's existing Tailwind asset build, defines
the initial Aero corporate theme, and introduces a scalable component catalog
inside the PageEditor. It does **not** rewrite existing manager or public-page
markup. Existing styles remain valid and unchanged; DaisyUI is the preferred
foundation for new UI and new page-builder components.

## Goals

- Add DaisyUI 5 as a pinned, build-time dependency without npm or a browser CDN.
- Keep the current Tailwind standalone compilation workflow and committed,
  versioned CSS artifacts.
- Avoid collisions with Aero's existing `.btn`, `.card`, and similar classes.
- Make useful Daisy components draggable into Aero pages as ordinary persisted
  `HtmlNode` trees.
- Keep generated pages editable through the normal canvas and inspector.
- Add room for curated HyperUI-inspired patterns without shipping HyperUI CSS.
- Establish site-level themes with identical editor-preview and public output.
- Specify a future internal visual theme generator based on safe design tokens.

## Non-goals for this slice

- Rewriting existing manager pages with Daisy classes.
- Migrating existing PageEditor elements or persisted page content.
- Adding npm, pnpm, Vite, PostCSS, or a runtime CSS compiler.
- Persisting arbitrary theme CSS supplied by ordinary editors.
- Copying HyperUI source or adding its stylesheet/runtime to AeroCMS.
- Shipping behavior-heavy components whose state or scripts have not been
  modeled safely (modal, drawer, dropdown, carousel, toast, theme controller).

## Build and dependency decision

AeroCMS will vendor pinned DaisyUI standalone ESM plugin files and verify their
SHA-256 digests before compiling CSS:

```text
Tailwind standalone binary
  + pinned daisyui.mjs
  + pinned daisyui-theme.mjs
  + Aero Tailwind source and source scan
  -> committed aero.generated.css
```

The plugin files are build inputs only. Browsers receive only the compiled,
fingerprinted/versioned CSS already served by AeroCMS. The existing
`eng/theme-assets/build-theme-assets.ps1` remains the single build entrypoint.

The integration uses a `d-` DaisyUI class prefix. Aero already defines generic
classes such as `.btn` and `.btn-primary`; the prefix allows DaisyUI to be added
without changing the behavior of current pages.

The first implementation pins DaisyUI **5.7.9** under
`src/Aero.Cms.Web/Styles/vendor/daisyui/5.7.9`. The Tailwind configuration is
intentionally additive:

```css
@plugin "./vendor/daisyui/5.7.9/daisyui.mjs" {
  themes: false;
  prefix: "d-";
  logs: false;
}

@plugin "./vendor/daisyui/5.7.9/daisyui-theme.mjs" {
  /* Aero theme definition */
}
```

Both plugin files are verified against their pinned SHA-256 digests before the
build proceeds. The path and version are repository-local and deterministic.

## Initial Aero corporate theme

The first theme uses DaisyUI's Tailwind plugin syntax and the approved
corporate values:

```css
@plugin "daisyui/theme" {
  name: "corporate";
  default: false;
  prefersdark: false;
  color-scheme: "light";
  --color-base-100: oklch(100% 0 0);
  --color-base-200: oklch(93% 0 0);
  --color-base-300: oklch(86% 0 0);
  --color-base-content: oklch(22.389% 0.031 278.072);
  --color-primary: oklch(58% 0.158 241.966);
  --color-primary-content: oklch(100% 0 0);
  --color-secondary: oklch(55% 0.046 257.417);
  --color-secondary-content: oklch(100% 0 0);
  --color-accent: oklch(60% 0.118 184.704);
  --color-accent-content: oklch(100% 0 0);
  --color-neutral: oklch(0% 0 0);
  --color-neutral-content: oklch(100% 0 0);
  --color-info: oklch(60% 0.126 221.723);
  --color-info-content: oklch(100% 0 0);
  --color-success: oklch(62% 0.194 149.214);
  --color-success-content: oklch(100% 0 0);
  --color-warning: oklch(85% 0.199 91.936);
  --color-warning-content: oklch(0% 0 0);
  --color-error: oklch(70% 0.191 22.216);
  --color-error-content: oklch(0% 0 0);
  --radius-selector: 0.25rem;
  --radius-field: 0.25rem;
  --radius-box: 0.25rem;
  --size-selector: 0.25rem;
  --size-field: 0.25rem;
  --border: 1px;
  --depth: 0;
  --noise: 0;
}
```

In the vendored build the plugin reference resolves to the local
`daisyui-theme.mjs` file. The theme name is applied through `data-theme` on a
site rendering boundary, never by mutating every component.

## Theme ownership and rendering

Themes are site-level presentation profiles. The selected site theme must be
resolved once and applied consistently to:

- public page rendering;
- authenticated draft preview;
- PageEditor canvas preview; and
- HTMX-rendered fragments returned for the same site.

Page content stores semantic component classes, not copied theme colors. A
button persists classes such as `d-btn d-btn-primary`; the active theme supplies
the corresponding color and shape tokens.

Existing theme stylesheets and framework-neutral `HtmlStyle` intent continue
to work. DaisyUI is an additional component vocabulary, not a replacement for
the current style compiler.

## PageEditor information architecture

The current top-level sidebar navigation remains:

```text
Document | Elements | Content | Inspector
```

Components belong inside **Elements**. A fifth top-level tab would separate
components from the place where editors already expect to add elements.

The Elements panel gains a compact catalog filter:

```text
All | Basics | Daisy | Patterns | HTML
```

- **Basics**: current Aero layouts and rendered blocks.
- **Daisy**: reusable single components represented by ordinary HTML nodes.
- **Patterns**: larger, curated compositions made from Daisy plus Tailwind.
- **HTML**: standards-oriented raw HTML element palette.

Search spans every category. Component cards show a concise icon, human name,
and optional category badge; both click-to-insert and drag-to-canvas remain
supported.

## Component persistence contract

Daisy components are template factories, not new persisted polymorphic types.
Dragging a component creates a normal, editable `HtmlNode` subtree:

```text
catalog descriptor
  -> template factory
  -> validated HtmlNode subtree
  -> existing PageEditor command/history pipeline
  -> existing HTML renderer
```

This preserves the current content model and avoids coupling saved pages to a
specific C# component class. Stable catalog keys are used for editor commands
and telemetry, while persisted nodes remain ordinary HTML.

Catalog descriptors replace an ever-growing enum/switch pair. A descriptor
contains at least:

- stable key;
- display name and description;
- catalog group and search keywords;
- icon identifier;
- template factory;
- trust/feature flags where needed; and
- optional preview metadata.

Factories emit complete, statically known Tailwind/Daisy class tokens. They do
not construct Tailwind class names dynamically because build-time source
scanning cannot discover arbitrary runtime strings.

## Initial draggable Daisy component set

The first slice favors useful, script-free components:

- Button
- Badge
- Alert
- Card
- Hero
- Stat
- Progress
- Skeleton
- Divider
- Breadcrumbs
- Steps
- Timeline
- Table
- Pagination
- Accordion using native `details`/`summary`

All templates must pass the existing HTML content-model and attribute policy.
Components with executable behavior, global overlays, or cross-node state are
deferred until their editor and security contracts are explicit.

## HyperUI-inspired patterns

The `hyperui/` submodule is a design/reference source only. AeroCMS will not
load HyperUI CSS and will not depend on HyperUI at runtime.

Selected layouts are recreated as Aero-owned compositions using prefixed
Daisy classes, ordinary Tailwind utilities, semantic HTML, and Aero media or
content bindings. Initial pattern candidates are:

- marketing hero with primary and secondary actions;
- feature-card grid;
- pricing comparison section;
- testimonial/social-proof section;
- product card;
- call-to-action banner; and
- application empty state.

Each adopted pattern receives an Aero name, stable catalog key, attribution
review, responsive verification, and accessibility review. The source
submodule is never included in public CSS or documentation ingestion.

## Future internal visual theme generator

A later phase will add a manager experience inspired by DaisyUI's theme
generator. It will be an Aero-owned editor, not an embedded third-party page.

The generator will expose:

- base, primary, secondary, accent, neutral, info, success, warning, and error
  color pairs;
- selector, field, and box radii;
- selector and field sizing;
- border width, depth, and noise;
- light/dark color-scheme metadata;
- live previews across representative components and content patterns;
- accessible contrast feedback; and
- clone, rename, preview, publish, and revert workflows.

The database stores a structured, validated token document and its published
version. It does not store unrestricted plugin CSS as the authoritative model.
Publishing validates tokens, generates deterministic Daisy theme CSS, compiles
or publishes a versioned site asset, and invalidates the relevant theme/output
caches. Draft preview can render unpublished tokens in an isolated preview
boundary.

```text
Theme token draft
  -> validation and contrast checks
  -> deterministic Daisy theme source
  -> build/publish artifact
  -> versioned site theme selection
  -> preview/public cache invalidation
```

## Security and accessibility

- Theme editing requires a dedicated trusted design permission.
- Custom arbitrary CSS remains a separate advanced capability.
- Component templates cannot introduce scripts, inline event handlers, or
  unsafe URLs.
- Interactive controls require keyboard, focus, name, role, and state review.
- Color-token publishing reports WCAG contrast problems before activation.
- Preview and public rendering use the same sanitized HTML and compiled CSS.

## Delivery phases

### Phase 1: foundation and catalog

1. Vendor and verify pinned DaisyUI standalone plugin inputs.
2. Compile the prefixed component CSS and corporate theme through the current
   Tailwind asset script.
3. Add a descriptor-driven PageEditor component catalog and category filter.
4. Ship the initial script-free Daisy component templates.
5. Verify editor insertion, save/reload, preview, publish, and public rendering.

### Phase 2: curated patterns

1. Recreate selected HyperUI-inspired patterns in Aero-owned templates.
2. Add responsive and accessibility regression coverage.
3. Add component thumbnails or lightweight previews after catalog behavior is
   stable.

### Phase 3: site theme management

1. Persist a site-level theme selection and version.
2. Add manager preview and safe publish/revert workflows.
3. Ensure CSS artifact and output-cache invalidation is deterministic.

### Phase 4: visual theme generator

1. Add the token editor and component preview gallery.
2. Add contrast validation and light/dark theme workflows.
3. Add import/export of validated Daisy-compatible theme tokens.

## Acceptance criteria for Phase 1

- The normal AeroCMS build remains npm-free.
- DaisyUI plugin inputs are pinned and integrity checked.
- Existing pages and manager controls retain their current styling.
- Prefixed Daisy classes are present in the generated CSS.
- The corporate theme is available in editor preview and public rendering.
- The Elements panel can search/filter and drag the initial Daisy components.
- Inserted components persist as normal HTML nodes and survive save/reload.
- Preview and published output render the same component structure and theme.
- Focused tests and the standard solution/build checks pass.
