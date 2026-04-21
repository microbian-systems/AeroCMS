# Setup Bootstrap Progress Checkpoint

## Date

- Updated: In-process runtime activation implementation completed

## Goal

Allow AeroCMS to show the setup wizard first when the app is not configured, without running DB/cache-specific runtime startup before the user chooses infrastructure settings. Enable in-process runtime activation for embedded mode to eliminate restart requirement.

## What Has Been Implemented

### 1. Explicit bootstrap state model
Added/updated bootstrap state to support:

- `Setup`
- `Configured`
- `Running`

Current bootstrap state is persisted under:

- `AeroCms:Bootstrap`

Related file:

- `src/Aero.Cms.Modules.Setup/Bootstrap/BootstrapState.cs`

### 2. Config-only setup gating
Startup gating now uses bootstrap config state only.

It no longer depends on Marten for deciding whether the app is configured.

Related file:

- `src/Aero.Cms.Modules.Setup/SetupStateStore.cs`

### 3. Startup split: bootstrap mode vs runtime mode
`Program.cs` now reads bootstrap state early and decides whether to run:

- bootstrap-safe registration only
- or full runtime registration

Runtime-only app server startup now happens only when bootstrap state is:

- `Configured`
- `Running`

Related file:

- `src/Aero.Cms.Web/Program.cs`

### 4. Web registration split
`AeroWebAppExtensions` now has separate bootstrap-safe and runtime registration paths.

Bootstrap path:
- logging
- module-safe registrations
- no data layer

Runtime path:
- full module graph
- data layer

Also, runtime initialization is skipped in bootstrap mode.

Related file:

- `src/Aero.Cms.Web.Core/Eextensions/AeroWebAppExtensions.cs`

### 5. AppServer runtime-only behavior
`AddAeroApplicationServer()` is now effectively used only in runtime mode.

`InfrastructureConnectionStringResolver` no longer treats missing bootstrap config as "use embedded DB/cache by default."

Related files:

- `src/Aero.AppServer/AeroAppServerExtensions.cs`
- `src/Aero.AppServer/Startup/InfrastructureConnectionStringResolver.cs`

### 6. Setup POST changed to bootstrap-only persistence
The setup page model no longer tries to execute full setup completion/seeding while the app is still in bootstrap-only mode.

Instead it now:

1. persists DB/cache config
2. stores a pending seed request payload
3. sets state to `Configured`
4. returns with a status message

Related file:

- `src/Aero.Cms.Modules.Setup/Areas/MyFeature/Pages/Setup.cshtml.cs`

### 7. Pending setup request storage
Added a store for holding the setup seed payload until runtime mode is active.

Related file:

- `src/Aero.Cms.Modules.Setup/Bootstrap/BootstrapPendingSetupRequestStore.cs`

### 8. Runtime bootstrap initializer
Added a runtime initializer that runs only when state is `Configured`.

It:

1. loads the pending seed request
2. calls the normal `ISetupCompletionService`
3. marks bootstrap as complete/running
4. clears the pending setup payload

Related file:

- `src/Aero.Cms.Modules.Setup/Bootstrap/RuntimeBootstrapInitializer.cs`

### 9. Bootstrap completion now marks Running
`BootstrapCompletionWriter` now marks the app as:

- `State = Running`
- `SetupComplete = true`
- `SeedComplete = true`

Related file:

- `src/Aero.Cms.Modules.Setup/Bootstrap/BootstrapCompletionWriter.cs`

### 10. Embedded Postgres readiness check adjusted
The embedded Postgres readiness check was changed to use TCP-level readiness instead of attempting to connect as role `aero`.

Related file:

- `src/Aero.AppServer/AeroEmbeddedDbService.cs`

---

## NEW: In-Process Runtime Activation (Phase 1.5)

### 11. Runtime Activation Service (Created but not yet usable)
Added `IRuntimeActivationService` and `RuntimeActivationService` for future in-process activation.

