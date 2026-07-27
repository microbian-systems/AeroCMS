---
title: Configuration
description: Current AeroCMS bootstrap, infrastructure, security, identity, AI, cache, and telemetry settings.
---

Configuration comes from ASP.NET Core providers, but setup persists only bootstrap state and protected secret references. Prefer environment variables, a secret manager, or the setup provider boundary for sensitive values.

## Bootstrap

`AeroCms:Bootstrap` controls early startup:

| Key | Current values/meaning |
| --- | --- |
| `State` | `Setup`, `Configured`, or `Running` |
| `SetupComplete`, `SeedComplete`, `HasBootstrapConfig` | bootstrap progress markers |
| `DatabaseMode` | `Embedded` or `Server` |
| `CacheMode` | `Local` or `Server` |
| `SecretProvider` | selected secret-provider label |
| `DatabaseConnectionStringReference` | protected reference, not a raw connection string |
| `RequestedManagerAuthenticationProvider` | manager authority selected during setup |
| `RequestedMemberAuthenticationProvider` | member authority selected during setup |

The durable setup document remains the authoritative completion/idempotency record after initialization. Do not toggle these flags manually to skip seeding.

## Infrastructure

- `ConnectionStrings:aero`: effective Sable/SurrealDB connection string after secret resolution.
- `ConnectionStrings:cache`: Redis-compatible endpoint used by FusionCache and output cache.
- `AeroCms:Database:Username` and `AeroCms:Database:Password`: optional server-mode values, preferably secret-backed.
- `AeroCms:DataProtection:KeyStoragePath`: persistent key-ring directory.
- `AeroCms:DataProtection:ApplicationName`: shared discriminator, default `AeroCMS`.
- `AeroCms:DataProtection:Certificate:Path` and `Password`: certificate protection for the key ring.

Local cache mode defaults to `localhost:33333`; server mode fails startup without a non-blank endpoint.

## Security and identity

`Aero:Security:ApiKeys` binds API-key prefix, length, and URL-safe generation settings. Development-only external-provider secret settings exist under `AeroCms:Authentication`, but production deployments must use the durable provider/secret boundary and keep development providers disabled.

Keep cookie options, redirect paths, and authorization additions in `AeroCmsOptions` when embedding the host. Do not weaken the `site:*` policies globally.

## AI

Provider defaults are read from `Ai:Providers:<Provider>` and a configured default-provider key. Typical non-secret values are provider ID, display name, model, base endpoint, enabled state, and capability flags. API keys belong in the protected AI settings store or a secret provider; the manager read model only indicates whether a key exists.

## Optional modules

- `AeroCms:Modules:MiniProfiler:Enable` defaults to false.
- `AeroCms:Analytics` binds analytics script settings.
- `AeroCms:EnableWebAssemblyDebugging` explicitly enables WebAssembly debugging behavior.
- `OTEL_EXPORTER_OTLP_ENDPOINT` enables the OTLP exporter in service defaults.

## Safe example

```json
{
  "AeroCms": {
    "Bootstrap": {
      "State": "Setup",
      "DatabaseMode": "Embedded",
      "CacheMode": "Local",
      "HasBootstrapConfig": false
    },
    "DataProtection": {
      "KeyStoragePath": "keys",
      "ApplicationName": "AeroCMS",
      "Certificate": {
        "Path": "certs/aero-cms.pfx",
        "Password": null
      }
    }
  },
  "ConnectionStrings": {}
}
```

This is a runnable non-secret setup-mode shape, not a production configuration. Supply certificate passwords and infrastructure credentials outside tracked JSON.
