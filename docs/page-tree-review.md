Yes, this is accurate as a target architecture, and it lines up well with how serious modern visual editors are actually built. The core diagnosis is right: `LayoutRegions` (fixed zones, each holding a list of placements) is a rendering-era concept from server-templated CMS systems where the page shape was baked into the theme. A WYSIWYG editor needs the opposite — arbitrary nesting, arbitrary composition, no hardcoded slots at the page level — so collapsing everything into a single recursive `PageNode` tree, with `PageDocument` holding only routing/SEO metadata and pointers to draft/published composition documents, is the correct shape. The Webflow/GrapesJS/Notion/Portable-Text comparisons are fair: all of those systems treat the document body as a typed node tree rather than a manifest of regions, and publish-by-pointer-swap (rather than mutating a single record in place) is the same pattern Contentful and Sanity use for draft/publish separation. None of that needs correcting.

A few things in the schema are worth pinning down before you lock it in, though, because they're the kind of decisions that are cheap to make now and expensive to retrofit later.

The localization story is ambiguous as written. `Culture` lives on both `PageDocument` and `PageCompositionDocument`, which implies a full separate tree per locale — but then `PageNode` also carries "Localization metadata," which implies the opposite: one shared structural tree with per-node translated fields. Those are genuinely different architectures with different tradeoffs (duplicate-tree-per-locale gives translators full structural freedom but means layout changes have to be propagated by hand across languages; shared-tree-with-localized-fields keeps structure in sync automatically but constrains translators to the same layout). Pick one explicitly rather than letting both live in the schema at once.

The versioning model is sound at the publish level (pointer swap) but underspecified for history. If `Version` increments on every save, where do old versions go — are they archived `PageCompositionDocument`s queryable by `PageId`, or does only Draft+Published ever exist with everything else discarded? If editors are going to want rollback or an audit trail, that needs a deliberate retention policy. Separately, since this is Marten, be careful that your own `Version` field doesn't get conflated with Marten's built-in optimistic-concurrency version tracking — those are two different concerns (content revision number vs. write-conflict guard), and mixing them up is a common source of confusing publish bugs.

Baking `ResponsiveStyle` directly onto each node is fine if this CMS is purely a web WYSIWYG renderer, but it's a real coupling risk if there's any ambition toward true headless/multi-channel delivery later (app, email, voice, whatever). Worth noting that GrapesJS, despite the comparison, generally keeps a separation between the component tree and a CSS rule layer rather than inlining style per node — so if multi-channel is even a maybe, it's worth deciding now whether style belongs in the content node or in a parallel "presentation" layer keyed off `ElementType`/`CatalogId`.

The other gap is query and search. A deeply nested JSON document is great for "load PageCompositionDocument by id, recurse, render" — exactly the public render path you described — but it's weak for anything like "find every page that uses component X" (for impact analysis when a catalog component's schema changes) or full-text search across block content. Marten handles that well, but not by querying inside the nested tree directly; you'd want a secondary flattened projection (Marten's projection support is a natural fit here) rather than treating it as an afterthought once an editor asks "where else is this block used."

Related to that: `CatalogId` implies reusable component definitions, and component definitions evolve. When a catalog component's shape changes, existing `PageNode` instances referencing the old shape need somewhere to land — typically a per-node catalog/schema version stamp so old nodes keep rendering against their original definition until migrated. This is one of the genuinely hard problems in block-based CMS design (Sanity and Builder.io both have to solve it) and it's not in the proposal at all.

The migration plan (steps 1–8) is a standard strangler-fig approach and there's nothing wrong with it — keep `LayoutRegions` as a compatibility adapter, introduce the new model alongside it, convert legacy pages through an adapter, then delete the old path once migrated. That part doesn't need changes.

So: the architectural direction is correct and matches industry practice, but I'd treat localization strategy and the content/style coupling decision as blocking questions to resolve before writing the persisted schema into `ux-refactor.md`, and treat versioning depth, search projections, and catalog schema versioning as things to at least sketch so they don't get discovered mid-implementation.