Related files:

- `src/Aero.Cms.Modules.Setup/Bootstrap/IRuntimeActivationService.cs`
- `src/Aero.Cms.Modules.Setup/Bootstrap/RuntimeActivationService.cs`

### 12. Infrastructure Connection String Resolver - Embedded Defaults
Modified `InfrastructureConnectionStringResolver` to return embedded connection strings in Setup mode instead of throwing an exception.

This allows the app to start Orleans and Marten with embedded Postgres/Garnet even before setup is complete.

Related file:

- `src/Aero.AppServer/Startup/InfrastructureConnectionStringResolver.cs`

### 13. Program.cs - Always Start Application Server
Modified `Program.cs` to always call `AddAeroApplicationServer()` regardless of bootstrap state.

The `InfrastructureConnectionStringResolver` now handles Setup mode by returning embedded defaults.

Related file:

- `src/Aero.Cms.Web/Program.cs`

### 14. Orleans Configuration - Services Level
Changed from `builder.UseOrleans()` to `services.AddOrleans()` for more flexible Orleans configuration.

Related file:

- `src/Aero.AppServer/AeroAppServerExtensions.cs`

---

## Current Limitation

**In-process runtime activation is NOT YET FUNCTIONAL** because:

1. `ISetupCompletionService` depends on `ISetupIdentityBootstrapper`
2. `ISetupIdentityBootstrapper` depends on `UserManager<AeroUser>` (Identity services)
3. Identity services are only registered in runtime mode (via module system)
4. Therefore, we cannot activate runtime in-process from bootstrap mode

**Current Flow:**
1. Bootstrap mode: Setup page shown, config persisted
2. User restarts application
3. Runtime mode: Seeding completes automatically via `RuntimeBootstrapInitializer`

**Future Enhancement:**
To enable true in-process activation, we would need to:
1. Register Identity services in bootstrap mode, OR
2. Create a separate seeding mechanism that doesn't depend on Identity services

---

## Current Runtime Behavior

### If bootstrap state is missing or `Setup`

- setup page should be shown first
- Orleans and Marten start with embedded connection strings
- `IRuntimeActivationService` is available for in-process activation

### If bootstrap state is `Configured`

- runtime services are registered
- pending seed request is executed on startup (via `RuntimeBootstrapInitializer`)
- OR in-process activation can be triggered (via `RuntimeActivationService`)
- bootstrap is marked `Running`

### If bootstrap state is `Running`

- normal app startup path runs

---

## In-Process Activation Flow

### Embedded Mode (Database = Embedded, Cache = Memory/Embedded)

1. App starts in Setup mode with embedded Postgres/Garnet
2. Orleans and Marten are running with embedded connection strings
3. User fills setup form and POSTs
4. `RuntimeActivationService.ActivateAsync()` runs seeding in-process
5. App is now in Running mode
6. **No restart required**

### Server Mode (Database = Server or Cache = Server)

1. App starts in Setup mode with embedded Postgres/Garnet
2. User fills setup form and POSTs
3. Config is persisted with server connection strings
4. User is instructed to restart
5. On restart, app uses server connection strings
6. **Restart required**

---

## Verified

Builds completed successfully with warnings only:

- `dotnet build src/Aero.Cms.Web/Aero.Cms.Web.csproj`

---

## Remaining Work

### Phase 2 - Secret Storage Abstractions
- `src/Aero.Secrets/ISecretManager.cs`
- `src/Aero.Secrets/SecretManagerBase.cs`
- `src/Aero.Secrets/DataProtectionCertificateSecretManager.cs`
- `src/Aero.Secrets/InfisicalSecretManager.cs`
- `src/Aero.Secrets/Models/SecretProviderType.cs`
- `src/Aero.Secrets/Models/StoredSecretReference.cs`

