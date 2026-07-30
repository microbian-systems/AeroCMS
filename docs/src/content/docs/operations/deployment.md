---
title: Deployment and operations
description: Build, publish, infrastructure, caching, health, telemetry, backup, and recovery guidance.
---

AeroCMS is pre-beta. There is no verified turnkey production topology at this baseline. Build a deployment from the current host contracts and complete the security checklist rather than treating historical Compose files as production manifests.

## Build and publish

```powershell
dotnet restore src/Aero.Cms.slnx
pwsh ./eng/theme-assets/build-theme-assets.ps1
dotnet build src/Aero.Cms.slnx --no-restore
dotnet publish src/Aero.Cms.Web/Aero.Cms.Web.csproj `
  --configuration Release `
  --no-restore `
  --output artifacts/publish/web
```

Expected result: a Release web publish with current committed theme assets. Publish the separate manager host when the chosen topology uses it.

## Required durable state

- SurrealDB/Sable data for CMS documents, identity, setup state, and domain records.
- Data-protection keys and their protecting certificate/key material.
- Media objects and metadata.
- Redis-compatible cache only when it carries non-authoritative cached state; it must be rebuildable.
- Secret-provider records and provider authority configuration.

Use a stable hostname and HTTPS. Persist data-protection keys across replicas and deploy them with the same application name. A key-ring loss can invalidate cookies and protected settings.

## Database and cache

Setup supports embedded or server database mode and local or server cache mode. Use server modes for horizontally scaled production. The cache endpoint must be Redis-compatible; FusionCache and output cache use separate key namespaces and responsibilities.

The repository's historical Compose file contains unpinned images and development-oriented services/settings. It is evidence of experiments, not an approved production deployment.

## Health

The Health module maps aggregate `/health` outside Development and adds no concrete dependency check by itself. Service defaults can register a self liveness check and development `/health`/`/alive` endpoints when explicitly called. Neither path proves SurrealDB, cache, media, setup completion, or tenant readiness unless those checks are registered.

Restrict health endpoints at the network edge and add explicit readiness checks for every required dependency.

## Telemetry and logging

Service defaults can register ASP.NET Core, HTTP client, and runtime metrics; ASP.NET Core and HTTP-client traces; and OpenTelemetry logging. OTLP export is enabled only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set.

The `Aero.Cms.Modules.OpenTelemetry` assembly is currently a placeholder and adds no telemetry behavior. Serilog settings in the host provide console/file output and can use an OTLP sink in production configuration. Redact authorization headers, cookies, API keys, provider payloads, PII, and content drafts.

## Backup and restore

Use the backup tooling appropriate to the deployed SurrealDB storage engine and media store. A complete recovery point includes database state, media, data-protection keys, certificates, and secret-provider references. Cache data is normally excluded.

Test restoration into an isolated environment:

1. restore database and media;
2. restore the matching data-protection key ring/certificate;
3. supply secret-provider access without copying raw secrets into config;
4. start in a non-public environment;
5. verify setup state, manager login, site resolution, published routes, content queries, and background workflows;
6. rotate credentials if the backup left the trusted boundary.
