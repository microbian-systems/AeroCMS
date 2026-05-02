# Islands Architecture — HTMX-Driven Interactive Blocks

> **Status:** Proposed  
> **Relates to:** `feature-improvements.md` §3 (Unified Islands Architecture), §5 (Resilient Block Rendering)  
> **Core concept:** Each CMS block becomes a self-contained interactive "island" — rendered server-side, swapped client-side via HTMX, with zero JavaScript framework dependency for interactivity.

---

## 1. Concept

The Islands Architecture treats individual CMS blocks as independent interactive units. On initial page load, the server renders all blocks to static HTML (as it does today). For subsequent interactions, only the specific block responsible for that interaction re-renders on the server and HTMX swaps its HTML fragment into the DOM.

This avoids:
- Shipping a full SPA framework for admin-level interactivity
- Duplicating rendering logic between headless JSON APIs and HTML views
- Full-page postbacks for small UI changes (e.g., toggling a pricing table from monthly to annual)

---

## 2. Architecture Overview

```
Browser                                Server
  │                                      │
  │  Initial page load                   │
  │  ─────────────────────────────────►  │
  │  ◄─────────────────────────────────  │  Full rendered HTML (all blocks)
  │                                      │
  │  User interacts with block           │
  │  (HTMX sends HX-Request)             │
  │  ─────────────────────────────────►  │
  │  GET /blocks/{id}/render             │
  │                                      │  BlockService.GetBlock(id)
  │                                      │  CmsBlockHtmlRenderer.RenderAsync(block)
  │                                      │  BlockRenderer.razor → concrete renderer
  │                                      │
  │  ◄─────────────────────────────────  │  HTML fragment (no layout wrapper)
  │  HTMX swaps fragment into place      │
```

### Key property: each island is independently loadable, cacheable, and fail-able.

---

## 3. HTMX Detection

The existing `BlockRenderContext` already carries HTMX fields that are currently unused:

```csharp
// src/Aero.Cms.Shared/Blocks/Rendering/BlockRenderContext.cs
public sealed record BlockRenderContext(
    NavigationDetail? Navigation = null,
    bool IsPreview = false,
    bool IsHtmxRequest = false,   // ← available, not yet populated
    string? HtmxTarget = null,    // ← available, not yet populated
    CultureInfo? Culture = null);
```

### Middleware / Endpoint Filter

An `IEndpointFilter` or middleware checks for the `HX-Request` header and hydrates the context:

```csharp
public class HtmxContextFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        if (ctx.HttpContext.Request.Headers.ContainsKey("HX-Request"))
        {
            ctx.HttpContext.Items["IsHtmxRequest"] = true;
            ctx.HttpContext.Items["HtmxTarget"] =
                ctx.HttpContext.Request.Headers["HX-Target"].FirstOrDefault();
        }
        return await next(ctx);
    }
}
```

This activates the `IsHtmxRequest` and `HtmxTarget` fields so renderers can adjust their output.

---

## 4. Island Endpoint Pattern

Each module that wants island interactivity registers a minimal API endpoint:

### Base pattern

```csharp
public static IEndpointRouteBuilder MapBlockIslandEndpoints(
    this IEndpointRouteBuilder routes)
{
    routes.MapGet("/blocks/{blockId:long}/render", async (
        long blockId,
        IBlockService blockService,
        CmsBlockHtmlRenderer renderer,
        HttpContext ctx) =>
    {
        var block = await blockService.GetBlockAsync(blockId);
        if (block is null) return Results.NotFound();

        var context = new BlockRenderContext(
            IsHtmxRequest: ctx.Items["IsHtmxRequest"] as bool? ?? false,
            IsPreview: true
        );

        var html = await renderer.RenderAsync(block, context);
        return Results.Content(html, "text/html");
    });

    return routes;
}
```

### Parameterized endpoint (for blocks with interactive state)

```csharp
routes.MapGet("/blocks/{blockId:long}/mode/{mode}", async (
    long blockId,
    string mode,
    IBlockService blockService,
    CmsBlockHtmlRenderer renderer) =>
{
    var block = await blockService.GetBlockAsync(blockId);
    if (block is null) return Results.NotFound();

    // Mutate the block's rendering state before rendering
    if (block is AeroPricingBlock pricing)
        pricing.SelectedMode = mode;

    var html = await renderer.RenderAsync(block);
    return Results.Content(html, "text/html");
});
```

### Response contract

- **Status:** `200 OK` with `Content-Type: text/html`
- **Body:** Pure HTML fragment — no `<html>`, `<body>`, or layout wrapper
- **Headers:** HTMX response headers (`HX-Push-Url`, `HX-Replace-Url`, `HX-Refresh`, etc.) can be added per block as needed