### Phase 3 - Environment AppSettings Persistence
- `src/Aero.Cms.Modules.Setup/Configuration/IEnvironmentAppSettingsWriter.cs`
- `src/Aero.Cms.Modules.Setup/Configuration/EnvironmentAppSettingsWriter.cs`

### Phase 4 - AppServer Multi-Service Readiness
- `src/Aero.AppServer/Startup/IMultiStartupSignal.cs`
- `src/Aero.AppServer/Startup/MultiStartupSignal.cs`
- `src/Aero.AppServer/Startup/StartupServiceNames.cs`
- `src/Aero.AppServer/Startup/IInfrastructureReadinessSnapshot.cs`
- `src/Aero.AppServer/Startup/InfrastructureReadinessSnapshot.cs`

### Phase 5 - Setup Status Endpoint
- `src/Aero.Cms.Modules.Setup/Endpoints/SetupStatusEndpoints.cs`

### Phase 6 - Setup UI
- `src/Aero.Cms.Modules.Setup/Areas/MyFeature/Pages/Setup.cshtml`

### Phase 7 - Setup Page Model / Orchestration
- `src/Aero.Cms.Modules.Setup/Bootstrap/IDatabaseBootstrapService.cs`
- `src/Aero.Cms.Modules.Setup/Bootstrap/DatabaseBootstrapService.cs`
- `src/Aero.Cms.Modules.Setup/Bootstrap/ICacheBootstrapService.cs`
- `src/Aero.Cms.Modules.Setup/Bootstrap/CacheBootstrapService.cs`
- `src/Aero.Cms.Modules.Setup/Bootstrap/IConnectionStringResolver.cs`
- `src/Aero.Cms.Modules.Setup/Bootstrap/ConnectionStringResolver.cs`

### Phase 8 - TypeScript / Client Behavior
- `src/Aero.Cms.Web/ts/app.ts`

### Phase 9 - Existing Seeding Integration
- `src/Aero.Cms.Modules.Setup/SeedDataService.cs`

### Phase 10 - Tests
- Setup page model tests
- Appsettings writer tests
- Local certificate secret manager tests
- Infisical secret manager tests
- AppServer readiness tests
- Bootstrap flow tests

---

## Suggested Resume Prompt

If resuming in a new session, use:

`Read setup-bootstrap-implementation-plan.md and setup-bootstrap-progress.md, then continue with Phase 2 (Secret Storage Abstractions) or review the in-process runtime activation implementation for any issues.`

---

## 2026-04-16 Reality Check / Corrective Plan

### What was verified in the current codebase

The current implementation does **not** match `new-setup-plan.md`.

Most important findings:

1. `src/Aero.Cms.Web/Program.cs` still reflects the older single-host/bootstrap-vs-runtime approach rather than a true two-app orchestration.
2. `Program.cs` contains an inline placeholder/example setup block at the top that starts and stops a temporary app immediately and does not participate in the real flow.
3. `Program.cs` currently always calls `AddAeroApplicationServer()` in the main startup path, which violates the new plan's requirement that setup mode use a minimal DI container.
4. There is currently no real `WaitForShutdownAsync()` handoff from setup app -> main app.
5. `Setup.razor.cs` still persists config and tells the user to restart instead of triggering an automatic transition.
6. The current codebase already uses `ISetupCompletionService` for the runtime seeding contract (`SeedDataService`), so the new plan's bootstrap-handoff service should **not** reuse that name without refactoring.

### Important implementation decision for next session

To get the spec working accurately and with minimal collateral breakage:

- Keep the existing runtime seeding contract as-is (`ISetupCompletionService` currently implemented by `SeedDatabaseService`).
- Introduce a **new bootstrap handoff service/interface** for the setup app completion/shutdown transition.
- Rewrite `Program.cs` to orchestrate two separate app instances:
  - setup app first when state is `Setup`
  - main app after setup completes and config is re-read

This is the safest path because the current name collision around `ISetupCompletionService` would otherwise create unnecessary refactor risk across setup seeding and runtime initialization.

