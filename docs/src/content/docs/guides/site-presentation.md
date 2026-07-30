---
title: Themes, media, navigation
description: Site presentation, committed theme assets, media limitations, navigation, headers, and footers.
---

Site presentation is composed from a versioned theme selection, a per-site style profile, page content, navigation/header definitions, footer definitions, and media references.

## Themes and CSS

A site selects a theme identity/version and stores style-profile tokens. Public cache variation includes the selected theme. Trusted authors can edit Tailwind or SCSS inputs, but compilers never run in a public request.

Regenerate committed Tailwind output from the repository root:

```powershell
pwsh ./eng/theme-assets/build-theme-assets.ps1
```

Expected result: browser-ready committed CSS is refreshed. The script downloads the official Tailwind standalone CLI `v4.3.3` for the host platform, verifies its SHA-256 value, and stores the ignored tool under `.tools`.

For a Web build that owns regeneration:

```powershell
dotnet build ./src/Aero.Cms.Web/Aero.Cms.Web.csproj -p:AeroBuildThemeAssets=true
```

SCSS for the deployed Aero Safe theme is compiled by build/publish targets. A normal Web build fails if committed assets are older than relevant authoring/UI sources.

## Navigation, headers, and footers

Manager screens under `/manager/navigations` and `/manager/footers` create culture variants, drafts, and published/default records. Admin endpoints use `site:*` policies. Public rendering resolves the current site's published/default variant.

Navigation supports structured rows/columns/blocks and links. Keep destinations normalized and validate external URLs. Moving or translating navigation content does not move pages or create page aliases.

## Media

The Media manager and `/api/v1/admin/media` endpoints browse metadata, create folders, upload, update, and delete. Persistence delegates to an Orleans media actor while file I/O occurs in the endpoint.

Current limitations are production blockers:

- the general Base64 upload path combines a request file name with the media directory without complete containment validation;
- site-ownership checks are marked as a later hardening phase;
- file writes and metadata persistence are not transactional;
- MIME validation for HTML-editor uploads trusts declarations/extensions rather than inspecting file signatures;
- the HTML-editor upload disables antiforgery and therefore depends on surrounding authentication/origin controls.

Use the HTML-editor image path only with authenticated trusted authors, enforce reverse-proxy request limits, add content-signature scanning and durable object storage, and complete site-ownership authorization before production.

## Sample imagery

Product samples that need remote imagery should use the existing `PexelsService` boundary rather than hard-coded third-party image URLs. Store attribution and licensing metadata required by the provider.
