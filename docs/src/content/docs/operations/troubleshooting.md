---
title: Troubleshooting
description: Evidence-led fixes for setup, build, theme, database, cache, rendering, authorization, and documentation failures.
---

Start from the first exact error, the active bootstrap state, selected site/culture, and current commit. Avoid deleting state until the failure path is understood.

## The application stays on setup

Check `/setup/status`, bootstrap state, local infrastructure readiness, and the setup handoff log. `Configured` means a protected pending request still needs runtime initialization; `Running` means normal startup is allowed. Do not set `SetupComplete` by hand.

If setup failed after durable seeding but before appsettings completion, the idempotent setup state may repair the handoff on the next attempt. Preserve logs and database state before retrying.

## Database or cache startup fails

Confirm `DatabaseMode`/`CacheMode`, the resolved secret reference, and effective connection string provider. Server cache mode requires `ConnectionStrings:cache`. Local mode expects the local infrastructure endpoint.

Do not switch to obsolete Marten configuration. Current domains use Sable/SurrealDB.

## Web build says theme assets are stale

Run:

```powershell
pwsh ./eng/theme-assets/build-theme-assets.ps1
dotnet build src/Aero.Cms.Web/Aero.Cms.Web.csproj --no-restore
```

The build intentionally rejects committed CSS older than authoring/UI inputs.

## A manager API returns 401 or 403

- 401: the expected manager scheme did not authenticate.
- 403: check the selected `AeroCms.SiteId`, Snowflake user ID claim, site assignment, and required `site:*` policy.

Changing the host header does not select or authorize a manager site.

## Public content is missing

Verify host-to-site resolution, culture, publication state, slug/path, and content type alias. The public query API never returns drafts. Check `wasTruncated`, maximum depth/items, and field projection before assuming data loss.

## Renderer fails

- Scriban: inspect validation errors, strict-variable names, declared content bindings, template/output bounds, and timeout.
- HTMX: inspect the browser request, `HX-Request`, accepted representation, response status, and sanitized markup.
- SharpTS: check renderer ID/source version/hash, forbidden imports/decorators, type-check diagnostics, `render(context)` return value, and output cap.
- Aero composition: check element/fragment validation and referenced content query definitions.

## Preview differs from published output

Confirm the preview uses the exact current draft/source version and selected culture. Publishing creates a separate public snapshot and may be blocked by concurrency or route-impact validation. Clear only the feature cache after verifying publication succeeded.

## Build files are locked

Stop the running host and then run:

```powershell
dotnet build-server shutdown
```

Retry the focused project with serialized build settings if concurrent builds contend for `obj` outputs.

## Documentation build fails

From `docs/`:

```powershell
pnpm install --frozen-lockfile
pnpm run check
pnpm run build
```

From `docfx/`:

```powershell
docfx metadata
docfx build
```

The validators reject missing manifest sources, duplicate canonical paths, Git-submodule provenance, and broken internal canonical links.
