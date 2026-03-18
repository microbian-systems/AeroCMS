# Implementation Plan: Module System (Infrastructure)

## Phase 1: Foundation (Contracts and Base Types)
- [x] Task: Define `IModule` and specialization interfaces in `Aero.Cms.Core`. 60a3ff1
- [x] Task: Implement `ModuleDescriptor` and `ModuleGraph` models. b6945bc
- [~] Task: Update `AeroModuleBase` to align with the new spec.
- [ ] Task: Conductor - User Manual Verification 'Phase 1: Foundation (Contracts and Base Types)' (Protocol in workflow.md)

## Phase 2: Module Discovery
- [ ] Task: Implement `IModuleDiscoveryService` using assembly scanning (Scrutor).
- [ ] Task: Implement `IModuleGraphService` for building the dependency graph.
- [ ] Task: Implement topological sorting for deterministic load order.
- [ ] Task: Conductor - User Manual Verification 'Phase 2: Module Discovery' (Protocol in workflow.md)

## Phase 3: ASP.NET Core Integration
- [ ] Task: Implement `IModuleBuilder` and its core functionality.
- [ ] Task: Create `AddAeroCmsModules` extension for `IServiceCollection`.
- [ ] Task: Create `UseAeroCmsModules` extension for `IApplicationBuilder`.
- [ ] Task: Conductor - User Manual Verification 'Phase 3: ASP.NET Core Integration' (Protocol in workflow.md)

## Phase 4: Final Verification and Stabilization
- [ ] Task: Create unit tests for discovery and dependency resolution.
- [ ] Task: Create integration tests for module loading in a test host.
- [ ] Task: Conductor - User Manual Verification 'Phase 4: Final Verification and Stabilization' (Protocol in workflow.md)