---

## 5. Writing an HTMX-Aware Block Renderer

Block renderers are Razor components. To make one HTMX-aware, add `hx-*` attributes conditionally.

### Pattern

```razor
@* AeroPricingRenderer.razor *@
@inherits CmsBlockComponent<AeroPricingBlock>

@if (Context.IsHtmxRequest)
{
    @* Islands mode: the block tells HTMX how to refresh itself *@
    <div hx-get="/blocks/@Block.Id/mode/monthly"
         hx-trigger="click from:#monthly-btn"
         hx-target="this"
         hx-swap="outerHTML"
         class="pricing-island">

        <button id="monthly-btn" class="btn">Monthly</button>
        <button id="annual-btn"
                hx-get="/blocks/@Block.Id/mode/annual"
                hx-target="closest .pricing-island"
                hx-swap="outerHTML">Annual</button>

        @RenderPricingCards(Block.Items, "monthly")
    </div>
}
else
{
    @* Initial render: full server-rendered HTML *@
    <div class="pricing-static">
        @RenderPricingCards(Block.Items, Block.SelectedMode ?? "monthly")
    </div>
}
```

### Guards

- Use `Context.IsHtmxRequest` inside the renderer to determine whether to emit HTMX attributes.
- On initial page load (`IsHtmxRequest == false`), render the block as static HTML.
- When HTMX re-renders the block (`IsHtmxRequest == true`), include the HTMX trigger attributes so the island remains interactive after swap.

This prevents "HTMX inception" — nested HTMX attributes that pile up across multiple swaps.

---

## 6. Integration with Existing Pipeline

The existing rendering pipeline requires **zero structural changes** for islands to work:

| Component | Role in Islands | Change Required |
|---|---|---|
| `BlockRenderContext` | Carries `IsHtmxRequest`, `HtmxTarget` | Already defined, just needs population |
| `CmsBlockHtmlRenderer.RenderAsync()` | Renders a single block to HTML string | None — already single-block capable |
| `BlockRenderer.razor` | Entry point, resolves adapter, wraps in `ErrorBoundary` | None — renders a single block correctly |
| Source-generated `ICmsBlockRenderAdapter` | Opens the correct Razor component for a block type | None |
| `IBlockService` / Marten | Loads block data from Postgres | None |
| `ErrorBoundary` | Catches render failures per island | None — already wraps each block independently |

### What you add (not change)

| Addition | Purpose |
|---|---|
| `HtmxContextFilter` (endpoint filter) | Populates `BlockRenderContext.IsHtmxRequest` from `HX-Request` header |
| `MapBlockIslandEndpoints()` extension | Registers `GET /blocks/{id}/render` and parameterized variants |
| HTMX attributes in individual block renderers | Opt-in interactivity per block type |

---

## 7. Adding a New Island Block

1. **Create the block model** with `[BlockMetadata("my_block", "My Block")]` in `Blocks/Common/`
2. **Create the renderer** with `[CmsBlockRenderer(typeof(MyBlock))]` in `Blocks/Rendering/`
3. **Source generator** auto-registers everything — no manual wiring
4. **Add HTMX attributes** in the renderer for the interactive parts
5. **Register island endpoints** in the module if dynamic re-rendering is needed

```csharp
// Step 1: Block model
[BlockMetadata("live_counter", "Live Counter", Category = "Interactive")]
public sealed class LiveCounterBlock : BlockBase
{
    public string Label { get; set; } = "";
    public int InitialValue { get; set; } = 0;
    public override string BlockType => "live_counter";
    public override IHtmlContent Accept(IBlockVisitor v) => v.Visit(this);
}

// Step 2: Renderer marker
[CmsBlockRenderer(typeof(LiveCounterBlock))]
public partial class LiveCounterRenderer;

// Step 3: Registration handled by source generator — done.

// Step 4: Razor renderer with HTMX interactivity
// See pattern in §5 above.

// Step 5: Island endpoint
app.MapGet("/blocks/{blockId:long}/increment", async (
    long blockId, IBlockService svc, CmsBlockHtmlRenderer renderer) =>
{
    var block = await svc.GetBlockAsync<LiveCounterBlock>(blockId);
    if (block is null) return Results.NotFound();
    block.InitialValue++;
    var html = await renderer.RenderAsync(block);
    return Results.Content(html, "text/html");
});
```

---

## 8. Performance & Resilience

### Circuit Breaker per Island

Each island endpoint is wrapped in a Polly circuit breaker (from `feature-improvements.md` §5):

