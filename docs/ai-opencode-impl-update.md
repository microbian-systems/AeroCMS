# OpenCode Model Selector — Implementation Plan

> Council-reviewed architecture. Strategy + Registry pattern. SOLID / GoF / OCP compliant.

## Architecture

```
┌─ Core / Abstractions ─────────────────────────────────────────┐
│  IProviderCapability { AiProviderKind Provider; }              │
│  IProviderModelLister : IProviderCapability {                  │
│      Task<Result<List<ProviderModelInfo>>> ListModelsAsync(    │
│          AiProviderProfile profile, CancellationToken ct);     │
│  }                                                             │
│  ProviderModelInfo(string Id, string? DisplayName,             │
│                    string? OwnedBy)                            │
│  ProviderCapabilityRegistry(IEnumerable<IProviderModelLister>) │
│                                                                │
│  AiProviderSettings / AiProviderOption                         │
│      + SupportsModelListing : bool  (computed server-side)     │
│                                                                │
│  EnhanceContentRequest                                         │
│      + ProviderOptions : IReadOnlyDictionary<string,string>?   │
└────────────────────────────────────────────────────────────────┘

┌─ Module Implementation ────────────────────────────────────────┐
│  OpenCodeModelLister : IProviderModelLister                    │
│      GET {endpoint}/models → List<ProviderModelInfo>           │
│      Caching: IMemoryCache (5 min sliding)                     │
│      Fallback: empty list on error                             │
│                                                                │
│  DI: services.AddSingleton<IProviderModelLister,               │
│                            OpenCodeModelLister>()              │
│                                                                │
│  API: GET /api/v1/admin/ai/providers/{id}/models               │
│      → Registry → finds lister → returns models                │
│                                                                │
│  AiSettingsStore: compute SupportsModelListing via registry     │
│  AiContentEnhancementService: honor ProviderOptions["model"]   │
└────────────────────────────────────────────────────────────────┘

┌─ Blazor UI ────────────────────────────────────────────────────┐
│  ProviderModelSelect.razor  (reusable component)               │
│      Props: ProviderId, OnModelChanged, SelectedModel          │
│      Renders: <select> populated via GET /providers/{id}/models│
│      Shown when provider.SupportsModelListing == true           │
│                                                                │
│  AiSettings.razor: <ProviderModelSelect />                     │
│  PostEditor.razor: <ProviderModelSelect />                     │
│                                                                │
│  ZERO `if (provider == AiProviderKind.OpenCode)` in UI code    │
└────────────────────────────────────────────────────────────────┘
```

## Files to Create

| # | File | Purpose |
|---|------|---------|
| 1 | `Aero/src/Aero.Core.Ai/IProviderCapability.cs` | `IProviderCapability`, `IProviderModelLister`, `ProviderModelInfo` |
| 2 | `Aero/src/Aero.Core.Ai/ProviderCapabilityRegistry.cs` | Resolves capabilities from `IEnumerable<T>` |
| 3 | `src/Aero.Cms.Modules.Ai/Services/OpenCodeModelLister.cs` | `: IProviderModelLister`, calls `{endpoint}/models` |

## Files to Modify

| # | File | Change |
|---|------|--------|
| 4 | `src/Aero.Cms.Abstractions/Ai/AiContentEnhancementContracts.cs` | Add `SupportsModelListing` to `AiProviderSettings` and `AiProviderOption`. Add `ProviderOptions` dict to `EnhanceContentRequest` |
| 5 | `src/Aero.Cms.Abstractions/Http/Clients/AiClient.cs` | Add `GetProviderModelsAsync(string providerId)` to interface + impl |
| 6 | `src/Aero.Cms.Modules.Ai/Api/AiApi.cs` | Add minimal API: `GET /api/v1/admin/ai/providers/{providerId}/models` |
| 7 | `src/Aero.Cms.Modules.Ai/Services/AiContentEnhancementService.cs` | Read `request.ProviderOptions["model"]` override; fall back to profile.Model |
| 8 | `src/Aero.Cms.Modules.Ai/Configuration/AiSettingsStore.cs` | Compute `SupportsModelListing` by checking registry |
| 9 | `src/Aero.Cms.Shared/Pages/Manager/AiSettings.razor` | Replace text input with `<ProviderModelSelect>` when `SupportsModelListing` |
| 10 | `src/Aero.Cms.Shared/Pages/Manager/AiSettings.razor.cs` | Wire up model select binding |
| 11 | `src/Aero.Cms.Shared/Pages/Manager/PostEditor/PostEditor.razor` | Add `<ProviderModelSelect>` in enhance panel when `SupportsModelListing` |
| 12 | `src/Aero.Cms.Shared/Pages/Manager/PostEditor/PostEditor.razor.cs` | Load models on provider change, send `ProviderOptions["model"]` in request |
| 13 | `src/Aero.Cms.Shared/Components/ProviderModelSelect.razor` | Reusable Blazor model selector component |
| 14 | `src/Aero.Cms.Modules.Ai/` (module startup) | Register `OpenCodeModelLister` in DI |

## Anti-patterns Avoided

- ❌ `IOpenCodeModelsService` — concrete service per provider
- ❌ `string? ModelOverride` on request — one-provider field
- ❌ `if (provider == AiProviderKind.OpenCode)` in Blazor — type check in UI
- ❌ `OpenAiModelsResponse` as shared DTO — leaks provider-specific API shape
- ❌ Giant switch/case for provider capabilities

## Adding a Future Provider

1. Create `SomeProviderModelLister : IProviderModelLister`
2. Add `services.AddSingleton<IProviderModelLister, SomeProviderModelLister>()`
3. Done — zero changes to UI, API, or existing code

## Model Override Flow

```
PostEditor enhance:
  User picks provider "opencode" → UI loads models via GET /providers/opencode/models
  User picks model "deepseek-v4-pro"
  User clicks "Generate suggestion"
  → EnhanceContentRequest {
        ProviderId: "opencode",
        ProviderOptions: { "model": "deepseek-v4-pro" },
        ...
    }
  → Server EnhanceAsync:
        effectiveModel = request.ProviderOptions?["model"] ?? profile.Model
        create ChatClient with effectiveModel
```

## Not in Scope

- Fixing `ToTornadoProvider` switch → separate PR, convert to `FrozenDictionary`
- Provider-specific settings beyond model selection
- LM Studio model listing (adds naturally via the same `IProviderModelLister` pattern)
