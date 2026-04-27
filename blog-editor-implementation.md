# Blog Post Editor — Implementation Plan

## Overview

Replace the `PostDetailPopup` modal in `Posts.razor` with a full-page post editor at `/manager/post/editor/{id:long}`, modeled after `PageEditor.razor`.

---

## Tab Structure

| Tab | Component | Purpose |
|---|---|---|
| **Editor** | `StandaloneCodeEditor` (BlazorMonaco) | Raw markdown editing with syntax highlighting |
| **Preview** | `<RadzenMarkdown Text="@Content" />` | Rendered markdown display, with device toolbar (desktop/tablet/mobile) |
| **Metadata** | Radzen form controls | Slug, Category dropdown, Tags multi-select, Excerpt, Featured Image URL |

A **full preview toggle** in the header (borrowed from PageEditor) overlays a device-width container for responsive preview.

---

## Files to Create

| File | Purpose |
|---|---|
| `src/Aero.Cms.Shared/Pages/Manager/PostEditor/PostEditor.razor` | Tabbed editor page |
| `src/Aero.Cms.Shared/Pages/Manager/PostEditor/PostEditor.razor.cs` | Code-behind |

---

## Files to Modify

| File | Change |
|---|---|
| `src/Directory.Packages.props` | Add `<PackageVersion Include="BlazorMonaco" Version="3.4.0" />` |
| `src/Aero.Cms.Shared/Aero.Cms.Shared.csproj` | Add `<PackageReference Include="BlazorMonaco" />` and `<PackageReference Include="Markdig" />` |
| `src/Aero.Cms.Shared/Pages/Manager/Posts.razor` | `OnRowClick` → direct navigation to `/manager/post/editor/{Id}`; wire "New Post" button → `/manager/post/editor` |

---

## Page Architecture

```
┌──────────────────────────────────────────────────┐
│ [Post Title input]                       [Publish] [Save] │
├──────────────────────────────────────────────────┤
│ [Editor]  [Preview]  [Metadata]                  │
├──────────────────────────────────────────────────┤
│                                                   │
│  Tab "Editor":  BlazorMonaco StandaloneCodeEditor │
│                 Language = "markdown"             │
│                                                   │
│  Tab "Preview": RadzenMarkdown rendering          │
│                 + device toolbar (desktop/         │
│                   tablet/mobile) from PageEditor   │
│                                                   │
│  Tab "Metadata": Slug, Category (dropdown),        │
│                  Tags (multi-select), Excerpt,      │
│                  Featured Image URL/picker          │
│                                                   │
└──────────────────────────────────────────────────┘
```

---

## Routes

- `@page "/manager/post/editor"` — create new post
- `@page "/manager/post/editor/{id:long}"` — edit existing post
- `@rendermode InteractiveServer` — required by BlazorMonaco

---

## Content Sync Strategy

- Single `string Content` field in code-behind is the source of truth.
- **BlazorMonaco** → writes to `Content` via `OnDidChangeModelContent` + `GetValue()` JS interop.
- **RadzenMarkdown** → reads from `Content` via `Text="@Content"`.
- On tab switch: save Monaco value to `Content` before showing Preview.

---

## Code-Behind (`PostEditor.razor.cs`)

- `[Parameter] public long? Id`
- Inject: `IBlogHttpClient`, `ICategoriesHttpClient`, `ITagsHttpClient`, `NavigationManager`
- `OnInitializedAsync`: load post via `BlogApi.GetByIdAsync(Id)`; load categories + tags for dropdowns
- BlazorMonaco `ConstructionOptions`: `Language = "markdown"`, `AutomaticLayout = true`
- Auto-save timer: 30s (same as PageEditor)
- `SaveAsync()` → `CreateAsync` / `UpdateAsync`
- `PublishAsync()` → save first if new, then `BlogApi.PublishAsync(Id)`
- `UnpublishAsync()` → `BlogApi.UnpublishAsync(Id)`
- Toast notifications (matching `PageEditor.razor.cs` pattern)
- Navigation uses `forceLoad: true` to prevent Blazor enhanced navigation from breaking Monaco

---

## BlazorMonaco Scripts

Add to host HTML layout before `blazor.*.js`:

```html
<script src="_content/BlazorMonaco/jsInterop.js"></script>
<script src="_content/BlazorMonaco/lib/monaco-editor/min/vs/loader.js"></script>
<script src="_content/BlazorMonaco/lib/monaco-editor/min/vs/editor/editor.main.js"></script>
```

---

## Notes

- `RadzenMarkdown` is a read-only renderer (not an editor with `@bind-Value`). It displays markdown as formatted HTML. Parameters: `Text`, `ChildContent`, `AutoLinkHeadingDepth`, `AllowHtml`.
- BlazorMonaco v3.4.0 targets netstandard2.0 + net9.0; last updated Oct 2025. Requires `InteractiveServer` render mode.
- `PostDetailPopup.razor` is no longer invoked from `Posts.razor` but the file can be kept for other uses.
