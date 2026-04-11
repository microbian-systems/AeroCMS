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