# Sable Serialization Problem: Complex Types in SCHEMAFULL Mode

## Problem Statement

Sable (AeroDB's SurrealDB document session in `AeroDB.Sable`) cannot reliably persist entities containing complex POCO properties (e.g., `MediaAttribution`, `NavigationBlock.ResponsiveStyle`, `SeoSettings`) when the SurrealDB table is defined with **SCHEMAFULL** mode.

**Symptoms:**
- Seed/bootstrap fails with `System.InvalidOperationException: Failed to save changes`
- SurrealDB rejects inline object literals because nested `{…}` is parsed as flat field paths under SCHEMAFULL
- The `$data` / CBOR parameterized path cannot handle `System.Text.Json.JsonElement` in the type graph

**Root cause chain:**
1. Sable uses two serialization paths: **inline SurrealQL literal** and **`$data` / CBOR parameterized**
2. The inline literal path builds `CREATE … CONTENT { field: value, … }` by emitting raw SurrealQL
3. The `$data` path sends the full entity via SurrealDB.NET's CBOR serializer
4. Neither path correctly handles nested complex objects end-to-end

---

## The Two Serialization Paths

### Path A: Inline SurrealQL Literal (`TryBuildSurrealQlObjectLiteral`)

Located in `DocumentSession.cs:1516`. Iterates entity properties, calls `ToSurrealQlLiteral()` on each value, and joins them into a SurrealQL object literal:

```surrealql
CREATE media_asset:`1526058882492317697` CONTENT { name: 'example.jpg', attribution: … };
```

`ToSurrealQlLiteral` handles null, string, bool, numeric, DateTime, Guid, Enum, Geometry types, IDictionary, IEnumerable, and record links. Everything else hits a catch-all.

### Path B: `$data` / CBOR Parameterized

When `TryBuildSurrealQlObjectLiteral` returns `false`:

```csharp
$"CREATE {table}:`{insertId}` CONTENT $data"
// parameters: { ["data"] = entity }
```

The full entity object is serialized via Dahomey.Cbor → CBOR bytes → sent as a parameter → SurrealDB parses the complete object. This path avoids the inline SurrealQL parser's `{…}` ambiguity.

---

## Approaches Attempted

### 1. `[JsonIgnore]` on Computed Properties

**Goal:** Prevent computed (non-storable) properties like `IsPubliclyVisible`, `IsActive`, `TotalAmount` from leaking into the serialized document.

**Change:**
```csharp
// PageDocument.cs, DocsPage.cs, PostDocument.cs
[JsonIgnore] public bool IsPubliclyVisible => …;

// ApiKeyDocument.cs
[JsonIgnore] public bool IsActive => …;

// BasketDocument.cs
[JsonIgnore] public decimal TotalAmount => …;
```

Also created Roslyn analyzer `ADB001` to enforce this convention at build time (13 tests).

**Result:** Fixed computed property leakage into the inline literal. Did NOT solve complex type serialization — `MediaAttribution` (with real persisted data) still fell through.

**Status:** ✅ **Retained** — necessary but not sufficient.

---

### 2. Catch-all → Quoted JSON String (Council "Option 1")

**Goal:** Serialize complex POCOs as JSON strings so they don't crash `ToString()`.

**Change:**
```csharp
// DocumentSession.cs:1566, ToSurrealQlLiteral catch-all
_ => $"'{EscapeSurrealQlString(JsonSerializer.Serialize(value))}'"
// Produces: '{"CreatorName":null,"CreatorUrl":null,…}'
```

**Error from SurrealDB:**
```
Expected `none | object` but found `'{"CreatorName":null,…}'`
```

**Root cause:** The schema defines `attribution` as `option<object>` (via source gen). SurrealDB expects either `NONE` or an object value — a quoted string of JSON is type-mismatched.

**Result:** ❌ **Reverted** — type mismatch with schema.

---

### 3. Catch-all → Raw JSON (No Quotes)

**Goal:** Emit valid SurrealQL object syntax by removing the single-quote wrapping.

**Change:**
```csharp
_ => JsonSerializer.Serialize(value)
// Produces: {"CreatorName":null,"CreatorUrl":null,…}
```

The inline literal then becomes:
```surrealql
CREATE media_asset:`…` CONTENT { …, attribution: {"CreatorName":null,…}, … }
```

**Error from SurrealDB:**
```
Found field 'attribution.CreatorName', but no such field exists for table 'media_asset'
```

**Root cause:** Under **SCHEMAFULL**, SurrealDB's inline parser treats nested `{…}` in `CONTENT { … }` as **flat field paths** — `attribution.CreatorName` is interpreted as a direct column on `media_asset`, which doesn't exist. The schema only defines a single `attribution` field of type `option<object>`.

With a parameterized object (Path B), SurrealDB receives the full serialized blob and distinguishes top-level fields from nested object properties. With inline SurrealQL, the parser flattens everything.

**Result:** ❌ **Not viable alone** — SCHEMAFULL rejects nested inline objects.

---

### 4. Type-based Gating → `$data` Path (`IsComplexLiteralType`)

**Goal:** Detect complex entity types at the *type level* and route them to the `$data` / CBOR path instead of the inline literal path.

**Change:** Added an `IsComplexLiteralType` check in `TryBuildSurrealQlObjectLiteral` that returned `false` for entity types with any property whose type wasn't a known primitive.

**Error from SurrealDB:**
```
Failed to deserialize params
```

**Root cause:** The `$data` / CBOR pipeline in SurrealDB.NET v0.10.2 cannot serialize `System.Text.Json.JsonElement`. When the type graph contains a `JsonElement` (e.g., in dynamic documents or polymorphic fields), Dahomey.Cbor throws — and the Rust SurrealDB engine rejects the malformed CBOR at the wire level.

This was proven with 5 reproduction tests in `AeroDB.Analyzers.Tests / JsonElementCborRepro.cs`:
- `JsonElement` → CBOR → wire → SurrealDB → `"Failed to deserialize params"`
- Even wrapping with custom converters failed because Dahomey.Cbor's type discovery can't traverse `JsonElement`'s opaque structure

**Impact:** Broke 27 AeroDB tests that relied on the `$data` path.

**Result:** ❌ **Reverted** — the CBOR pipeline isn't ready for `JsonElement`.

---

### 5. `JsonElementCborConverter` (Safety Net)

**Goal:** Fix the CBOR pipeline so `JsonElement` can be serialized/deserialized through Dahomey.Cbor.

**File:** `AeroDB.Sable/Internals/Cbor/JsonElementCborConverter.cs`

The converter:
- **Write:** Deserializes `JsonElement` back to a .NET object tree via `JsonSerializer.Deserialize<object>()`, then lets Dahomey.Cbor serialize that tree natively. This avoids the `JsonElement` → CBOR dead-end entirely.
- **Read:** Accepts any CBOR value, serializes it to JSON via `System.Text.Json`, and wraps it in a `JsonElement`.

**Registration:** Added to `AeroDBCborOptions.Configure()` alongside existing `DateTimeOffset`, `GeometryPoint`, `GeometryPolygon` converters.

**Result:** ⚠️ **Not yet exercised in production path** — the converter fixes the CBOR pipeline for `JsonElement`, but the current code avoids the `$data` path for INSERT entirely. Kept as a safety net for when `$data` / CBOR is used (e.g., UPDATE, nested queries, future paths).

---

### 6. Value-based Gating (`IsInlineLiteralable`) — CURRENT

**Goal:** Check each property *value* at runtime (not just the type) and route entities with complex POCO values to the `$data` / CBOR path.

**Change:**
```csharp
// New helper — mirrors the pattern match in ToSurrealQlLiteral
private static bool IsInlineLiteralable(object? value)
{
    return value switch
    {
        null => true,
        string => true,
        char => true,
        bool => true,
        GeometryPoint => true,
        GeometryPolygon => true,
        DateTime => true,
        DateTimeOffset => true,
        Guid => true,
        Enum => true,
        System.Collections.IDictionary => true,
        System.Collections.IEnumerable => true,
        byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => true,
        _ => false  // complex POCOs → route to $data
    };
}
```

Modified `TryBuildSurrealQlObjectLiteral` to check each property value:
```csharp
var value = property.GetValue(entity);
if (!IsInlineLiteralable(value))
{
    literal = "";
    return false;  // → $data / CBOR path
}
```

Combined with `String.Equals` support in `ExpressionVisitor.cs`.

**Result:** 🔄 **Pending verification** — seed has not yet been confirmed working. The `$data` path may still fail if the CBOR pipeline encounters `JsonElement` in the type graph, despite the converter being registered.

---

## Why SurrealDB.NET Cannot Handle This Natively

### The Core Tension

SurrealDB.NET v0.10.2 provides two ways to send data to SurrealDB:

| Mechanism | Works with… | Fails with… |
|-----------|-------------|-------------|
| Inline SurrealQL `CONTENT { … }` | Primitives, flat records | Nested objects under SCHEMAFULL |
| Parameterized `CONTENT $data` | Arbitrary POCOs via CBOR | `JsonElement` in type graph |

There is **no single path** that handles both SCHEMAFULL tables AND `JsonElement`-containing entities.

### Why Inline `{…}` Fails Under SCHEMAFULL

SurrealDB's parser interprets `CONTENT { attribution: { CreatorName: null } }` as:
```
SET attribution.CreatorName = null
```

In SCHEMAFULL mode, there is no field `attribution.CreatorName` — only `attribution` (of type `option<object>`). The parser flattens nested `{…}` into dotted field paths.

This behavior is **by design** in SurrealQL — the `CONTENT` clause processes a flat field map. Nested objects are only preserved when sent as serialized blobs (via `$data`, `$param`, or `$value`).

### Why `$data` / CBOR Fails with `JsonElement`

Dahomey.Cbor (the CBOR library used by SurrealDB.NET) does **not** natively understand `System.Text.Json.JsonElement`. When it encounters a `JsonElement` in the object graph:

1. It attempts to discover properties via reflection
2. `JsonElement` is an opaque struct with no discoverable properties
3. Dahomey.Cbor throws a serialization exception
4. SurrealDB.NET sends malformed CBOR to the Rust engine
5. Rust engine responds: `"Failed to deserialize params"`

This is a **fundamental gap** in SurrealDB.NET v0.10.2 — it exposes `JsonElement` in its public API (via `Thing`, `RecordId`, flexible document types) but its CBOR serializer can't round-trip it.

### The Schema Generator Gap (Option 3 — Future)

Both the source generator (`AeroDBDocumentGenerator.cs:461`) and the runtime schema manager (`SchemaManager.cs:671`) produce:

```surrealql
DEFINE FIELD attribution ON media_asset TYPE option<object>;
```

This is correct SurrealDB syntax — `option<object>` means "NONE or any object". But with SCHEMAFULL and inline `CONTENT {…}`, SurrealDB still tries to flatten nested `{…}`.

The **long-term fix** (tracked as "Option 3") is to make schema generators produce explicit nested field definitions:

```surrealql
DEFINE FIELD attribution ON media_asset TYPE option<object>;
DEFINE FIELD attribution.CreatorName ON media_asset TYPE option<string>;
DEFINE FIELD attribution.CreatorUrl ON media_asset TYPE option<string>;
DEFINE FIELD attribution.SourceUrl ON media_asset TYPE option<string>;
DEFINE FIELD attribution.Platform ON media_asset TYPE string;
DEFINE FIELD attribution.MediaType ON media_asset TYPE int;
```

This would allow inline `CONTENT { … }` to work because all nested field paths are explicitly defined in the schema. However:
- Requires the source generator to recursively analyze complex types
- Adds significant complexity to schema generation
- Schema becomes tightly coupled to .NET type structure
- Defeats the purpose of `option<object>` (which is meant to be flexible)

---

## Current State

### Changes in Place

| File | Change | Purpose |
|------|--------|---------|
| `DocumentSession.cs:1566` | `_ => JsonSerializer.Serialize(value)` | Raw JSON for complex types |
| `DocumentSession.cs:1537-1542` | `IsInlineLiteralable(value)` check | Route complex POCOs to `$data` |
| `DocumentSession.cs:1577-1595` | `IsInlineLiteralable()` helper | Value-level type detection |
| `ExpressionVisitor.cs:436-439` | Static `String.Equals` | Slug lookup in LINQ |
| `ExpressionVisitor.cs:458-461` | Instance `String.Equals` | Slug lookup in LINQ |
| `JsonElementCborConverter.cs` | CBOR converter for `JsonElement` | Safety net for `$data` path |
| `AeroDBCborOptions.cs:31-33` | Register `JsonElement` converter | Wiring for safety net |
| `MediaAsset.cs` | `MediaAttribution? Attribution` | Complex POCO causing seed failure |

### What's Been Ruled Out

- ❌ Quoted JSON strings → type mismatch (`option<object>` vs string)
- ❌ Raw inline JSON → SCHEMAFULL flattens nested `{…}`
- ❌ Type-based gating + `$data` → CBOR breaks on `JsonElement`
- ❌ Fixing SurrealDB.NET internals → v0.10.2 is a NuGet dependency

### What's Unverified

- 🔄 Value-based gating + `$data` → CBOR path may still encounter `JsonElement`
- 🔄 Seed operation after all fixes → not yet confirmed

---

## Resolution Strategy

### Short-term (Current Work)

Route entities with complex types to `$data` / CBOR path, relying on `JsonElementCborConverter` to handle any `JsonElement` instances in the type graph.

### Medium-term

Verify and fix the `$data` / CBOR path end-to-end:
1. Ensure `JsonElementCborConverter` is properly wired for all SurrealDB client types (HTTP, WS, embedded)
2. Add integration tests for complex type round-tripping through `$data`
3. Run the full AeroDB test suite (1935 tests) with the current changes

### Long-term (Option 3)

Fix schema generators to produce explicit nested field definitions, allowing inline `CONTENT {…}` to work natively with SCHEMAFULL. This removes the dependency on the fragile `$data` / CBOR path entirely.

---

## Key Files

- `AeroDB/src/AeroDB.Sable/DocumentSession.cs` — `TryBuildSurrealQlObjectLiteral` (line 1516), `ToSurrealQlLiteral` (line 1552), `IsInlineLiteralable` (line 1577)
- `AeroDB/src/AeroDB.Sable/Linq/ExpressionVisitor.cs` — `String.Equals` handling (line 436, 458)
- `AeroDB/src/AeroDB.Sable/Internals/Cbor/JsonElementCborConverter.cs` — CBOR converter for `JsonElement`
- `AeroDB/src/AeroDB.Sable/Internals/Cbor/AeroDBCborOptions.cs` — CBOR converter registration
- `src/Aero.Cms.Core.Entities/MediaAsset.cs` — `MediaAttribution? Attribution` property
- `AeroDB/tests/AeroDB.Analyzers.Tests/JsonElementCborRepro.cs` — 5 tests proving CBOR + `JsonElement` is broken
- `AeroDB/tests/AeroDB.Analyzers.Tests/ComputedPropertyAnalyzerTests.cs` — 8 ADB001 analyzer tests

---

## Appendix: Error Log Chronology

### Run 1 (21:52 — Original)
```
Expected `none | object` but found `'{"CreatorName":null,…}'`
```
→ Catch-all produced quoted string; schema expected object type.

### Run 2 (21:53 — After catch-all → JSON string)
```
Expected `none | object` but found `'{"CreatorName":null,…}'`
```
→ Same error — catch-all still produced quoted string (same code).

### Run 3 (22:30 — After catch-all → raw JSON + String.Equals)
```
Found field 'attribution.CreatorName', but no such field exists for table 'media_asset'
```
→ Raw JSON parsed as SurrealQL object, but SCHEMAFULL flattened nested `{…}` into field paths.

### Run 4 (after IsInlineLiteralable gating)
→ Pending.
