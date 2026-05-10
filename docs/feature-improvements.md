# AeroCMS Architectural Enhancements & Feature Improvements

This document outlines strategic recommendations to further optimize the AeroCMS platform, focusing on performance, resilience, and developer experience within a .NET 10+ and Native AOT context.

---

## 1. Minimal API Optimization: Sparse Fieldsets & Expansions

To solve the over-fetching/under-fetching problem without the complexity of GraphQL, the Minimal API layer should adopt a deterministic querying pattern.

- **Recommendation**: Implement `?fields=` (Sparse Fieldsets) and `?include=` (Expansions).
- **Mechanism**: 
  - Use a custom `IEndpointFilter` to intercept requests.
  - If `?include=author,tags` is present, the API should leverage Marten's `Include` or `BatchQuery` features to fetch related documents in a single round-trip.
  - Use a Source Generated projection to filter JSON output based on the `?fields` parameter to reduce payload size.

## 2. Flattened Read Models via Marten Async Projections

Querying deep block hierarchies (LayoutRegions → Columns → Blocks) can be computationally expensive during cache misses.

- **Recommendation**: Implement **Async Projections** in Marten to maintain a "Flattened" Read Model of pages.
- **Benefit**: Instead of traversing a complex tree at runtime, the API performs a O(1) primary key lookup on a pre-computed `FlatPageDocument`. This dramatically increases the speed of "cold" requests.

## 3. Unified "Islands" Architecture (HTMX + Minimal APIs)

Avoid duplicating logic between the Headless API (JSON) and the Admin UI (HTML).

- **Recommendation**: Implement a "Partial-Aware" rendering strategy.
- **Mechanism**: If an API request contains the `HX-Request` header, the endpoint returns a **Razor Slice** (HTML fragment) instead of JSON. 
- **Benefit**: Adheres to the DRY principle while allowing high-performance interactivity for the Admin dashboard using the same endpoints as headless consumers.

## 4. Immutable History via Marten Event Sourcing

Move beyond simple version snapshots to a full audit trail.

- **Recommendation**: Transition the Content Lifecycle from document snapshots to **Marten Event Sourcing**.
- **Benefit**: 
  - Provides an immutable audit trail (who, what, when, why).
  - Enables "Time Travel" debugging and previewing.
  - Makes "Scheduled Publishing" more robust by projecting events into the future.

## 5. Resilient Block Rendering (Circuit Breaker Pattern)

Prevent slow or failing external blocks (e.g., a Twitter/X feed) from hanging the entire page rendering pipeline.

- **Recommendation**: Wrap `IBlockRenderer.RenderAsync` calls in a **Polly Circuit Breaker**.
- **Benefit**: Gracefully degrades the UI by rendering a "Placeholder" or "Retry later" state for a specific failing block, ensuring the rest of the page remains responsive and accessible.

## 6. AOT-Safe Scriban Integration

Avoid reflection when passing complex .NET objects to the Scriban templating engine.

- **Recommendation**: Create a **Source Generated Scriban Member Renamer**.
- **Mechanism**: Generate a static mapping of `ContentItem.Fields` to Scriban-compatible `ScriptObject` keys at compile time.
- **Benefit**: Maintains full Native AOT compatibility and increases template rendering performance.

## 7. Semantic "Related Content" via pg_vector

Leverage the existing vector search infrastructure for automated content relationships.

- **Recommendation**: Implement **Automatic Semantic Linking** (RAG-Lite).
- **Mechanism**: 
  - Generate and store embeddings for content items upon publication.
  - Use vector similarity queries in Postgres to automatically populate "Related Content" blocks.
- **Benefit**: Provides high-value "intelligent" features with zero additional infrastructure overhead.

## 8. Observability: OpenTelemetry Trace Propagation

Identify performance bottlenecks in a modular, multi-layered system.

- **Recommendation**: Instrument the `PageReadPipeline` and `IBlockRenderer` implementations with .NET `ActivitySource`.
- **Benefit**: Provides granular distributed tracing, allowing developers to see exactly which module or block is contributing to latency across the cache stack.

---

## 9. Eliminate Reflection in Social Plugs

The `Aero.Social` module currently relies on `MethodInfo.Invoke` and `GetProperty("Result")`, which are incompatible with Native AOT and incur runtime overhead.

- **Recommendation**: Replace `PlugExecutor` with a source-generated **Plug Dispatcher**.
- **Mechanism**: Use a generator to scan for `[Plug]` attributes and produce a static switch statement that calls provider methods directly, bypassing runtime method invocation.

## 10. AOT-Safe Polymorphic Serialization

The `BlockEditingService.DuplicateBlock` currently uses `JsonSerializer` without a context, which is prone to failure in Native AOT when handling derived types.

- **Recommendation**: Refactor all internal serialization/deserialization to use the source-generated `AeroJsonContext`.
- **Benefit**: Ensures that block duplication and polymorphic state management remain functional and performant in Native AOT environments.

## 11. Source-Generated Object Factory

The CMS currently uses `Activator.CreateInstance` for dynamic block creation during editing.

- **Recommendation**: Replace dynamic activation with a **Source-Generated Factory** (e.g., `BlockFactory.Create(string type)`).
- **Benefit**: Eliminates reflection-based activation overhead and ensures every block type is explicitly known and instantiable at compile time.

## 12. Reflection-Free Object Mapping

Generic utility extensions (e.g., `ToDictionary()`) currently use reflection to inspect object properties at runtime.

- **Recommendation**: Replace broad reflection-based mapping with **Source-Generated Mappers**.
- **Mechanism**: Implement mappers at compile time for specific DTOs and entities using the same patterns as the existing JSON source generators.

## 13. Clean up AOT-Incompatible Namespaces

Namespaces such as `System.Reflection.Emit` are present in global usings but are fundamentally incompatible with Native AOT.

- **Recommendation**: Perform a strict audit and removal of `System.Reflection.Emit` and other runtime-codegen namespaces.
- **Benefit**: Prevents accidental re-introduction of AOT-incompatible logic and ensures the project passes strict AOT compiler validation.
