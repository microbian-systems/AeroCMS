# Aero.Cms.Modules.Aliases

URL alias management and error page handling module. Maps old paths to new paths with 301 redirects and provides friendly error pages for 404/5xx responses.

## Architecture

```
AliasModule
 ├── AliasDocument              ← Marten persistence (OldPath, NewPath, SiteId)
 ├── AliasRepository/Service    ← CRUD via compiled queries
 ├── IAliasRuleCache            ← ImmutableDictionary<string, AliasRuleEntry> — O(1) hot lookup
 ├── AliasRewriteRule           ← IRule, reads cache only (zero DB per request)
 ├── AliasPipelineStartupFilter ← IStartupFilter — auto‑registers middleware
 ├── AliasRuleCacheWarmupService← BackgroundService — loads cache on startup
 ├── AliasCacheInvalidationHandler ← [WolverineHandler] — invalidates on CUD
 └── Events (Created/Updated/Deleted) ← Wolverine messages
```

## Request Flow

```
Request hits /anything
    └── IStartupFilter (Insert 0 → outermost wrapper)
         ├── UseRewriter → AliasRewriteRule
         │     Cache hit? → 301 redirect to NewPath
         │     Cache miss? → query Marten, apply + log at Debug
         └── UseStatusCodePages  ← unified error handler
              /api/*             → pass through
              else + 404         → redirect /oops?status=404
              else + 5xx         → redirect /oops?status=500
```

The entire middleware pipeline lives in this module via `IStartupFilter`. Program.cs has zero status code or rewrite middleware code — everything is self-contained in the vertical slice.

## Ordering Precedence Note

`IStartupFilter` implementations run in **DI registration order**. The first `IStartupFilter` registered in `IServiceCollection` becomes the outermost wrapper and executes first on every request.

This module uses `services.Insert(0, ...)` to force its `IStartupFilter` to position 0, guaranteeing the alias rewrite and status code handler wrap the **entire** request pipeline — including any `IStartupFilter` registered by other modules or libraries.

Additionally, only **one** `UseStatusCodePages` / `UseStatusCodePagesWithReExecute` can be active at a time — the last one registered wins. This module owns the status code handler, so no other module or Program.cs should register a competing one. All error page behavior (API pass‑through, 404/5xx redirect) is handled here.

## Cache Lifecycle

```
App starts → BackgroundService warms cache from Marten

Alias created/updated/deleted
    → AliasService publishes event via IMessageBus
    → AliasCacheInvalidationHandler ([WolverineHandler]) calls cache.RefreshAsync()
    → Cache reloads all aliases from Marten into ImmutableDictionary
    → Next request reads new aliases — no restart required
```

## Key Files

| File | Role |
|------|------|
| `AliassModule.cs` | Module entry, `Order = -9999`, registers all services + IStartupFilter |
| `AliasDocument` | Marten entity: `SiteId`, `OldPath` (unique), `NewPath`, `Notes` |
| `AliasServices.cs` | CRUD with Wolverine event publishing on CUD |
| `AliasRewriteRule.cs` | `IRule` — sync, reads IAliasRuleCache, DB fallback on miss |
| `AliasRuleCache.cs` | `ImmutableDictionary<string, AliasRuleEntry>` — singleton, O(1) lookup |
| `AliasPipelineStartupFilter.cs` | `IStartupFilter` — registers `UseRewriter` + `UseStatusCodePages` |
| `AliasRuleCacheWarmupService.cs` | `BackgroundService` — initial cache load |
| `AliasCacheInvalidationHandler.cs` | `[WolverineHandler]` — cache refresh on CUD events |
| `SlugRewriteHook.cs` | `IPageSaveHook` — stub for slug-change detection (future) |
| `RedirectRule.cs` | Type for in‑memory redirect rules (future) |

## Marten Schema

```csharp
opts.Schema.For<AliasDocument>().DocumentAlias("aero.aliases");
opts.Schema.For<AliasDocument>().Identity(x => x.Id);
opts.Schema.For<AliasDocument>().Index(x => x.SiteId);
opts.Schema.For<AliasDocument>().UniqueIndex(x => x.OldPath);
opts.Schema.For<AliasDocument>().Index(x => x.NewPath);
opts.Schema.For<AliasDocument>().Index(x => x.CreatedOn);
opts.Schema.For<AliasDocument>().Index(x => x.ModifiedOn);
```

## Wolverine Integration

- **Package**: `WolverineFx`
- **Discovery**: Source‑generated (no assembly scanning)
- **Handlers**: `[WolverineHandler]` attribute + `IWolverineHandler` interface
- **Events**: `AliasCreated`, `AliasUpdated`, `AliasDeleted` — publish fire‑and‑forget

## DI Registration

| Service | Lifetime | Notes |
|---------|----------|-------|
| `IAliasRepository` | Scoped | Compiled Marten queries |
| `IAliasServcie` | Scoped | CRUD + event publishing |
| `IAliasRuleCache` | Singleton | ImmutableDictionary — O(1) |
| `AliasRewriteRule` | Singleton | IRule — zero DB per request |
| `AliasRuleCacheWarmupService` | Hosted | BackgroundService |
| `IStartupFilter` | Transient | Insert(0) for execution priority |

## Seeded Data

During setup, the seed service creates:
- `/oops` CMS page (error page content)
- `/404` → `/oops` alias (301 redirect for URL visits)
- `/500` → `/oops` alias (301 redirect for URL visits)

See `SeedDataService.SeedOopsPageAsync()` for details.
