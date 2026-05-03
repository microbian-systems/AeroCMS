# Source Generator Chaining Limitation — The Big Issue

## The Problem

**Third-party modules shipping as NuGet packages cannot add new block types** without modifying the core `BlockJsonContext.cs` file in `Aero.Cms.Abstractions`. This makes the plugin system non-functional for external consumers.

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

## Impact on NuGet Packages

If a third party creates `Aero.Cms.Modules.Foo` with a new `FooBlock : BlockBase`:

1. `[BlockMetadata]` and `[JsonDerivedType]` work for polymorphic serialization — the source-generated `BlockBase.Polymorphic.g.cs` picks up all `[JsonDerivedType]` correctly
2. But `BlockJsonContext` has no `[JsonSerializable(typeof(FooBlock))]` — so direct `Deserialize<FooBlock>()` or `Serialize(fooList)` via `BlockJsonContext.Default` will throw `NotSupportedException` at runtime
3. The module's author would need to either:
   - Submit a PR to add their type to `BlockJsonContext.cs` (defeats pluggability)
   - Create their own `FooJsonContext : JsonSerializerContext` and register it manually

## ✋ Deferred — Will Revisit After Content Type System Ships

This issue is **acknowledged and documented** but deferred. The content type system is shipping with the hand-maintained `BlockJsonContext.cs` as a tactical bridge. We'll return to this immediately afterward because this is foundational infrastructure.

---

## Chosen Strategy: Option A — Runtime Composite Resolver with Full GetTypeInfo Source Gen

This is **the only correct approach** for a pluggable CMS. It requires source gen to emit the complete `JsonSerializerContext` implementation (not just `[JsonSerializable]` attributes), bypassing the Roslyn chaining limitation entirely.

### Architecture

```
┌──────────────────────────────────────────────────────────┐
│                  CompositeJsonResolver                    │
│  (runtime assembled, owns TypeInfoResolverChain)          │
│                                                           │
│  Chain:                                                   │
│  1. GeneratedBlockJsonContext (source gen, all blocks)    │
│  2. ModuleAJsonContext (3rd party module)                 │
│  3. ModuleBJsonContext (another module)                   │
│  4. BlockJsonContext (empty fallback, hand-maintained)    │
└───────────────────────┬──────────────────────────────────┘
                        │
            ┌───────────┴───────────┐
            ▼                       ▼
    JsonSerializer.Serialize()    Marten storage
```

### Step-by-step Plan

#### Step 1 — Source Generator: Emit `GetTypeInfo` Implementation (~150 lines changed)

**File:** `src/Aero.Cms.SourceGenerators/BlockRendererGenerator.cs`

The existing `RenderGeneratedContext()` emits `[JsonSerializable]` attributes but relies on STJ's generator to produce the implementation. We change this to emit a **complete, self-contained `JsonSerializerContext`**:

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

The key insight: `JsonMetadataServices` has factory methods for all primitive, object, and collection types. We generate one factory method per discovered type. This is what STJ's source generator does internally — we just do it ourselves.

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

#### Step 5 — Module Registration API

**File:** `src/Aero.Modular/IModuleBuilder.cs`

```csharp
void AddJsonContext<TContext>() where TContext : JsonSerializerContext;
```

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

#### Step 6 — Update Code References

**Files to update:**
- `ContentItemExtensions.Get<T>()` in `Aero.Cms.Abstractions/Content/ContentItemExtensions.cs`
- `ContentTypeDynamicBlockBridge` in `Aero.Cms.Core/Blocks/Dynamic/ContentTypeDynamicBlockBridge.cs`
- Any other `BlockJsonContext.Default.Options` references

Change from `BlockJsonContext.Default.Options` to the runtime composite. The simplest approach: inject the `JsonSerializerOptions` at the point of use, or use a static holder that gets populated at startup:

```csharp
// Static holder populated at app startup
public static class AeroJson
{
    public static JsonSerializerOptions Options { get; set; } = null!; // set during startup
}
```

Then `ContentItemExtensions.Get<T>()` uses `AeroJson.Options` instead of `BlockJsonContext.Default.Options`.

### Project Restructuring

No new projects needed. Changes touch existing projects:

| Project | Changes |
|---------|---------|
| `Aero.Cms.SourceGenerators` | `RenderGeneratedContext()` rewrite — emit `GetTypeInfo` + factory methods |
| `Aero.Cms.Abstractions` | Strip `BlockJsonContext.cs` to empty shell |
| `Aero.Cms.Core` | New: `Serialization/CompositeJsonTypeInfoResolver.cs`; New: `AeroJson.cs` static holder |
| `Aero.Cms.Generated.Json` | Update `GeneratedMartenConfiguration.cs` |
| `Aero.Modular` | `IAeroModuleBuilder.AddJsonContext<T>()` |
| `Aero.Cms.Modules.Modules` | `AeroModuleBuilder` implementation |
| `Aero.Cms.Web.Core` (or Modules) | Startup wiring — instantiate contexts, build composite |

### Estimated Effort

| Step | Complexity | Files |
|------|-----------|-------|
| Step 1: Source gen GetTypeInfo emission | **High** (300+ lines generated) | 1 source file modified |
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
3. Rewrite `RenderGeneratedContext()` to emit `GetTypeInfo()` overrides
4. Follow steps 2-6 in order

**Related files:**
- `src/Aero.Cms.SourceGenerators/BlockRendererGenerator.cs` (lines 167-185, 755-771)
- `src/Aero.Cms.Abstractions/Blocks/Serialization/BlockJsonContext.cs`
- `src/Aero.Cms.Generated.Json/GeneratedMartenConfiguration.cs`
- `src/Aero.Modular/IModuleBuilder.cs` (needs `AddJsonContext<T>()`)
- `src/Aero.Cms.Modules.Modules/Services/AeroModuleBuilder.cs` (needs implementation)
- `src/Aero.Cms.Abstractions/Content/ContentItemExtensions.cs` (needs `AeroJson.Options`)
- `src/Aero.Cms.Core/Blocks/Dynamic/ContentTypeDynamicBlockBridge.cs` (needs `AeroJson.Options`)
