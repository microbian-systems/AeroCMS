The plan is very solid overall — it reads like something written by someone who has shipped a real editor before and knows where the bodies are buried. A few observations, both strengths and gaps worth addressing:

## Strong Foundations

**Command + Memento for undo/redo** is exactly right. Memento-only is a snapshot tax; command-only makes inverses brittle. The coalescing note (pointer moves, typing batched per gesture) is the right detail to call out early.

**`ICompositionPolicy` as a central validator** is the correct SOLID answer. Putting validation only in the UI is a classic mistake this plan explicitly avoids.

**Logical CSS properties first** (block-start/end, inline-start/end) is the right call for RTL. Retrofitting logical properties after physical ones are entrenched is painful — good that it's Phase A, not an afterthought.

**Phase B Sortable audit before scaling** — this is wise. NeoUI Sortable's behavior in nested + named drop zone scenarios is the highest-risk technical unknown in the whole plan. Proving it early is exactly right.

---

## Genuine Concerns

**`ICompositionCapabilities` on every node could become expensive.** If you're doing real-time drop-target highlighting across a large tree, evaluating `AllowedChildKinds`/`AllowedParentKinds` per pointer event on every potential target gets costly. You'll want a precomputed compatibility matrix or cached lookup keyed by `NeoPageNodeKind` pair — not a per-instance interface call in a hot path.

**`EditorCanvasState` ownership is underspecified.** The plan mentions it but doesn't clarify whether it lives in a Blazor cascading parameter, a scoped service, or an Orleans grain. Given you're using Blazor Interactive Server for the manager, a scoped `EditorCanvasService` is probably right — but that needs to be pinned before Phase A commands start taking a dependency on it, or you'll get constructor hell.

**The `INeoNodeBlockMapper` bidirectional contract hides a real impedance mismatch.** Canned blocks were designed around the `EditorBlock` property bag. Round-tripping them through `NeoPageNode` and back is going to surface lossy mappings — fields that exist on one side but not the other. The plan acknowledges this with the adapter pattern for existing blocks, but there's no explicit strategy for what happens when `ToBlock(ToNode(block)) != block`. You need a documented invariant and a test that enforces it, otherwise you'll have subtle data loss bugs that only appear after a save-reload cycle.

**Phase E (canned block slots) sequencing is optimistic.** Slots require the renderer to know about named regions, the canvas to render drop zones inside an otherwise atomic block, and the composition policy to understand slot-scoped cardinality rules. That's a significant cross-cutting change. If Phase D (mixed canvas + persistence) takes longer than expected — and mixed-canvas persistence problems usually do — Phase E will feel like a ground-up rewrite of work you just stabilized. Consider whether slots should be scoped to just one block type as a spike during Phase C rather than a whole phase after D.

**`CollectionEditor<T>` is listed as a shared control but will carry significant complexity.** Drag-reorder inside a modal, inside a canvas that also has drag-reorder, with NeoUI Sortable in play at both levels, is a pointer-event conflict waiting to happen. This deserves its own risk row in the table.

**The Playwright test matrix is correct but the scope is underestimated.** LTR + RTL × desktop/tablet/mobile × the listed interaction types is a meaningful matrix to maintain. Without a dedicated test data seeding strategy and deterministic canvas state, these tests will be flaky. The plan calls for seed data but doesn't specify how Playwright tests get a clean, known canvas state before each test — that needs to be decided in Phase A alongside the command/history infrastructure.

---

## Minor Gaps

- **`EditorCapabilitySet` is referenced but never defined.** Even a rough enumeration of capability flags (HasTypography, HasMedia, HasLayout, etc.) should appear in the plan so Phase C implementors aren't guessing.
- **The `CssLength` type is implied but not specified.** You'll want a discriminated value type covering `px`, `%`, `rem`, `em`, `vw`, `vh`, `auto`, and `calc()` early, since it touches everything from spacing to responsive breakpoints.
- **No mention of how the canvas communicates pointer/selection events back to Blazor.** The plan correctly says Blazor owns durable state and TypeScript/Alpine owns transient interaction, but the boundary protocol (JS interop calls? CustomEvents? a message bus?) isn't described. That seam will be touched by every Phase B and F task.

---

## Bottom Line

The phasing is disciplined, the contracts are well-scoped, and calling out RTL/localization as Phase A rather than Phase F is genuinely uncommon and correct. The biggest real risks are the Sortable nested drop zone behavior (already flagged), the `EditorCanvasState` ownership ambiguity, and the mapper round-trip invariant. Address those three concretely before you write Phase A code and the rest of the plan will hold up well.