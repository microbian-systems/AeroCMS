# AeroCMS Tentative Future Features

## Status

**Tentative ideas, not an accepted roadmap or implementation commitment**

This file keeps post-alpha and post-1.0 experiments separate from the current
architecture. An item must receive its own evidence, threat analysis,
architecture decision, implementation plan, and acceptance criteria before it
becomes scheduled work.

Updated: 2026-07-24

## Razor and SharpTS Interoperability

### Intent

Explore ways for deployment-owned Razor or `.cshtml` code to invoke SharpTS, or
for a compiled SharpTS implementation to participate behind a stable .NET
interface.

This is separate from the initial page-renderer model:

- `aero.composition` may contain Scriban, SharpTS, and HTMX fragments;
- `aero.scriban`, `aero.sharpts`, and `aero.htmx` are code-first whole-page
  renderer choices; and
- `Page.cshtml` remains the deployment-owned shell that dispatches to those
  renderers.

### Plausible future shapes

1. A precompiled Razor Page, partial view, or ViewComponent injects an
   AeroCMS-owned SharpTS rendering service and renders its validated result.
2. A deployment- or publication-compiled SharpTS assembly implements a stable
   interface known to Razor at build time and is resolved at runtime through
   the page-renderer registry.
3. A registered Blazor component uses a SharpTS-backed service and returns
   statically rendered component output where that model is appropriate.
4. Trusted SharpTS invokes an Aero-owned Razor template-rendering capability
   implemented with RazorEngineCore or another managed Razor engine.

The Razor view does not necessarily need recompilation when a SharpTS artifact
changes if the Razor assembly depends only on a stable host interface. Artifact
compatibility, dependency injection, lifetime, unloading, and cache
invalidation would still need to be proven.

### Important boundary

SharpTS does not parse or compile Razor syntax. Allowing CMS users to create
arbitrary `.cshtml` inside the deployment-owned `Page.cshtml` would require
runtime Razor compilation or another dynamic Razor host. That is trusted
server-side code with broad .NET authority and is outside the alpha and initial
1.0 scope.

Precompiled, allowlisted partial views or ViewComponents are a safer and
separate feature from user-authored runtime Razor. A Razor partial or
ViewComponent belongs to the Razor rendering system; a `RenderFragment` belongs
to Blazor.

There will be no `.tshtml` language, mixed Razor/TypeScript parser, or custom
Razor compiler backend. SharpTS pages use the AeroCMS `html` tagged-template
helper. Razor templates, if added, remain ordinary Razor plus C# and are a
separate renderer or registered capability.

### SharpTS invocation boundary

SharpTS can invoke managed .NET APIs, but scripts must not receive
RazorEngineCore's raw compile API. AeroCMS should expose a narrow wrapper such
as:

```csharp
public interface IAeroRazorTemplateRenderer
{
    Task<Result<string>> RenderAsync(
        string templateKey,
        AeroRazorTemplateModel model,
        CancellationToken cancellationToken = default);
}
```

The wrapper resolves an allowlisted, deployment-owned or previously published
template by key. A SharpTS script may supply the model but cannot supply Razor
source, assembly references, compiler options, filesystem paths, or services.
This preserves the option for interpreted and compiled SharpTS to call the same
stable .NET capability without turning Razor compilation into a privilege
escalation path.

### Prerequisites for investigation

- a stable `IPageRenderer` contract and renderer registry;
- a proven SharpTS hosting and isolation model;
- versioned compiled artifacts and deterministic cache invalidation;
- explicit allowlists for Razor components and SharpTS host capabilities;
- output validation and sanitization;
- bounded execution, diagnostics, and observability; and
- tests proving artifact replacement, service lifetimes, unloadability, and
  failure behavior.

### Current decision

Defer Razor/SharpTS interoperability until after the standalone SharpTS page and
fragment renderer is stable. Do not add runtime Razor compilation or
user-authored `.cshtml` to the current implementation phases.

Related design:
[SharpTS TypeScript Dynamic Pages and Elements](sharpts-typescript-dynamic-rendering.md).