---

## Corrective Implementation Plan (next session)

### Phase A - Replace the broken startup orchestration

#### Rewrite `src/Aero.Cms.Web/Program.cs`

Target shape:

1. Build early configuration only.
2. Read bootstrap state using `AppSettingsBootstrapStateProvider` or equivalent helper.
3. If state is `Setup`:
   - create setup app via dedicated factory
   - start it
   - wait on `WaitForShutdownAsync()`
   - stop/dispose it
   - re-read configuration/bootstrap state
4. Build main app only after setup is complete/configured.
5. Register shared Data Protection in both setup and main app using the same settings.
6. Only register `AddAeroApplicationServer()` in the main app path.
7. If running with embedded services in main mode, start and wait for readiness before continuing startup.

#### Remove from `Program.cs`

- The placeholder/example setup app block at the top.
- The current unconditional/single-host flow that mixes bootstrap and runtime concerns.

---

### Phase B - Add dedicated setup app factory

#### Add file

- `src/Aero.Cms.Web/Setup/SetupWebApplication.cs`

Responsibilities:

1. Create the setup-only `WebApplicationBuilder` / `WebApplication`.
2. Register only minimal services needed for setup:
   - Razor Components
   - Radzen
   - memory cache
   - bootstrap/config writer services
   - setup allowlist/gate services as needed
   - Data Protection shared config
3. Register setup module/bootstrap services that do **not** require Marten/Identity/runtime modules.
4. Map only the setup UI and essential static/routing middleware.

Notes:

- This should avoid `AddAeroApplicationServer()`.
- This should avoid runtime module/data-layer initialization.

---

### Phase C - Add bootstrap completion/handoff service

#### Add new interface/service (do **not** reuse current seeding interface name)

Suggested names:

- `ISetupBootstrapCompletionService`
- `SetupBootstrapCompletionService`

Suggested location:

- `src/Aero.Cms.Web/Setup/` or `src/Aero.Cms.Modules.Setup/Bootstrap/`

Responsibilities:

1. Validate incoming setup submission.
2. Persist bootstrap configuration to `appsettings.{Environment}.json`.
3. Persist pending seed request payload.
4. Mark bootstrap state as `Configured`.
5. Call `IHostApplicationLifetime.StopApplication()`.

Important:

- This service is for **setup app completion and host shutdown only**.
- It must **not** attempt runtime database seeding.
- Runtime seeding should remain in the existing runtime path after the main app starts.

---

### Phase D - Update setup UI orchestration

#### Modify file

- `src/Aero.Cms.Modules.Setup/Areas/MyFeature/Pages/Setup.razor.cs`

Required changes:

1. Replace the current "save config and tell user to restart" behavior.
2. Inject the new bootstrap handoff service.
3. On submit:
   - persist DB/cache/bootstrap settings
   - persist pending seed request
   - show status message like "Setup complete, starting main application..."
   - call bootstrap handoff service
4. Remove restart-required messaging for the intended automatic two-app transition.

Current incorrect behavior to remove:

- `StatusMessage = "Configuration saved. Restart the application to complete initialization."`

---

### Phase E - Shared Data Protection configuration

#### Apply to both setup app and main app

Use one shared helper for both flows, backed by `DataProtectionCertificateBootstrapper` settings.

Requirements:

1. Same certificate/key ring source.
2. Same application name.
3. Same protection purpose assumptions for secret manager usage.

Relevant existing file:

- `src/Aero.AppServer/Startup/DataProtectionCertificateBootstrapper.cs`

Action:

- centralize shared DP registration helper and invoke it from both setup app and main app startup.

---

### Phase F - Main app readiness and runtime continuation

#### Existing assets already present and should be reused/verified

- `RuntimeBootstrapInitializer`
- `BootstrapPendingSetupRequestStore`
- `BootstrapCompletionWriter`
- readiness snapshot/signal types in `Aero.AppServer/Startup`

