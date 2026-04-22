# Aero CMS Two-App Startup Implementation Plan

## Executive Summary

This document defines the approved startup architecture for Aero CMS:

1. **Setup App** runs first when bootstrap state is `Setup`
2. **Main App** runs after setup app exits and configuration is re-read
3. **No manual restart required** - the setup app uses `IHostApplicationLifetime.StopApplication()` to hand off to the main app
4. **`Configured` is not traffic-ready** - it means configuration is saved but runtime bootstrap is still pending
5. **`Running` is the only normal-traffic-ready state**

This is the chosen finish line for bootstrap work so the team can move on to higher-value CMS work like the block editor.

---

## Approved Architecture

### Phase 1 - Setup Host

- Build early configuration
- Read `AeroCms:Bootstrap`
- If bootstrap state is `Setup`:
  - create a **minimal setup WebApplication**
  - register only setup-safe services
  - show setup UI
  - on successful submit:
    - persist bootstrap configuration
    - persist pending seed/bootstrap request
    - mark bootstrap as `Configured`
    - call `StopApplication()`

### Phase 2 - Main Host

- Re-read configuration after setup app exits
- Build the **full runtime WebApplication**
- Start the host with `StartAsync()` so hosted services can begin
- Wait for required infrastructure readiness using a **timeout `CancellationTokenSource`**
- Run runtime bootstrap/initialization
- Mark bootstrap as `Running`
- Only then allow normal traffic/readiness to go healthy

---

## Lifecycle Model

### Bootstrap States

- `Setup`
  - Setup UI is shown
  - Normal traffic is blocked
- `Configured`
  - Setup app has completed
  - Main host must still finish runtime bootstrap
  - Normal traffic is still blocked
- `Running`
  - Runtime bootstrap succeeded
  - Normal traffic is allowed
- `Failed`
  - Runtime bootstrap failed or timed out
  - Normal traffic remains blocked until corrected/retried

### Important Rule

`Configured` is **not** equivalent to ready.

That distinction is required to avoid serving requests before:

- embedded services are ready
- seeding completes
- migrations/module runtime initialization completes

---

## Architecture Overview

```text
Program.cs Entry
    ↓
Build early configuration
    ↓
Read AeroCms:Bootstrap
    ↓
IF State = Setup:
    Create Setup WebApplication
        - minimal DI only
        - Data Protection shared with main app
        - setup UI + setup status endpoint
        - await app.StartAsync()
        - await app.WaitForShutdownAsync()
        - await app.StopAsync()
    ↓
    Re-read configuration
    ↓
Create Main WebApplication
    - full runtime DI
    - await app.StartAsync()
    - await WaitForRequiredInfrastructureAsync(cts.Token)
    - await RunRuntimeBootstrapAsync()
    - mark bootstrap Running
    - await app.WaitForShutdownAsync()
```

---

## Key Technical Decisions

### 1. Shared Data Protection (Required)

Both setup and main app must use identical Data Protection configuration:

```csharp
services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keyPath))
    .ProtectKeysWithCertificate(certificate)
    .SetApplicationName("AeroCMS");
```

The setup app may encrypt values that the main app must later decrypt.

### 2. Readiness Model: Snapshot + Barrier

The approved readiness pattern is:

- **snapshot** of current readiness state for UI/status reporting
- **awaitable barrier** for startup coordination

This is preferred over introducing a separate `IEmbeddedServiceHealth` contract.

#### Snapshot responsibilities

- expose current `PostgresReady`
- expose current `GarnetReady`
- support `/setup/status`
- support setup UI polling

#### Barrier responsibilities

- await required services based on selected modes
- use timeout via `CancellationTokenSource`
- fail startup clearly if required infra does not become ready

### 3. Timeout-Based Readiness Wait

The main app should use a real wait barrier with timeout:

```csharp
await app.StartAsync();

using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

await runtimeStartupCoordinator.WaitForInfrastructureAsync(
    databaseMode,
    cacheMode,
    cts.Token);
```