```csharp
app.MapGet("/blocks/{blockId:long}/render", async (...) => { ... })
   .AddPolicyHandler(Policy<HttpResult>
       .Handle<Exception>()
       .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));
```

If an island fails (e.g., external API dependency is down), only that island renders a placeholder — the rest of the page stays intact.

### Caching Strategy

| Cache Level | Key | TTL |
|---|---|---|
| Rendered HTML (FusionCache) | `block:{blockId}:render` | Varies per block type |
| Marten document | Entity ID | Long-lived (Marten identity map) |

### Graceful Degradation

```razor
<ErrorBoundary>
    <BlockRenderer Block="@block" Context="@context" />
</ErrorBoundary>
```

If a block renderer throws, the `ErrorBoundary` renders a fallback UI for just that island. The parent page and all other islands remain fully functional.

---

## 9. Relation to Headless API

The same block data can serve both headless JSON consumers and HTMX island consumers:

| Request | Consumer | Response |
|---|---|---|
| `GET /api/v1/pages/{id}` with `Accept: application/json` | Headless/SPA | JSON |
| `GET /blocks/{id}/render` with `HX-Request: true` | HTMX island | HTML fragment |

No logic duplication — both paths go through `IBlockService` → Marten, then diverge at serialization (JSON) vs rendering (HTML).

This follows the **"Partial-Aware" rendering strategy** from `feature-improvements.md` §3.

---

### 10. Future: Event-Driven Island Invalidation

- Marten event sourcing (per `feature-improvements.md` §4) publishes an event when a block is updated
- A background service invalidates FusionCache entries for affected block IDs
- Islands with active HTMX connections could receive server-sent events (SSE) to trigger automatic refresh via `hx-trigger="sse:block-updated"`:

```html
<div hx-get="/blocks/@Block.Id/render"
     hx-trigger="sse:block-@Block.Id"
     hx-target="this"
     hx-swap="outerHTML">
```

---

## 11. Client-Side Interactivity (The "Local" Layer)

While HTMX handles data-driven state changes, AeroCMS utilizes **Alpine.js** and **RxJS** for immediate, client-side interactivity that does not require a server round-trip.

- **Rule of Thumb:** "Server for Data (HTMX), Alpine/RxJS for UI."
- **Alpine.js:** Used for local UI state (dropdowns, mobile menus, tab switching, simple transitions).
- **RxJS:** Used for complex client-side event streams, debounced inputs, and cross-island coordination without server overhead.

This "Hybrid" approach ensures the UI feels "alive" and instantaneous for trivial actions while maintaining the power of server-side rendering for content.

---

## 12. Drawbacks & Mitigations

### 1. Flash of Unstyled/Old Content (FOUC)
When HTMX swaps a fragment, there can be a visual flicker or layout jump.
- **Mitigation:** Use HTMX CSS transitions (`.htmx-swapping`) and `hx-indicator` to provide visual continuity.

### 2. Accessibility (A11y) Complexity
Dynamic DOM swaps can disorient screen readers and break keyboard focus.
- **Mitigation:** Rigorous use of `aria-live` regions and manual focus management (using Alpine.js to restore focus) after an `htmx:afterSwap` event.

### 3. Increased Server Pressure
The server must execute the Razor rendering engine for every interaction.
- **Mitigation:** The **Triple Threat caching strategy** (FusionCache) is mandatory. Rendered HTML fragments must be cached at the L1/L2 level to prevent CPU exhaustion.

### 4. Lost Ephemeral State
Swapping a whole island resets local state like scroll position or cursor focus.
- **Mitigation:** Use `hx-preserve` for specific elements and leverage Alpine.js to persist non-data UI state across swaps.

---

## See Also


- [`feature-improvements.md`](feature-improvements.md) §3 — Unified "Islands" Architecture
- [`feature-improvements.md`](feature-improvements.md) §5 — Resilient Block Rendering (Circuit Breaker)
- [`source-generator-block-renderer.md`](source-generator-block-renderer.md) — How the block renderer source generator works
- Existing HTMX usage in `src/Aero.Cms.Modules.Blog/Areas/Blog/Pages/_BlogPostsList.cshtml`
- `BlockRenderContext` at `src/Aero.Cms.Shared/Blocks/Rendering/BlockRenderContext.cs`
- `CmsBlockHtmlRenderer` at `src/Aero.Cms.Web.Core/Blocks/Rendering/CmsBlockHtmlRenderer.cs`
- `BlockRenderer.razor` at `src/Aero.Cms.Shared/Blocks/Rendering/BlockRenderer.razor`