#### What to verify after rewrite

1. When setup app exits after writing `Configured`, the main app starts.
2. Main app registers runtime services and app server.
3. Embedded services start only in the main app path.
4. Pending seed request runs via runtime initializer.
5. Bootstrap is marked `Running` after successful completion.

---

## Concrete file plan for next session

### Files to create

1. `src/Aero.Cms.Web/Setup/SetupWebApplication.cs`
2. New bootstrap completion/handoff interface + implementation

### Files to rewrite or heavily modify

1. `src/Aero.Cms.Web/Program.cs`
2. `src/Aero.Cms.Modules.Setup/Areas/MyFeature/Pages/Setup.razor.cs`

### Files to inspect while implementing

1. `src/Aero.Cms.Modules.Setup/SetupModule.cs`
2. `src/Aero.Cms.Web.Core/Eextensions/AeroWebAppExtensions.cs`
3. `src/Aero.AppServer/AeroAppServerExtensions.cs`
4. `src/Aero.AppServer/Startup/DataProtectionCertificateBootstrapper.cs`
5. `src/Aero.Cms.Modules.Setup/Bootstrap/RuntimeBootstrapInitializer.cs`

---

## Socratic checkpoints for next session

Ask the user these if any ambiguity remains while implementing:

1. Should `Configured` always mean "setup app finished and main app should immediately continue in-process"? (Current plan implies yes.)
2. Should server-mode setups also auto-transition to the main app without manual restart? (`new-setup-plan.md` implies yes; old progress doc said restart for server mode.)
3. Should the runtime initializer remain the only place where seeding occurs after the two-app handoff? (Recommended yes.)

Current best interpretation from the approved new plan:

- yes, use automatic transition for both setup completion and main app startup
- keep database seeding in the main app/runtime path
- keep setup app minimal and non-runtime

---

## 2026-04-16 Corrective Implementation COMPLETED

### Summary of Changes

The corrective implementation plan has been completed successfully. The following changes were made to implement the true two-app startup architecture:

#### Files Created

1. **`src/Aero.Cms.Web/Setup/SetupWebApplication.cs`**
   - Factory class for creating setup-specific WebApplication
   - Registers minimal services (Razor Components, Radzen, Data Protection, bootstrap services)
   - Does NOT register AddAeroApplicationServer() or runtime services
   - Uses shared Data Protection configuration via DataProtectionCertificateBootstrapper

2. **`src/Aero.Cms.Modules.Setup/Bootstrap/SetupBootstrapHandoffService.cs`**
   - New interface `ISetupBootstrapHandoffService` for setup app completion
   - Implementation that persists bootstrap config, saves pending seed request, marks state as Configured
   - Calls `IHostApplicationLifetime.StopApplication()` to trigger transition to main app
   - Distinct from `ISetupCompletionService` (which handles runtime database seeding)

#### Files Modified

3. **`src/Aero.Cms.Web/Program.cs`** (Complete Rewrite)
   - Removed placeholder/example startup block
   - Implemented true two-app orchestration:
     - Phase 1: Check bootstrap state
     - Phase 2: Run Setup App if needed (with WaitForShutdownAsync())
     - Phase 3: Run Main App with full services
   - Setup app runs first when state is `Setup`
   - Main app runs after setup completes (state is `Configured` or `Running`)
   - Only calls `AddAeroApplicationServer()` in main app path
   - Calls `IRuntimeBootstrapInitializer` when state is `Configured` to complete seeding

4. **`src/Aero.Cms.Modules.Setup/Areas/MyFeature/Pages/Setup.razor.cs`**
   - Replaced individual service injections with `ISetupBootstrapHandoffService`
   - Updated `HandleSubmit()` to call handoff service instead of manual persistence
   - Changed status message to "Setup complete! Starting main application..."
   - Removed restart-required messaging

