# CMS Source Generator System — Implementation Specification

> **Purpose:** Complete specification for the source generator subsystem of the block-based CMS.
> Designed for agent swarm implementation. Each generator is a discrete, independently implementable unit.
> All generators target C# 13 / .NET 10+ / Roslyn incremental generator API.

---

## Table of Contents

1. [Overview & Goals](#1-overview--goals)
2. [Project Structure](#2-project-structure)
3. [Shared Generator Infrastructure](#3-shared-generator-infrastructure)
4. [Generator 1 — CmsBlockSourceGenerator](#4-generator-1--cmsblocksourcegenerator)
5. [Generator 2 — CmsPipelineSourceGenerator](#5-generator-2--cmspipelinesourcegenerator)
6. [Generator 3 — CmsViewComponentSourceGenerator](#6-generator-3--cmsviewcomponentsourcegenerator)
7. [Generator 4 — CmsModuleSourceGenerator](#7-generator-4--cmsmodulesourcegenerator)
8. **[Marten + AOT + Source Generators Guide](marten-aot.md)** (Refer to dedicated specification)
9. [Generator 5 — CmsEventHandlerSourceGenerator](#8-generator-5--cmseventhandlersourcegenerator)
10. [Generator 6 — CmsAdminSectionSourceGenerator](#9-generator-6--cmsadminsectionsourcegenerator)
11. [Integration — Program.cs Generated Extensions](#10-integration--programcs-generated-extensions)
12. [Testing Source Generators](#11-testing-source-generators)
13. [Diagnostics & Errors](#12-diagnostics--errors)
14. [AOT Compatibility Matrix](#13-aot-compatibility-matrix)
15. [Agent Implementation Notes](#14-agent-implementation-notes)

---

## 1. Overview & Goals

### Problem

The CMS architecture uses several runtime patterns that rely on reflection:

| Pattern | Reflection Usage |
|---|---|
| Block JSON polymorphism | `JsonConverter` reads `blockType` string, calls `Deserialize<T>` via type map |
| Block renderer discovery | DI scans `IEnumerable<IBlockRenderer>` or `IBlockSliceRenderer` |
| Pipeline hook ordering | `IEnumerable<IPageReadHook>` sorted by `hook.Order` at runtime |
| ViewComponent dispatch | `IViewComponentHelper.InvokeAsync(string name, object args)` uses `MethodInfo.Invoke` |
| Module wiring | `assembly.GetTypes()` + `Activator.CreateInstance` |
| Event handler routing | `IEnumerable<ICmsEventHandler<TEvent>>` DI scan |

### Goal

Replace every pattern above with **compile-time generated code** using Roslyn incremental source generators so that:

1. Zero reflection in domain/infrastructure layer at runtime.
2. The generated code is fully compatible with standard ASP.NET Core performance optimizations.
3. Target **.NET 10+** features.

---

## 4. Generator 1 — CmsBlockSourceGenerator

### Responsibility

Scans for all non-abstract `BlockBase` subclasses and all `IBlockRenderer` (Admin) and `IBlockSliceRenderer` (CMS) implementations, then generates:

1. `BlockBase.Polymorphic.g.cs` — partial `BlockBase` with `[JsonPolymorphic]` + `[JsonDerivedType]` attributes.
2. `CmsJsonContext.g.cs` — `JsonSerializerContext` with `[JsonSerializable]` for every document type.
3. `CmsBlockServiceExtensions.g.cs` — `IServiceCollection.AddCmsBlocks()` extension method.

---

## 14. AOT Compatibility Matrix

| Component | Generator Output | Reflection Eliminated | AOT Compatible |
|---|---|---|---|
| Block JSON polymorphism | `[JsonPolymorphic]` + `CmsJsonContext` | ✓ `BlockJsonConverter` deleted | ✓ Full AOT |
| Block renderer registry | `AddCmsBlocks()` extension | ✓ No `IEnumerable` scan | ✓ Full AOT |

... [Rest of document updated for .NET 10]
