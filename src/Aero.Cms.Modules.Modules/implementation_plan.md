# Encapsulate Module Seeding in Aero.Cms.Modules.Modules

Currently, the logic for discovering and persisting modules during the initial application setup is located within the `Aero.Cms.Modules.Setup` project (`SeedDataService.cs`). This implementation plan aims to move this logic into the `Aero.Cms.Modules.Modules` project to improve domain encapsulation and follow the pattern established in other feature modules like `Headless`.

## User Review Required

> [!IMPORTANT]
> This change introduces a new dependency from `Aero.Cms.Modules.Setup` to `Aero.Cms.Modules.Modules`.
> The existing `ModulesSettings` class will be renamed to `ModulesModule` to align with the project's module naming convention.

## Proposed Changes

### [Aero.Cms.Modules.Modules]

This project will now serve as the domain owner for module management and initialization.

#### [MODIFY] [Aero.Cms.Modules.Modules.csproj](file:///c:/Users/bbqch/proj/microbians/AeroCMS/src/Aero.Cms.Modules.Modules/Aero.Cms.Modules.Modules.csproj)
- Add project reference to `Aero.Modular.csproj`.
- Ensure necessary dependencies for Marten and Core abstractions are present.

#### [NEW] [IModuleInitializationService.cs](file:///c:/Users/bbqch/proj/microbians/AeroCMS/src/Aero.Cms.Modules.Modules/Services/IModuleInitializationService.cs)
- Define `InitializeModulesAsync(CancellationToken ct)` method.

#### [NEW] [ModuleInitializationService.cs](file:///c:/Users/bbqch/proj/microbians/AeroCMS/src/Aero.Cms.Modules.Modules/Services/ModuleInitializationService.cs)
- Implement the service using logic extracted from `SeedDataService.cs`.
- Dependencies: `IModuleDiscoveryService`, `IModuleStateStore`.

#### [MODIFY] [ModulesModule.cs](file:///c:/Users/bbqch/proj/microbians/AeroCMS/src/Aero.Cms.Modules.Modules/ModulesModule.cs) (Renamed from `ModulesSettings.cs`)
- Change base class to `AeroWebModule` (or `AeroModuleBase` if web features aren't needed yet, but we'll follow `Headless` as a guide).
- Implement `ConfigureServices` to register `IModuleInitializationService` and `IModuleStateStore`.

---

### [Aero.Cms.Modules.Setup]

Update the setup orchestrator to delegate module seeding.

#### [MODIFY] [Aero.Cms.Modules.Setup.csproj](file:///c:/Users/bbqch/proj/microbians/AeroCMS/src/Aero.Cms.Modules.Setup/Aero.Cms.Modules.Setup.csproj)
- Add project reference to `Aero.Cms.Modules.Modules.csproj`.

#### [MODIFY] [SeedDataService.cs](file:///c:/Users/bbqch/proj/microbians/AeroCMS/src/Aero.Cms.Modules.Setup/SeedDataService.cs)
- Inject `IModuleInitializationService`.
- Remove direct dependencies on `IModuleDiscoveryService` and `IModuleStateStore`.
- Update `SaveModuleStateAsync` to call the new service.

#### [MODIFY] [SetupModule.cs](file:///c:/Users/bbqch/proj/microbians/AeroCMS/src/Aero.Cms.Modules.Setup/SetupModule.cs)
- Remove manual registration of `IModuleStateStore` and `ModuleStateStore` (now handled by `ModulesModule`).

## Verification Plan

### Automated Tests
- Since this is a refactoring of internal logic, we will verify by:
  - Performing a clean build of the solution.
  - Ensuring the `Setup` module still initializes correctly.
  - Using `TUnit` (if applicable) or manual verification of the setup flow.

### Manual Verification
- Run the application in "Setup" mode.
- Complete the setup wizard.
- Verify that modules are still correctly persisted in the `aero.mt_doc_modulestatedocument` table in Postgres.