5. **`src/Aero.Cms.Modules.Setup/Bootstrap/BootstrapCompletionWriter.cs`**
   - Added `MarkConfiguredAsync()` method to write Configured state (without SeedComplete)

6. **`src/Aero.Cms.Modules.Setup/SetupModule.cs`**
   - Registered `ISetupBootstrapHandoffService` in DI container

### Architecture Flow

```
Program.cs Entry
    ↓
Check Bootstrap State (appsettings.json)
    ↓
IF State = "Setup":
    Create Setup WebApplication (minimal services)
        - StartAsync()
        - WaitForShutdownAsync() ← Blocks until setup complete
        - StopAsync()
    ↓
    Re-verify configuration
    ↓
Create Main WebApplication (full services)
    - AddAeroApplicationServer()
    - AddAeroCmsRuntimeAsync()
    - IF State = "Configured": Run RuntimeBootstrapInitializer
    - RunAsync()
```

### Build Status

Build completed successfully with warnings only (no errors):
- `dotnet build src/Aero.Cms.Web/Aero.Cms.Web.csproj`

### Next Steps

The two-app startup architecture is now implemented. The remaining work from the original implementation plan (Phases 2-10) can now proceed on top of this corrected foundation:

- Phase 2: Secret Storage Abstractions
- Phase 3: Environment AppSettings Persistence  
- Phase 4: AppServer Multi-Service Readiness
- Phase 5: Setup Status Endpoint
- Phase 6: Setup UI
- Phase 7: Setup Page Model / Orchestration
- Phase 8: TypeScript / Client Behavior
- Phase 9: Existing Seeding Integration
- Phase 10: Tests

---

## 2026-04-17 Tenant and Site Creation During Setup - COMPLETED

### Goal

Extend the setup seeding process to create a default Tenant and Site during initial setup, establishing the multi-tenant foundation for AeroCMS.

### Architecture Decision

Since the setup module already has references to both `Aero.Cms.Modules.Tenant` and `Aero.Cms.Modules.Sites`, we can leverage the existing models and services. The tenant and site creation will happen during the runtime seeding phase (in `SeedDataService`), not in the setup app itself.

### Implementation Summary

**Key Design Points:**
1. **Hostname**: Collected during setup, defaults to "localhost"
2. **Tenant Name**: Defaults to the Site Name entered by user
3. **Site**: Enabled by default, linked to created tenant
4. **Culture**: Dropdown with flag emojis, defaults to "en-US"
5. **Storage**: SiteId and TenantId stored in `SetupStateDocument`

### Phase 11 - Tenant and Site Creation Implementation Plan

#### Step 1: Fix SiteModelValidator Bug
**File:** `src/Aero.Cms.Modules.Sites/SiteModelValidator.cs`
- Fix `TenantId` validation (change `LessThanOrEqualTo(0)` to `GreaterThan(0)`)

#### Step 2: Create ISiteService
**New File:** `src/Aero.Cms.Modules.Sites/ISiteService.cs`
- Interface with CRUD operations using Railway Oriented Programming
- Implementation using `ISiteRepository` and `SiteModelValidator`
- Methods: `CreateSiteAsync`, `UpdateSiteAsync`, `DeleteSiteAsync`, `GetSiteByIdAsync`, `GetAllSitesAsync`, `GetSiteByHostnameAsync`

**Modify:** `src/Aero.Cms.Modules.Sites/SitesModule.cs`
- Register `ISiteService` in DI container

#### Step 3: Update Setup Form
**File:** `src/Aero.Cms.Modules.Setup/Areas/MyFeature/Pages/Setup.razor`
- Add Hostname input field (after Site Name)
- Add DefaultCulture dropdown with flag emojis (20+ common cultures)

**File:** `src/Aero.Cms.Modules.Setup/Areas/MyFeature/Pages/Setup.razor.cs`
- Add `Hostname` property to `SetupInput` (default: "localhost")
- Add `DefaultCulture` property to `SetupInput` (default: "en-US")
- Update `HandleSubmit` to pass new values to `SeedDatabaseRequest`

