# Setup Bootstrap Progress Checkpoint

## Date

- Updated: 2026-04-21

## Goal

Finish bootstrap/setup work with a clean two-app startup architecture so the team can move on to the block editor.

---

## Approved End State

### Setup App

- runs only when bootstrap state is `Setup`
- uses minimal DI
- persists setup/bootstrap configuration
- persists pending runtime seed/bootstrap request
- marks bootstrap `Configured`
- calls `StopApplication()`

### Main App

- starts after setup app exits
- starts hosted services first via `StartAsync()`
- waits for required embedded infrastructure with a timeout barrier
- runs runtime bootstrap/seeding/init
- marks bootstrap `Running`
- only then allows normal traffic

### State Semantics

- `Setup` = setup UI only
- `Configured` = runtime bootstrap pending, not ready
- `Running` = traffic ready
- `Failed` = startup/bootstrap failure

### Chosen Technical Decisions

- **Readiness model:** snapshot + awaitable barrier
- **Setup UI interactivity:** Blazor Server only
- **No setup-specific TypeScript work required**

---

## What Is Already Implemented

### 1. Two-app startup foundation

- `src/Aero.Cms.Web/Program.cs`
- `src/Aero.Cms.Web/Setup/SetupWebApplication.cs`

Status: **Implemented, but runtime ordering still needs tightening**

### 2. Bootstrap state model/provider

- `src/Aero.Cms.Modules.Setup/Bootstrap/BootstrapState.cs`
- `src/Aero.Cms.Modules.Setup/Bootstrap/IBootstrapStateProvider.cs`
- `src/Aero.Cms.Modules.Setup/Bootstrap/AppSettingsBootstrapStateProvider.cs`

Status: **Implemented**

### 3. Setup handoff service

- `src/Aero.Cms.Modules.Setup/Bootstrap/SetupBootstrapHandoffService.cs`

Status: **Implemented, but persistence contract is incomplete**

### 4. Setup gate and setup UI

- `src/Aero.Cms.Modules.Setup/SetupGateMiddleware.cs`
- `src/Aero.Cms.Modules.Setup/Areas/MyFeature/Pages/Setup.razor`
- `src/Aero.Cms.Modules.Setup/Areas/MyFeature/Pages/Setup.razor.cs`

Status: **Implemented, but real readiness gating is not done**

### 5. Runtime continuation path

- `src/Aero.Cms.Modules.Setup/Bootstrap/RuntimeBootstrapInitializer.cs`
- `src/Aero.Cms.Modules.Setup/Bootstrap/BootstrapPendingSetupRequestStore.cs`
- `src/Aero.Cms.Modules.Setup/Bootstrap/BootstrapCompletionWriter.cs`

Status: **Implemented, but state semantics still need correction**

### 6. Readiness plumbing

- `src/Aero.AppServer/Startup/IMultiStartupSignal.cs`
- `src/Aero.AppServer/Startup/MultiStartupSignal.cs`
- `src/Aero.AppServer/Startup/IInfrastructureReadinessSnapshot.cs`
- `src/Aero.AppServer/Startup/InfrastructureReadinessSnapshot.cs`
- `src/Aero.AppServer/AeroEmbeddedDbService.cs`
- `src/Aero.AppServer/AeroCacheService.cs`

Status: **Partial**

### 7. Setup status endpoint

- `src/Aero.Cms.Modules.Setup/Endpoints/SetupStatusEndpoints.cs`

Status: **Implemented, but not yet consumed correctly by setup UI**

### 8. Secret abstraction work

- `Aero/src/Aero.Secrets/ISecretManager.cs`
- `Aero/src/Aero.Secrets/SecretManagerBase.cs`
- `Aero/src/Aero.Secrets/DataProtectionCertificateSecretManager.cs`
- `Aero/src/Aero.Secrets/InfisicalSecretManager.cs`

Status: **Implemented, integration incomplete**

### 9. Environment appsettings writer

- `src/Aero.Cms.Modules.Setup/Configuration/IEnvironmentAppSettingsWriter.cs`
- `src/Aero.Cms.Modules.Setup/Configuration/EnvironmentAppSettingsWriter.cs`

Status: **Implemented**

### 10. Tenant/site creation during setup seeding

Tenant/site creation work has already been completed and should remain on top of the final bootstrap architecture.

Status: **Implemented**

---

## What Is Incomplete or Incorrect Right Now

### 1. `Configured` is still too close to "ready"

This is the biggest correctness issue.

`Configured` should mean:

- configuration persisted
- runtime bootstrap still pending
- traffic still blocked

### 2. Main runtime startup ordering is not yet safe enough

The main host needs a clearer flow:

1. `StartAsync()`
2. wait for required infra using timeout CTS
3. run runtime bootstrap/init
4. mark `Running`

### 3. Readiness barrier is not truly awaited yet

Current readiness state exists, but it is not yet a strong awaited startup barrier.

### 4. Setup UI readiness is still mostly placeholder

Current setup UI does not yet provide real readiness-driven submit gating.

### 5. Persistence contract is incomplete

The setup flow still needs to fully carry/persist:

- `ConnectionString`
- `CacheConnectionString`
- `InfisicalMachineId`
- `InfisicalClientSecret`

### 6. Appsettings schema is not normalized

`AeroCms:Bootstrap` and `AeroCms:DataProtection` are not yet consistently present in environment appsettings files.

### 7. In-process runtime activation direction is obsolete

The earlier “always start application server in setup mode / in-process runtime activation” direction is no longer the chosen architecture.

The approved direction is:

- minimal setup app first
- full main app second
- real startup barrier in main app

---

## Current Priority Work Queue

### Priority 1 - Finish the persistence contract

#### Files
- `src/Aero.Cms.Modules.Setup/Areas/MyFeature/Pages/Setup.razor.cs`
- `src/Aero.Cms.Modules.Setup/Bootstrap/SetupBootstrapHandoffService.cs`
- `src/Aero.Cms.Modules.Setup/Bootstrap/DatabaseBootstrapService.cs`
- `src/Aero.Cms.Modules.Setup/Bootstrap/CacheBootstrapService.cs`
- `src/Aero.Cms.Modules.Setup/Bootstrap/BootstrapPersistenceModels.cs`

#### Tasks
- pass full setup inputs through handoff
- persist server DB/cache settings correctly
- persist Infisical auth/reference metadata correctly

Status: **Next**

### Priority 2 - Normalize appsettings schema

#### Files
- `src/Aero.Cms.Web/appsettings.json`
- `src/Aero.Cms.Web/appsettings.Development.json`
- `src/Aero.Cms.Web/appsettings.Staging.json`
- `src/Aero.Cms.Web/appsettings.Production.json`

#### Tasks
- add/normalize `AeroCms:Bootstrap`
- add/normalize `AeroCms:DataProtection`

Status: **Next**

### Priority 3 - Fix runtime startup semantics

#### Files
- `src/Aero.Cms.Web/Program.cs`
- `src/Aero.Cms.Web.Core/Eextensions/AeroWebAppExtensions.cs`
- `src/Aero.Cms.Modules.Setup/SetupStateStore.cs`
- `src/Aero.Cms.Modules.Setup/Bootstrap/RuntimeBootstrapInitializer.cs`
- `src/Aero.Cms.Modules.Setup/Bootstrap/BootstrapCompletionWriter.cs`

#### Tasks
- treat `Configured` as not ready
- start main host before runtime bootstrap work
- add real timeout-based readiness barrier
- only mark `Running` after success

Status: **High priority**

### Priority 4 - Make readiness real in setup UI

#### Files
- `src/Aero.Cms.Modules.Setup/Areas/MyFeature/Pages/Setup.razor.cs`
- `src/Aero.Cms.Modules.Setup/Areas/MyFeature/Pages/Setup.razor`
- `src/Aero.Cms.Modules.Setup/Endpoints/SetupStatusEndpoints.cs`

#### Tasks
- implement Blazor polling
- consume `/setup/status`
- compute readiness from selected modes
- disable submit until ready

Status: **High priority**

### Priority 5 - Tighten embedded readiness coordination

#### Files
- `src/Aero.AppServer/AeroEmbeddedDbService.cs`
- `src/Aero.AppServer/AeroCacheService.cs`
- `src/Aero.AppServer/Startup/IMultiStartupSignal.cs`
- `src/Aero.AppServer/Startup/MultiStartupSignal.cs`
- `src/Aero.AppServer/Startup/IInfrastructureReadinessSnapshot.cs`
- `src/Aero.AppServer/Startup/InfrastructureReadinessSnapshot.cs`

#### Tasks
- make readiness coordination truly awaitable
- make required waits mode-aware
- fix any port/readiness mismatches

Status: **High priority**

### Priority 6 - Add focused verification tests

#### Test targets
- setup → configured → running happy path
- timeout/failure path
- readiness gating
- full persistence of server/Infisical fields
- shared data protection compatibility

Status: **Required before calling bootstrap done**

---

## Out-of-Scope / Superseded Items

These items are no longer the active direction unless later re-approved:

- setup-specific TypeScript/client behavior
- in-process runtime activation as the main architecture
- treating setup mode as a place to run full app server/runtime services
- introducing `IEmbeddedServiceHealth` instead of finishing the current snapshot+barrier approach

---

## Definition of Done for Bootstrap Work

Bootstrap/setup is done when:

1. Setup app is minimal and isolated from runtime-only services
2. Main host starts and waits for required infra with timeout
3. `Configured` does not allow normal traffic
4. `Running` is only written after runtime bootstrap succeeds
5. Setup UI shows real readiness state and gates submit in Blazor
6. Full setup inputs persist correctly for server/Infisical scenarios
7. Shared Data Protection works across setup and main app
8. Happy-path and failure-path integration tests pass

At that point bootstrap work should stop and focus should move to the block editor.

---

## Suggested Resume Prompt

If resuming in a new session, use:

`Read new-setup-plan.md and setup-bootstrap-progress.md. Continue the approved two-app bootstrap finish plan using the snapshot + barrier readiness model and Blazor-only setup UI. Start with Priority 1 and Priority 3 unless the current branch already contains those changes.`