Rules:

- `DatabaseMode = Embedded` → wait for Postgres
- `DatabaseMode = Server` → no embedded Postgres wait
- `CacheMode = Memory` → no cache wait
- `CacheMode = Embedded` → wait for Garnet
- `CacheMode = Server` → no embedded Garnet wait

### 4. Blazor Server Only for Setup UI

Setup interactivity is implemented in **Blazor Server only**.

This means:

- no setup-specific TypeScript work is required
- readiness polling and conditional UI behavior should live in `Setup.razor` / `Setup.razor.cs`
- old plan references to client TS setup behavior are superseded

### 5. Runtime Bootstrap Happens Only in Main Host

Setup app responsibilities end after configuration persistence and handoff.

Runtime-only work must happen after the main host starts:

- migrations
- seeding
- module runtime initialization
- final bootstrap completion (`Running`)

---

## Configuration Schema

Environment-specific appsettings files should contain:

```json
{
  "AeroCms": {
    "DataProtection": {
      "KeyStoragePath": "keys",
      "ApplicationName": "AeroCMS",
      "Certificate": {
        "Path": "certs/aero-cms.pfx",
        "Password": null
      }
    },
    "Bootstrap": {
      "State": "Setup",
      "SetupComplete": false,
      "SeedComplete": false,
      "DatabaseMode": "Embedded",
      "CacheMode": "Memory",
      "SecretProvider": "Local Certificate"
    }
  }
}
```

Environment-specific values:

- Development: relative key ring path
- Production/Docker: mounted volume path like `/app/keys`

---

## Current Status Summary

### Implemented

- Two-app orchestration exists in `src/Aero.Cms.Web/Program.cs`
- Setup app factory exists in `src/Aero.Cms.Web/Setup/SetupWebApplication.cs`
- Bootstrap handoff service exists
- Bootstrap state/provider exists
- Setup UI exists in Blazor
- Setup status endpoint exists
- Readiness snapshot/signal types exist

### Not Yet Complete

- `Configured` is still treated too much like ready in current behavior
- main-host readiness barrier is not yet an awaited timeout-based coordinator
- setup UI readiness polling is still placeholder/stubbed
- setup submit gating is not yet based on real readiness
- full server/Infisical persistence path needs completion
- appsettings schema is not fully normalized

---

## Required File Changes

### Primary Runtime/Bootstrap Files

1. `src/Aero.Cms.Web/Program.cs`
2. `src/Aero.Cms.Web.Core/Eextensions/AeroWebAppExtensions.cs`
3. `src/Aero.Cms.Modules.Setup/SetupStateStore.cs`
4. `src/Aero.Cms.Modules.Setup/Bootstrap/RuntimeBootstrapInitializer.cs`
5. `src/Aero.Cms.Modules.Setup/Bootstrap/BootstrapCompletionWriter.cs`

### Setup Input / Persistence Files

6. `src/Aero.Cms.Modules.Setup/Areas/MyFeature/Pages/Setup.razor.cs`
7. `src/Aero.Cms.Modules.Setup/Areas/MyFeature/Pages/Setup.razor`
8. `src/Aero.Cms.Modules.Setup/Bootstrap/SetupBootstrapHandoffService.cs`
9. `src/Aero.Cms.Modules.Setup/Bootstrap/DatabaseBootstrapService.cs`
10. `src/Aero.Cms.Modules.Setup/Bootstrap/CacheBootstrapService.cs`
11. `src/Aero.Cms.Modules.Setup/Bootstrap/BootstrapPersistenceModels.cs`

### Readiness / Infra Coordination Files

12. `src/Aero.AppServer/AeroEmbeddedDbService.cs`
13. `src/Aero.AppServer/AeroCacheService.cs`
14. `src/Aero.AppServer/Startup/IMultiStartupSignal.cs`
15. `src/Aero.AppServer/Startup/MultiStartupSignal.cs`
16. `src/Aero.AppServer/Startup/IInfrastructureReadinessSnapshot.cs`
17. `src/Aero.AppServer/Startup/InfrastructureReadinessSnapshot.cs`