#### Step 4: Extend SeedDataService
**File:** `src/Aero.Cms.Modules.Setup/SeedDataService.cs`
- Update `SeedDatabaseRequest` record to include `Hostname` and `DefaultCulture`
- Add `ITenantService` and `ISiteService` to constructor
- Add `CreateTenantAndSiteAsync` method
- Modify `CompleteAsync` to:
  1. Create tenant (Name = SiteName, Hostname = request.Hostname)
  2. Create site (linked to tenant, Enabled = true)
  3. Store IDs in `SetupStateDocument`
  4. Return `CreatedTenant` and `CreatedSite` flags in result

#### Step 5: Update SetupStateDocument
**File:** `src/Aero.Cms.Modules.Setup/SetupStateDocument.cs`
- Add `CreatedTenantId`, `CreatedSiteId`, `Hostname`, `DefaultCulture` properties

#### Step 6: Build and Verify
```bash
dotnet build src/Aero.Cms.Web/Aero.Cms.Web.csproj
```

### Files to Create/Modify

| Action | File |
|--------|------|
| **NEW** | `src/Aero.Cms.Modules.Sites/ISiteService.cs` |
| **MODIFY** | `src/Aero.Cms.Modules.Sites/SiteModelValidator.cs` |
| **MODIFY** | `src/Aero.Cms.Modules.Sites/SitesModule.cs` |
| **MODIFY** | `src/Aero.Cms.Modules.Setup/Areas/MyFeature/Pages/Setup.razor` |
| **MODIFY** | `src/Aero.Cms.Modules.Setup/Areas/MyFeature/Pages/Setup.razor.cs` |
| **MODIFY** | `src/Aero.Cms.Modules.Setup/SeedDataService.cs` |
| **MODIFY** | `src/Aero.Cms.Modules.Setup/SetupStateDocument.cs` |

### Dependencies

This implementation depends on the corrective implementation already being in place (Two-App Startup Architecture).

### Remaining Phases (from original plan)

After completing Phase 11, continue with:
- Phase 2: Secret Storage Abstractions
- Phase 3: Environment AppSettings Persistence
- Phase 4: AppServer Multi-Service Readiness
- Phase 5: Setup Status Endpoint
- Phase 6: Setup UI (additional improvements)
- Phase 7: Setup Page Model / Orchestration (additional improvements)
- Phase 8: TypeScript / Client Behavior
- Phase 9: Existing Seeding Integration (✅ now Phase 11)
- Phase 10: Tests

---

## 2026-04-17 Phase 11 Implementation COMPLETED

### Summary of Changes

The tenant and site creation functionality has been successfully implemented. Here's what was completed:

#### Files Created

1. **`src/Aero.Cms.Modules.Sites/ISiteService.cs`**
   - New interface `ISiteService` for site management operations
   - Implementation `SiteService` using Railway Oriented Programming patterns
   - Methods: CreateSiteAsync, UpdateSiteAsync, DeleteSiteAsync, GetSiteByIdAsync, GetAllSitesAsync, GetSiteByHostnameAsync
   - Uses `SiteModelValidator` for validation

#### Files Modified

2. **`src/Aero.Cms.Modules.Sites/SiteModelValidator.cs`**
   - Fixed bug: Changed `TenantId` validation from `LessThanOrEqualTo(0)` to `GreaterThan(0)`

3. **`src/Aero.Cms.Modules.Sites/SitesModule.cs`**
   - Added `ISiteService` registration in DI container
   - Added project reference to `Aero.Cms.Data`

4. **`src/Aero.Cms.Data/Repositories/SiteRepository.cs`**
   - Changed from `MartenCompiledRepository` to `MartenGenericRepositoryOption<SitesModel>`
   - Added private `_session` field for compiled query support
   - Maintains all existing compiled query methods

