# Source Generator Chaining Limitation — The Big Issue

## The Problem

**Third-party modules shipping as NuGet packages cannot add new block types** without modifying the core `BlockJsonContext.cs` file in `Aero.Cms.Abstractions`. This makes the plugin system non-functional for external consumers.

> [!REVIEW]
> This is the right failure mode to call out, but the wording should be narrowed before this becomes an implementation contract. A third-party module can add block types without modifying `BlockJsonContext.cs` if it ships its own source-generated `JsonSerializerContext` and AeroCMS has a first-class way to register that context. The current problem is that AeroCMS has no composed runtime JSON-contract registration path yet; it is not that every external block must necessarily be known by the core context. See: [Microsoft Learn — combine source-generated contexts](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/source-generation#combine-source-generators).

## Current Architecture

There are **two** JSON serializer contexts, but only one works at runtime:

### 1. Hand-maintained: `BlockJsonContext` (LIVE)

`src/Aero.Cms.Abstractions/Blocks/Serialization/BlockJsonContext.cs`

- ~140 lines of hand-authored `[JsonSerializable]` attributes for every block type
- Referenced directly in code:
  - `ContentItemExtensions.Get<T>()` → `BlockJsonContext.Default.Options`
  - `GeneratedMartenConfiguration.UseAeroGeneratedJsonContext()` → `BlockJsonContext.Default`
  - `ContentTypeDynamicBlockBridge` → `BlockJsonContext.Default.Options`
- **Must be updated every time a new block type is added**

### 2. Source-generated: `GeneratedBlockJsonContext` (DEFERRED)

Emitted by `BlockRendererGenerator.cs` (lines 167-185, 755-771):

```csharp
// NOTE: GeneratedBlockJsonContext.g.cs emission is DEFERRED.
// The RenderGeneratedContext method and crossAssemblyBlockData pipeline
// are kept as infrastructure for future use when the Roslyn source
// generator chaining limitation is resolved (dotnet/roslyn#57239).
// Currently, STJ's JsonSourceGenerator cannot see [JsonSerializable]
// attributes emitted by another generator in the same compilation.
```

The generator already has all the code to produce `GeneratedBlockJsonContext` — it's just **commented out**.

## Root Cause: Roslyn Generator Chaining (roslyn#57239)

The STJ source generator (`System.Text.Json.SourceGeneration`) runs as a separate generator. It discovers `[JsonSerializable]` attributes at compile time. But when our `BlockRendererGenerator` emits `[JsonSerializable]` attributes through `AddSource()`, the STJ generator **cannot see them** — they're in "generated file space", not in the original compilation.

**The chain breaks like this:**

```
[BlockMetadata] on MyBlock
    ↓
BlockRendererGenerator emits [JsonSerializable(typeof(MyBlock))] in GeneratedBlockJsonContext.g.cs
    ↓
STJ's JsonSourceGenerationGenerator ✗ CANNOT see this emitted attribute
    ↓
GeneratedBlockJsonContext (our partial class) has no implementation — runtime failure
```

This is a **Roslyn limitation**, not an STJ limitation. The STJ generator only sees attributes that exist in the **original source** before any generator runs. It does not re-scan generated files.

## Why Two Contexts Coexist

| Context | How it gets `[JsonSerializable]` | Works? |
|---------|----------------------------------|--------|
| `BlockJsonContext` (hand) | Hand-written attributes in source | ✅ Always |
| `GeneratedBlockJsonContext` (source gen) | Attributes emitted by `BlockRendererGenerator` | ❌ STJ generator can't see them |

The shim project `Aero.Cms.Generated.Json` wraps `BlockJsonContext.Default` into Marten's serializer and notes the limitation in comments.

> [!REVIEW]
> This matches the current code. `GeneratedMartenConfiguration.UseAeroGeneratedJsonContext()` still sets `stj.TypeInfoResolver = BlockJsonContext.Default`, and `BlockRendererGenerator` still leaves `GeneratedBlockJsonContext.g.cs` emission deferred. Keep this section: it is a good current-state snapshot.

## Impact on NuGet Packages

If a third party creates `Aero.Cms.Modules.Foo` with a new `FooBlock : BlockBase`:

1. `[BlockMetadata]` and `[JsonDerivedType]` work for polymorphic serialization — the source-generated `BlockBase.Polymorphic.g.cs` picks up all `[JsonDerivedType]` correctly
2. But `BlockJsonContext` has no `[JsonSerializable(typeof(FooBlock))]` — so direct `Deserialize<FooBlock>()` or `Serialize(fooList)` via `BlockJsonContext.Default` will throw `NotSupportedException` at runtime
3. The module's author would need to either:
   - Submit a PR to add their type to `BlockJsonContext.cs` (defeats pluggability)
   - Create their own `FooJsonContext : JsonSerializerContext` and register it manually

> [!REVIEW]
> The second bullet is probably the strategic direction, not just an escape hatch. For NuGet packages, the cleanest boundary is "generate in the module project, aggregate in the host." The module's own compilation can contain real `[JsonSerializable]` source, so STJ can generate a normal context there. The host should then compose the module's context into its serializer options. This avoids asking the host generator to inspect source that is hidden behind a compiled package.

## ✋ Deferred — Will Revisit After Content Type System Ships

This issue is **acknowledged and documented** but deferred. The content type system is shipping with the hand-maintained `BlockJsonContext.cs` as a tactical bridge. We'll return to this immediately afterward because this is foundational infrastructure.

---

## Chosen Strategy: Option A — Runtime Composite Resolver with Full GetTypeInfo Source Gen

This is **the only correct approach** for a pluggable CMS. It requires source gen to emit the complete `JsonSerializerContext` implementation (not just `[JsonSerializable]` attributes), bypassing the Roslyn chaining limitation entirely.

> [!REVIEW]
> I would not treat this as "the only correct approach." The runtime composite resolver part is correct and supported by STJ, but emitting a complete `JsonSerializerContext` implementation from `BlockRendererGenerator` is high-risk. It means AeroCMS would reimplement a large part of STJ's source generator behavior: object creators, property metadata, constructor handling, records, init-only properties, inherited members, `[JsonIgnore]`, custom converters, naming policies, required members, nullable behavior, collection contracts, and future runtime changes. Microsoft documents the supported composition model as combining `JsonSerializerContext`/`IJsonTypeInfoResolver` instances through `JsonTypeInfoResolver.Combine(...)` or `JsonSerializerOptions.TypeInfoResolverChain`; that model lets STJ keep owning context implementation details. See: [Microsoft Learn — combine source generators](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/source-generation#combine-source-generators), [JsonSerializerContext.GetTypeInfo](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonserializercontext.gettypeinfo?view=net-10.0), and [JsonSerializerContext.GeneratedSerializerOptions](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonserializercontext.generatedserializeroptions?view=net-10.0).

> [!REVIEW]
> Recommended alternative: make each module own its JSON contract. A first-party or third-party module can ship a concrete partial context such as `FooModuleJsonContext : JsonSerializerContext` with `[JsonSerializable(typeof(FooBlock))]` attributes in the module's source. STJ then generates the real implementation during that module's build. AeroCMS only needs to compose registered contexts at startup.

### Architecture

```
┌──────────────────────────────────────────────────────────┐
│                  CompositeJsonResolver                    │
│  (runtime assembled, owns TypeInfoResolverChain)          │
│                                                           │
│  Chain:                                                   │
│  1. CoreBlockJsonContext / BlockJsonContext               │
│  2. ModuleAJsonContext (3rd party module)                 │
│  3. ModuleBJsonContext (another module)                   │
│  4. Optional fallback resolver                            │
└───────────────────────┬──────────────────────────────────┘
                        │
            ┌───────────┴───────────┐
            ▼                       ▼
    JsonSerializer.Serialize()    Marten storage
```

> [!REVIEW]
> Prefer this chain shape over "GeneratedBlockJsonContext owns all blocks." The pluggability boundary should be that every module contributes its own resolver/context. Chain ordering matters: `JsonSerializerOptions` asks resolvers in order and returns the first non-null contract. See: [JsonSerializerOptions.TypeInfoResolverChain](https://learn.microsoft.com/dotnet/api/system.text.json.jsonserializeroptions.typeinforesolverchain?view=net-10.0).

### Step-by-step Plan

#### Step 1 — Source Generator: Emit `GetTypeInfo` Implementation (~150 lines changed)

**File:** `src/Aero.Cms.SourceGenerators/BlockRendererGenerator.cs`

The existing `RenderGeneratedContext()` emits `[JsonSerializable]` attributes but relies on STJ's generator to produce the implementation. We change this to emit a **complete, self-contained `JsonSerializerContext`**:

> [!REVIEW]
> This is the riskiest step in the plan. It is technically possible to generate contract metadata, but it is no longer just "source generator discovery"; it becomes a parallel serializer-contract generator. Before choosing this path, prove it against representative block models that include nested collections, dictionaries, nullable values, inherited properties, custom converters, init-only properties, and STJ attributes. Without that proof, the module-owned-context approach is much safer.

```csharp
// Generated: GeneratedBlockJsonContext.g.cs
// Emitted entirely by BlockRendererGenerator — NO STJ source gen dependency

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Aero.Cms.Generated;

public partial class GeneratedBlockJsonContext : JsonSerializerContext
{
    // ── Cached JsonTypeInfo per type ──
    private JsonTypeInfo<RichTextBlock>? _richTextBlock;
    private JsonTypeInfo<HeroBlock>? _heroBlock;
    private JsonTypeInfo<ContentEmbedBlock>? _contentEmbedBlock;
    private JsonTypeInfo<List<RichTextBlock>>? _listRichTextBlock;
    // ... one field per discovered type + one per List<T> variant

    // ── Default instance ──
    public static new GeneratedBlockJsonContext Default { get; } = new(
        new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() });

    public GeneratedBlockJsonContext() : base() { }
    public GeneratedBlockJsonContext(JsonSerializerOptions options) : base(options) { }

    // ── GetTypeInfo dispatch ──
    public override JsonTypeInfo? GetTypeInfo(Type type)
    {
        // Fast path: concrete type lookup
        if (type == typeof(RichTextBlock)) return _richTextBlock ??= Create_RichTextBlock();
        if (type == typeof(List<RichTextBlock>)) return _listRichTextBlock ??= Create_List_RichTextBlock();
        // ... etc for all discovered types
        return null;
    }

    // ── Factory methods per type ──
    private JsonTypeInfo<RichTextBlock> Create_RichTextBlock() =>
        JsonMetadataServices.CreateValueInfo<RichTextBlock>(Options, JsonMetadataServices.GetObjectJsonTypeInfo(typeof(RichTextBlock), ...));
    
    private JsonTypeInfo<List<RichTextBlock>> Create_List_RichTextBlock() =>
        JsonMetadataServices.CreateListInfo<List<RichTextBlock>, RichTextBlock>(Options, Create_RichTextBlock());
}
```

> [!REVIEW]
> The sketch is incomplete as a real `JsonSerializerContext` implementation. In current STJ, `JsonSerializerContext` also has `GeneratedSerializerOptions` as an abstract member, and generated contexts expose typed `JsonTypeInfo<T>` properties as well as `GetTypeInfo(Type)`. If AeroCMS emits this manually, it needs to match the full abstract contract and enough generated-context conventions for callers that expect normal source-generated context behavior. See: [JsonSerializerContext class](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonserializercontext?view=net-10.0).

The key insight: `JsonMetadataServices` has factory methods for all primitive, object, and collection types. We generate one factory method per discovered type. This is what STJ's source generator does internally — we just do it ourselves.

> [!REVIEW]
> This is exactly the maintenance concern. "Do what STJ's source generator does internally" binds AeroCMS to implementation-level behavior rather than the public authoring model documented for application code. That is especially fragile while this repo targets .NET 10 previews. If this path remains in the doc, mark it as an advanced fallback, not the default strategy.

**Implementation details in `RenderGeneratedContext()`:**

For each discovered `BlockModelDescriptor`, emit:
1. A cached `JsonTypeInfo<T>?` field (nullable, lazy)
2. A `GetTypeInfo()` branch that checks `type == typeof(T)` and returns the cached value
3. A private factory method using `JsonMetadataServices` APIs:
   - For concrete objects: `JsonMetadataServices.CreateObjectInfo<T>(Options, jsonObjectInfo)`
   - For lists: `JsonMetadataServices.CreateListInfo<List<T>, T>(Options, elementInfo)`
   - For dictionaries: `JsonMetadataServices.CreateDictionaryInfo<Dict<K,V>, K, V>(Options, keyInfo, valueInfo)`
   - For primitives (string, int, long, bool, DateTime): `JsonMetadataServices.CreateValueInfo<T>(Options, JsonMetadataServices.GetXxxJsonTypeInfo(typeof(T)))` — use the built-in `JsonMetadataServices` static methods

**Required JSON metadata for object types:**
```csharp
JsonObjectInfoValues<T> infoValues = new()
{
    ObjectCreator = () => new T(),
    SerializationHandler = ...,
    PropertyMetadataInitializer = ...
};
```

This is the most complex part — property metadata. For each property we need to emit:
```csharp
JsonPropertyInfoValues<TProperty> propInfo = new()
{
    IsProperty = true,
    IsPublic = true,
    Name = "propertyName",
    PropertyType = typeof(TProperty),
    Getter = obj => ((T)obj).PropertyName,
    Setter = (obj, val) => ((T)obj).PropertyName = (TProperty)val!,
};
```

The source generator already discovers all block types and their metadata via `[BlockMetadata]`. It can also discover properties via Roslyn syntax trees. The properties don't need custom attributes — they just need name/getter/setter lambdas that the generator emits.

> [!REVIEW]
> Property discovery needs more detail before implementation. It must honor STJ-visible shape, not just Roslyn public properties: ignored members, renamed members, converter attributes, required/init behavior, constructors, inherited members, nullable annotations, and collection element contracts all affect serialization. A generator that only emits public property getters/setters will silently diverge from STJ behavior.

#### Step 2 — Clean `BlockJsonContext.cs` (~140 lines removed)

**File:** `src/Aero.Cms.Abstractions/Blocks/Serialization/BlockJsonContext.cs`

Remove all `[JsonSerializable]` attributes. Keep only the options and an empty class:

```csharp
// No longer holds [JsonSerializable] entries — those are in GeneratedBlockJsonContext
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default | JsonSourceGenerationMode.Metadata)]
public partial class BlockJsonContext : JsonSerializerContext
{
    // Empty — kept as a well-known type reference for backward compat
    // All serializable types are registered via GeneratedBlockJsonContext from the source generator
}
```

This remains as a **fallback context** in the resolver chain — an empty context that resolves nothing, but provides a consistent type reference for code that currently depends on `BlockJsonContext.Default`.

> [!REVIEW]
> An empty `JsonSerializerContext` may not be useful as a fallback resolver. If it resolves nothing, keeping it in the chain only preserves a type name, not behavior. A safer migration is to keep `BlockJsonContext` as the core/first-party context until module contexts are registered and tests prove all existing block contracts are covered elsewhere.

#### Step 3 — Create `CompositeJsonTypeInfoResolver` (new file)

**File:** `src/Aero.Cms.Core/Serialization/CompositeJsonTypeInfoResolver.cs`

```csharp
public sealed class CompositeJsonTypeInfoResolver : IJsonTypeInfoResolver
{
    private readonly IReadOnlyList<IJsonTypeInfoResolver> _resolvers;

    public CompositeJsonTypeInfoResolver(IEnumerable<IJsonTypeInfoResolver> resolvers)
    {
        _resolvers = resolvers.ToArray();
    }

    public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        foreach (var resolver in _resolvers)
        {
            var info = resolver.GetTypeInfo(type, options);
            if (info is not null) return info;
        }
        return null;
    }
}
```

> [!REVIEW]
> This custom resolver is fine, but .NET already has built-in composition via `JsonTypeInfoResolver.Combine(...)` and, in .NET 8+, mutable `JsonSerializerOptions.TypeInfoResolverChain`. Unless AeroCMS needs extra diagnostics or ordering policy, prefer the built-in chain to reduce custom code. See: [Microsoft Learn — combine source generators](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/source-generation#combine-source-generators).

#### Step 4 — Update `GeneratedMartenConfiguration.cs` (~30 lines changed)

**File:** `src/Aero.Cms.Generated.Json/GeneratedMartenConfiguration.cs`

Switch from `BlockJsonContext.Default` to the composite:

```csharp
public static StoreOptions UseAeroGeneratedJsonContext(this StoreOptions options)
{
    options.UseSystemTextJsonForSerialization(configure: stj =>
    {
        stj.TypeInfoResolver = new CompositeJsonTypeInfoResolver(
        [
            GeneratedBlockJsonContext.Default,     // source gen — all block types
            // Module-registered contexts get added at module load time
        ]);
        stj.AllowOutOfOrderMetadataProperties = true;
    });
    return options;
}
```

The composite resolver is populated at module load time when modules register their `JsonSerializerContext` instances.

> [!REVIEW]
> This lifecycle needs to be pinned down. Marten's serializer options should be configured after the module graph is known and before the Marten store is built. Avoid a design that "updates Marten's serializer" after the store/options have already been materialized unless Marten explicitly supports that timing.

#### Step 5 — Module Registration API

**File:** `src/Aero.Modular/IModuleBuilder.cs`

```csharp
void AddJsonContext<TContext>() where TContext : JsonSerializerContext;
```

> [!REVIEW]
> Consider accepting an instance or factory in addition to a type. Most STJ-generated contexts expose a static `Default` instance already configured from `[JsonSourceGenerationOptions]`; registering `FooModuleJsonContext.Default` avoids needing DI to construct generated contexts and preserves the source-generated options.

**File:** `src/Aero.Cms.Modules.Modules/Services/AeroModuleBuilder.cs` — add backing store:

```csharp
private readonly List<Type> _jsonContexts = new();

public void AddJsonContext<TContext>() where TContext : JsonSerializerContext
{
    _jsonContexts.Add(typeof(TContext));
    Services.AddSingleton<TContext>();
}

public IReadOnlyList<Type> JsonContexts => _jsonContexts.AsReadOnly();
```

> [!REVIEW]
> If this stays type-based, the startup code must decide which constructor/options to use. That can accidentally discard options encoded in `[JsonSourceGenerationOptions]`. A registration shape like `AddJsonContext(IJsonTypeInfoResolver resolver)` or `AddJsonContext(JsonSerializerContext context)` aligns better with `TypeInfoResolverChain`.

**At startup** (in `ModuleOrchestrationExtensions` or `AeroWebAppExtensions`), after all modules are loaded, instantiate all registered contexts and rebuild the composite resolver, then update the Marten `StoreOptions`:

```csharp
// After modules are configured
var contexts = moduleGraph.GetRegisteredJsonContexts()
    .Select(t => (JsonSerializerContext)ActivatorUtilities.CreateInstance(sp, t));
var resolver = new CompositeJsonTypeInfoResolver(
    new[] { GeneratedBlockJsonContext.Default }.Concat(contexts));
// Update Marten's serializer
storeOptions.UseSystemTextJsonForSerialization(stj => stj.TypeInfoResolver = resolver);
```

> [!REVIEW]
> This example uses `ActivatorUtilities.CreateInstance`, which is runtime construction rather than source-generated discovery. That is acceptable for DI activation, but it should be explicit because the project guidelines generally prefer source generation over reflection-based discovery. The important architectural point is that block discovery can be source-generated while context composition can be normal registration.

#### Step 6 — Update Code References

**Files to update:**
- `ContentItemExtensions.Get<T>()` in `Aero.Cms.Abstractions/Content/ContentItemExtensions.cs`
- `ContentTypeDynamicBlockBridge` in `Aero.Cms.Core/Blocks/Dynamic/ContentTypeDynamicBlockBridge.cs`
- Any other `BlockJsonContext.Default.Options` references

Change from `BlockJsonContext.Default.Options` to the runtime composite. The simplest approach: inject the `JsonSerializerOptions` at the point of use, or use a static holder that gets populated at startup:

> [!REVIEW]
> Prefer injection over a static holder. A mutable static `AeroJson.Options` makes tests, parallel hosts, design-time tooling, and module isolation more brittle. If `ContentItemExtensions.Get<T>()` cannot receive DI directly, consider adding overloads that accept `JsonSerializerOptions`/`JsonSerializerContext`, and keep the existing overload as a compatibility shim.

```csharp
// Static holder populated at app startup
public static class AeroJson
{
    public static JsonSerializerOptions Options { get; set; } = null!; // set during startup
}
```

> [!REVIEW]
> If a static compatibility shim is unavoidable, make it defensive: initialize it to the current `BlockJsonContext.Default.Options`, expose a single startup-time configure method, and fail with a useful error if someone tries to mutate it after first use.

Then `ContentItemExtensions.Get<T>()` uses `AeroJson.Options` instead of `BlockJsonContext.Default.Options`.

### Project Restructuring

No new projects needed. Changes touch existing projects:

| Project | Changes |
|---------|---------|
| `Aero.Cms.SourceGenerators` | Prefer emitting or scaffolding module-owned context declarations; only emit full `GetTypeInfo` + factory methods if the advanced fallback is proven |
| `Aero.Cms.Abstractions` | Keep `BlockJsonContext.cs` as the core/first-party context during migration; strip only after equivalent coverage exists |
| `Aero.Cms.Core` | Add JSON context/resolver composition infrastructure; avoid static `AeroJson` unless needed as a compatibility bridge |
| `Aero.Cms.Generated.Json` | Update `GeneratedMartenConfiguration.cs` |
| `Aero.Modular` | `IAeroModuleBuilder.AddJsonContext<T>()` |
| `Aero.Cms.Modules.Modules` | `AeroModuleBuilder` implementation |
| `Aero.Cms.Web.Core` (or Modules) | Startup wiring — instantiate contexts, build composite |

### Estimated Effort

| Step | Complexity | Files |
|------|-----------|-------|
| Step 1: Module-owned context declaration/registration | Medium | source generator + module template/registration |
| Advanced fallback: full GetTypeInfo emission | **Very High** (serializer-contract generator) | 1 source file modified plus broad serialization tests |
| Step 2: Clean BlockJsonContext | Low | 1 file stripped |
| Step 3: Composite resolver | Low | 1 new file |
| Step 4: Update GeneratedMartenConfiguration | Low | 1 file modified |
| Step 5: Module registration API | Medium | 2 files modified |
| Step 6: Update code references | Medium | 3-5 files modified |
| Startup wiring | Medium | 1-2 files modified |
| **Total** | **~400 lines changed across 10 files** | |

### What Not to Change

- `BlockBase` polymorphic serialization (`[JsonDerivedType]`) — already handled correctly by source-generated `BlockBase.Polymorphic.g.cs`
- Block discovery and registration (`[Module]`, `IAeroModuleBuilder`) — already correct
- Marten's `UseSystemTextJsonForSerialization` — already correct, just plugging in the right resolver

---

## Current Tactical State

The content type implementation added `ContentEmbedBlock` and `ContentItem` to the hand-maintained `BlockJsonContext` to make them work **now**. This is acceptable because:

1. These types are in `Aero.Cms.Abstractions` (core, not a module) — they won't change often
2. All first-party modules currently add types the same way
3. The strategic fix above is needed **before** third-party NuGet packages ship

**When we return to this, the first step is:**
1. Read this doc
2. Read `BlockRendererGenerator.cs` lines 167-185 and 755-771
3. Prototype module-owned `JsonSerializerContext` registration and resolver-chain composition
4. Add tests proving a module block can serialize/deserialize without editing `BlockJsonContext.cs`
5. Only then decide whether full manual `GetTypeInfo()` emission is still needed

> [!REVIEW]
> The strongest first test is a fake external/module package that defines `FooBlock`, ships `FooModuleJsonContext.Default`, registers it through `IAeroModuleBuilder`, and verifies both direct `Deserialize<FooBlock>()` and Marten block persistence work without changing `BlockJsonContext.cs`.

**Related files:**
- `src/Aero.Cms.SourceGenerators/BlockRendererGenerator.cs` (lines 167-185, 755-771)
- `src/Aero.Cms.Abstractions/Blocks/Serialization/BlockJsonContext.cs`
- `src/Aero.Cms.Generated.Json/GeneratedMartenConfiguration.cs`
- `src/Aero.Modular/IModuleBuilder.cs` (needs `AddJsonContext<T>()`)
- `src/Aero.Cms.Modules.Modules/Services/AeroModuleBuilder.cs` (needs implementation)
- `src/Aero.Cms.Abstractions/Content/ContentItemExtensions.cs` (needs `AeroJson.Options`)
- `src/Aero.Cms.Core/Blocks/Dynamic/ContentTypeDynamicBlockBridge.cs` (needs `AeroJson.Options`)
