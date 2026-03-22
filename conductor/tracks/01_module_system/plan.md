# Implementation Plan: Module System (Infrastructure)

## Phase 1: Foundation (Contracts and Base Types) [checkpoint: dbadb31]
- [x] Task: Define `IModule` and specialization interfaces in `Aero.Cms.Core`. 60a3ff1
- [x] Task: Implement `ModuleDescriptor` and `ModuleGraph` models. b6945bc
- [x] Task: Update `AeroModuleBase` to align with the new spec.
- [x] Task: Conductor - User Manual Verification 'Phase 1: Foundation (Contracts and Base Types)' (Protocol in workflow.md)

## Phase 2: Module Discovery
- [x] Task: Implement `IModuleDiscoveryService` using assembly scanning (Scrutor).
- [x] Task: Implement `IModuleGraphService` for building the dependency graph.
- [x] Task: Implement topological sorting for deterministic load order.
- [x] Task: Conductor - User Manual Verification 'Phase 2: Module Discovery' (Protocol in workflow.md)

## Phase 3: ASP.NET Core Integration
- [x] Task: Implement `IModuleBuilder` and its core functionality.
- [x] Task: Create `AddAeroCmsModules` extension for `IServiceCollection`.
- [x] Task: Create `UseAeroCmsModules` extension for `IApplicationBuilder`.
- [x] Task: Conductor - User Manual Verification 'Phase 3: ASP.NET Core Integration' (Protocol in workflow.md)

## Phase 4: Final Verification and Stabilization
- [x] Task: Create unit tests for discovery and dependency resolution.
- [x] Task: Create integration tests for module loading in a test host.
- [x] Task: Conductor - User Manual Verification 'Phase 4: Final Verification and Stabilization' (Protocol in workflow.md)