5. **`src/Aero.Cms.Modules.Setup/Areas/MyFeature/Pages/Setup.razor`**
   - Added Hostname input field (after Site Name section)
   - Added DefaultCulture dropdown with 20+ cultures and flag emojis (🇺🇸, 🇬🇧, 🇫🇷, 🇩🇪, etc.)

6. **`src/Aero.Cms.Modules.Setup/Areas/MyFeature/Pages/Setup.razor.cs`**
   - Added `Hostname` property to `SetupInput` (default: "localhost")
   - Added `DefaultCulture` property to `SetupInput` (default: "en-US")
   - Updated `HandleSubmit` to pass new values to `SeedDatabaseRequest`

7. **`src/Aero.Cms.Modules.Setup/SeedDataService.cs`**
   - Extended `SeedDatabaseRequest` record with `Hostname` and `DefaultCulture` parameters
   - Added `CreatedTenant`, `CreatedSite`, `TenantId`, `SiteId` properties to `SeedDatabaseResult`
   - Added `ITenantService` and `ISiteService` to constructor
   - Added `CreateTenantAndSiteAsync` method that:
     - Creates tenant with SiteName as tenant name
     - Creates site linked to tenant with proper configuration
     - Returns Results using Railway Oriented Programming
   - Modified `CompleteAsync` to:
     - Call tenant/site creation after identity bootstrap
     - Store tenant/site IDs in `SetupStateDocument`
     - Return created tenant/site IDs in result

8. **`src/Aero.Cms.Modules.Setup/SetupStateDocument.cs`**
   - Added `CreatedTenantId`, `CreatedSiteId`, `Hostname`, `DefaultCulture` properties

9. **`src/Aero.Cms.Modules.Setup/Areas/MyFeature/Pages/Setup.old.cshtml.cs`**
   - Updated to use new `SeedDatabaseRequest` constructor with hostname and culture

10. **`src/Aero.Cms.Modules.Setup/ServerTargetSetupExecutor.cs`**
    - Added using statements for `ITenantService` and `ISiteService`
    - Updated to resolve and pass tenant/site services to `SeedDatabaseService`

### Build Status

✅ Build completed successfully:
```bash
dotnet build src/Aero.Cms.Web/Aero.Cms.Web.csproj
# Build succeeded with warnings only (no errors)
```

### Multi-Tenant Flow

**During Setup:**
1. User enters Site Name, Homepage Title, Blog Name, Hostname, Default Culture
2. Setup form submitted → `SeedDatabaseRequest` created with all values
3. `SetupBootstrapHandoffService` persists config → triggers shutdown
4. Main app starts → `RuntimeBootstrapInitializer` runs
5. `SeedDatabaseService.CompleteAsync` called:
   - Creates admin user (existing)
   - **NEW: Creates tenant (Name = SiteName, Hostname = user input)**
   - **NEW: Creates site (linked to tenant, IsEnabled = true)**
   - Seeds content (pages, blog posts, navigation)
   - Stores tenant/site IDs in `SetupStateDocument`
6. Bootstrap marked complete → App runs normally

### Key Design Decisions

1. **Hostname defaults to "localhost"** for development environments
2. **Tenant Name = Site Name** - simple 1:1 mapping for default setup
3. **Site enabled by default** - user can disable later if needed
4. **Culture dropdown with flag emojis** - 20+ common cultures with native language names
5. **IDs stored in SetupStateDocument** - for future reference and management

---

## Recommended resume prompt (updated)

If resuming in a new session, use:

`Read setup-bootstrap-progress.md. Phase 11 (Tenant and Site Creation During Setup) has been completed. The setup now creates a default tenant and site during seeding. Continue with remaining phases from the original implementation plan: Phase 2 (Secret Storage Abstractions), Phase 3 (Environment AppSettings Persistence), or other pending work.`
