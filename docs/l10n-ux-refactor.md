# Localization UX Refactor

Last modified: 2026-06-01

## Objective

The Pages and Posts manager screens should present localized content as translation groups instead of unrelated duplicate rows. Editors should see one logical content item in the hierarchy, expand it to inspect culture-specific variants, and create or overwrite localized variants from the default culture when needed.

Docs should receive the same treatment later. This document tracks that Docs UI localization refactor has not started yet.

## Scope

In scope now:

- Pages manager translation-group tree UX.
- Posts manager translation-group UX.
- Rename `TranslationGroupId` to `TranslationGroupId` across the current localization model and API surface.
- Add source relationship metadata for translation workflows.
- Add Pages-only hidden behavior for marketing/landing pages.
- Preserve the current page hierarchy/tree behavior.
- Keep tests on hold until the final test pass requested by the user.

Deferred:

- Docs manager translation-group UX.
- Machine translation provider integration beyond UI/action contract.
- Translation approval workflows, assignment, dashboards, and reports.
- Data migration. This is not a production app yet.

## Vocabulary

- Translation group: A logical content item shared by localized variants.
- Variant: A culture-specific document inside a translation group.
- Default variant: The variant matching the site's configured default culture.
- Source document: The document a variant was originally created from.

## Data Model

Use `TranslationGroupId` instead of `TranslationGroupId`.

Pages:

```csharp
public long TranslationGroupId { get; set; }
public long? SourcePageId { get; set; }
```

Posts:

```csharp
public long TranslationGroupId { get; set; }
public long? SourcePostId { get; set; }
```

Docs remain deferred but should follow the same naming when their UI refactor starts.

## Pages UX

The Pages manager remains a tree grid. The displayed row unit changes from page document to translation group.

Default culture selected:

- Show one group row for each logical page.
- Use the default-culture variant for title, slug, path, status, hidden state, preview, edit, and actions.
- If the default-culture variant is missing, fall back to the first available variant and visually mark the row as missing the default culture.
- Expanding the group row shows all localized variants with their own status/actions.

Specific culture selected:

- Show the culture-specific variant for each group where it exists.
- If a group is missing the selected culture, show the fallback/default row with a missing-translation warning.
- Expanded rows still show all variants.

Search:

- Search all translations in the group.
- Keep the result grouped and hierarchical.

Actions:

- Group/default row: preview, edit, publish/unpublish, add child, add translation, translate, delete.
- Variant row: preview, edit, publish/unpublish, delete variant.
- Add child should attach the child to the currently displayed/default variant and preserve translation-group hierarchy where matching parent variants exist.

Delete behavior:

- Deleting a non-default variant deletes only that localized document.
- Deleting the default-culture variant deletes the full translation group and all localized variants after an explicit warning.
- Bulk delete from group rows deletes selected groups, not just one variant.

Translate action:

- The group/default row includes a translate icon.
- Clicking it opens a confirmation dialog.
- If site-enabled cultures are missing, the dialog creates variants for the missing cultures.
- If variants already exist, the dialog shows an overwrite checkbox.
- Without overwrite, existing variants are preserved.
- With overwrite, existing localized content can be replaced from the current default-culture source.

Hidden behavior:

- `PageDocument.IsHidden` applies only to Pages.
- Hidden pages are excluded from sitemaps.
- Hidden pages are excluded from future native built-in search.
- Posts and Docs use publish/unpublish only.

## Posts UX

Posts should use the same translation-group principles:

- One logical post row per group.
- Culture coverage column.
- Culture filter dropdown.
- Expanded variant rows.
- Add translation and translate actions.
- Publish/unpublish controls per variant.

Posts do not get `IsHidden`.

## Docs UX

Docs are deferred.

Create a later implementation slice for:

- Translation-group document naming.
- Docs tree or section grouping behavior.
- Docs culture filter.
- Variant rows and translation actions.

## API Shape

Pages should expose a read contract that returns grouped tree rows rather than requiring the UI to load every page and group in memory.

```csharp
public sealed record PageTranslationGroupTreeItem(
    long TranslationGroupId,
    long DisplayPageId,
    long? ParentTranslationGroupId,
    string DisplayCulture,
    string DefaultCulture,
    string Title,
    string Slug,
    string Path,
    string PublicationState,
    bool IsHidden,
    bool MissingDefaultCulture,
    bool MissingSelectedCulture,
    IReadOnlyList<PageTranslationVariantItem> Variants);
```

```csharp
public sealed record PageTranslationVariantItem(
    long Id,
    string Culture,
    string Title,
    string Slug,
    string Path,
    string PublicationState,
    bool IsHidden,
    bool IsDefaultCulture);
```

The first implementation may build this read model in the Pages service. A Marten read projection remains the preferred follow-up once the UX contract is stable.

## Radzen Notes

Radzen `RadzenDataGrid` supports row templates, filtering/sorting/paging, row expansion, and hierarchical child data loading. The Pages implementation should keep using the existing tree grid pattern and evolve it into a translation-group tree rather than switching to a flat grouped table.

## Verification

During implementation:

- Build `src/Aero.Cms.Shared/Aero.Cms.Shared.csproj`.
- Build `src/Aero.Cms.Web/Aero.Cms.Web.csproj`.
- Manually verify Pages manager tree display.
- Tests remain on hold until the final test pass.