### Config Files

18. `src/Aero.Cms.Web/appsettings.json`
19. `src/Aero.Cms.Web/appsettings.Development.json`
20. `src/Aero.Cms.Web/appsettings.Staging.json`
21. `src/Aero.Cms.Web/appsettings.Production.json`

### Tests

22. `tests/Aero.Cms.Core.Tests/Integration/SetupPageModelTests.cs`
23. `tests/Aero.Cms.Core.Tests/Integration/BootstrapCompletionWriterTests.cs`
24. new or updated bootstrap/readiness integration tests

---

## Concrete Implementation Sequence

### Step 1 - Finish persistence contract

- carry full setup inputs through handoff:
  - `ConnectionString`
  - `CacheConnectionString`
  - `InfisicalMachineId`
  - `InfisicalClientSecret`
- ensure selected secret provider is respected

### Step 2 - Normalize appsettings schema

- add/normalize `AeroCms:Bootstrap`
- add/normalize `AeroCms:DataProtection`
- ensure setup and runtime read the same keys

### Step 3 - Fix startup state semantics

- `Setup` = setup only
- `Configured` = bootstrap pending, traffic blocked
- `Running` = ready
- optionally use `Failed` for startup/bootstrap failure

### Step 4 - Add real runtime startup barrier

- start main host first
- await required infra with timeout CTS
- fail startup if required infra does not become ready

### Step 5 - Separate route mapping from runtime initialization

- route mapping should not hide migrations/bootstrap side effects
- runtime initialization should be a deliberate startup phase

### Step 6 - Wire real setup readiness UI

- implement polling in Blazor page model
- consume `/setup/status`
- compute readiness based on chosen modes
- disable submit until required services are ready

### Step 7 - Add targeted end-to-end tests

- setup → configured → running happy path
- infra timeout / bootstrap failure path
- persistence coverage for server/Infisical fields

---

## Acceptance Criteria

The bootstrap work is considered complete when all of the following are true:

1. Fresh install shows setup UI without requiring runtime-only services to be initialized first
2. Setup submit transitions automatically to the main app with no manual restart
3. Main host starts and waits for required embedded services using a timeout barrier
4. `Configured` does not allow normal traffic
5. `Running` is only written after runtime bootstrap succeeds
6. Setup UI shows real readiness state in Blazor and prevents premature submit
7. Server DB/cache and Infisical setup inputs persist correctly
8. Shared Data Protection works across setup and main app
9. Happy-path and failure-path bootstrap tests pass

---

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Shared Data Protection mismatch | High | Centralize DP registration and verify cross-app decrypt |
| `Configured` treated as ready | High | Gate normal traffic until `Running` only |
| Embedded infra never becomes ready | Medium | Timeout CTS + clear failure state/logging |
| Hidden bootstrap side effects in route mapping | Medium | Separate initialization from endpoint mapping |
| Partial persistence of server/Infisical fields | Medium | Complete persistence contract and add tests |

---

## References

- Microsoft Learn: [.NET Generic Host](https://learn.microsoft.com/aspnet/core/fundamentals/host/generic-host)
- Microsoft Learn: [Configure ASP.NET Core Data Protection](https://learn.microsoft.com/aspnet/core/security/data-protection/configuration/overview)
- Microsoft Learn: [Key Storage Providers](https://learn.microsoft.com/aspnet/core/security/data-protection/implementation/key-storage-providers)
- Spec: `aero-cms-startup-spec.md`
- Spec: `startup-signal-pattern.md`
- Spec: `startup-logic.md`

---

## Approval

This plan reflects the currently approved direction:

- two-app startup remains
- readiness uses snapshot + barrier
- setup UI is Blazor Server only
- bootstrap work should finish cleanly so focus can shift to the block editor