----- update -----

Mostly agreed, and this is a real improvement over the first draft — the locale decision, the presentation split, and dropping the strangler migration are all the right calls given you're pre-production. There's one thing in here that's being treated as settled when it's actually a fork in the road, and a couple of smaller scoping questions worth nailing down before you write code.

Clarification: AeroCMS already uses Marten event sourcing and an inline
`PageDocumentProjection` for pages. The open decision is not "event sourcing
or not"; it is the granularity of page-composition events. The current page
stream uses coarse snapshot-style events such as `PageContentUpdated` and
`PagePublished`, which is the right direction for editor persistence but still
carries legacy `LayoutRegions` terminology.

The target should keep coarse persisted events and avoid recording every tiny
canvas operation as durable stream noise. The editor can keep local undo/redo
with Command/Memento state, while Marten records meaningful composition save
and publish milestones:

```
PageCompositionDraftSaved
- PageId
- CompositionId
- Culture
- ContentRevision
- Root node snapshot
- Presentation snapshot

PageCompositionPublished
- PageId
- PublishedCompositionId
- PublishedVersion
- Root node snapshot
- Presentation snapshot
```

Marten projections should then derive `PageDocument`, `PageCompositionDocument`,
`PageNodeIndex`, `PageSearchDocument`, and `ComponentUsageIndex` from those
coarse composition events. Inline projections are appropriate for the core
editor/public-read state that must be visible immediately after save. Async
projections are appropriate for search, usage analytics, and rebuildable
secondary indexes where a short lag is acceptable.

Two smaller things. `PublishedPageReadModel` is only worth having as a distinct document if it actually denormalizes something beyond what `PageCompositionDocument.Root` already gives you on a direct lookup by `PublishedCompositionId` — e.g., pre-resolved catalog metadata so the renderer doesn't need a second fetch per node type, or computed routing/sitemap fields. If it ends up being a structural copy of the composition document with no extra resolution, it's a moving part that earns its keep only if you can name what it's optimizing away.

And the locale call — one tree per culture with sync tooling — is the right choice for giving translators real structural freedom, but "tooling to copy/sync structure" is its own feature, not a detail. The hard part isn't the initial copy, it's what happens when the French tree has already diverged (someone added a section, reordered blocks) and the English source structure changes again — that's a diff/merge problem with real UX decisions about what gets auto-applied versus flagged for a human. Worth a short design pass on that specifically before it's assumed to be "tooling."

Last note in your favor: the typed-presentation-values stance isn't just architecturally cleaner, it also closes off a CSS-injection vector you'd otherwise have if arbitrary style strings ever flowed from editor input into rendered pages — worth keeping as a stated rationale, not just a preference.

So: agreed on the page-tree-as-source-of-truth, the presentation split, the
locale model, and dropping the long strangler migration. The architectural
commitment is: keep Marten event sourcing, move page-body persistence from
legacy layout-region snapshots toward coarse page-composition snapshot events,
and let projections build the flattened/indexed views the editor and renderer
need.

----- implementation checkpoint, 2026-06-18 -----

The first implementation slice now matches the target direction:

- `PageCompositionDraftSaved` and `PageCompositionPublished` exist as coarse
  Marten events carrying page tree snapshots.
- `PageDocument` remains the routing/publication shell and now stores
  `DraftCompositionId`, `PublishedCompositionId`, and `ContentRevision`.
- `PageCompositionDocument` stores first-class draft/published tree snapshots.
- `PageNodeIndexDocument` provides the first flattened node index projection
  for catalog/component lookup.
- `PageCompositionProjection` is registered inline with the pages module.
- The public page route now loads `PageCompositionDocument` by draft/published
  composition pointer and renders that tree through the Neo SSR node renderer
  before falling back to serialized root/layout data.

Tree-backed save/publish paths no longer generate a synthetic
`NeoCompositionBlock` or fake `LayoutRegions` bridge. `LayoutRegions` remains
only for older block-manifest requests and transitional fallback rendering.
