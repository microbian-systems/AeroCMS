# Application Caching Policy TODO

Status: future hardening work
Scope: public sites, the Aero Manager, previews, APIs, and static assets

## Why this is deferred

During recovery of the Blazor WebAssembly manager, the browser received a
`dotnet.native.*.wasm` response whose bytes did not match the integrity value in
the active Blazor boot manifest. A development-only middleware workaround was
added in `src/Aero.Cms.Web.Bootstrap/AeroCmsExtensions.cs`:

- responses under `/_framework` receive `no-store, no-cache, max-age=0`;
- `GET /manager...` responses receive `Clear-Site-Data: "cache"`.

This prevents a development browser from reusing framework resources from a
different build and made the recovered checkout usable again. It is deliberately
limited to `Development`, but it is not the intended long-term policy:

- fingerprinted Blazor framework assets are designed to be cached;
- clearing the origin cache on every manager document request is broad;
- repeatedly downloading the WebAssembly runtime slows manager startup; and
- cache bypass can hide an incoherent build, publish, or static-asset pipeline.

Do not disable Blazor integrity verification as a replacement. The eventual
fix must ensure that the boot manifest and every fingerprinted resource are
produced and served from the same build.

## Intended policy

### Versioned static assets

- Cache content-fingerprinted framework, JavaScript, CSS, font, and image assets
  for a long duration with `immutable`.
- A changed asset must receive a new fingerprinted URL.
- Avoid runtime rewriting, recompression, or proxy transformations that change
  bytes without changing the URL and integrity metadata.
- Keep the ASP.NET Core/Blazor static-web-asset pipeline authoritative for
  `/_framework`.

### Public pages

- Output-cache only published, anonymous responses.
- Vary every cached representation by site/host, culture, normalized route,
  renderer, publication version, and each declared query input.
- Treat personalized, authenticated, cart, checkout, account, preview, and
  authorization-sensitive responses as private or `no-store`.
- Invalidate affected output-cache entries when pages, posts, docs, content,
  navigation, aliases, themes, renderer source, or dependent assets change.
- Keep application/document caches separate from HTTP output caches. Sable
  remains the source of truth and cache entries must be safely rebuildable.

### Aero Manager

- Do not place authenticated manager HTML, API responses, tokens, drafts, or
  user-specific state in a shared cache.
- Prefer `private, no-store` for sensitive mutation and identity responses.
- Allow fingerprinted manager static assets to use normal long-lived immutable
  caching.
- Revalidate or avoid long-lived caching for the manager HTML shell and boot
  metadata that select the current fingerprinted asset set.

### Preview and authoring

- Draft and preview documents and fragment responses remain `no-store`.
- Preview caching must never leak content across site, tenant, culture, user, or
  authorization boundaries.
- Renderer artifact caching is separate from HTTP output caching and must use
  versioned source and capability-profile keys.

## Follow-up work

1. Reproduce and document the original manifest/resource mismatch from a clean
   build without the development workaround.
2. Verify Debug, `dotnet watch`, Visual Studio, Release, and publish outputs use
   isolated and coherent `bin`/`obj` and static-web-asset manifests.
3. Test raw and content-encoded responses to confirm that the served bytes match
   the active boot manifest integrity hashes.
4. Remove the development `Clear-Site-Data` behavior.
5. Remove the development `no-store` override for fingerprinted
   `/_framework` assets.
6. Add integration/browser tests for cache headers, SRI startup, manager
   upgrades, public output-cache variation, invalidation, and private-response
   isolation.
7. Document proxy/CDN requirements so compression and transformations preserve
   Blazor integrity and fingerprint semantics.

## Completion criteria

- A normal clean or incremental build starts `/manager` without manually
  clearing browser data.
- Rebuilding changes fingerprinted URLs when bytes change.
- The manager can use cached framework assets without an SRI failure.
- Public cache variation and invalidation tests cover all declared dimensions.
- Authenticated, private, draft, and preview data cannot enter shared caches.
- The temporary development middleware is removed.
