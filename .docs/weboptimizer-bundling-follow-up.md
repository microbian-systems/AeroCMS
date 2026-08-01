# WebOptimizer Bundling Follow-up

**Status:** Deferred investigation  
**Related source:** `src/Aero.Cms.Modules.WebOptimizer/WebOptimizerModule.cs`

## Preserved Source Notes

The following TODO and references were removed from the source during the XML
documentation cleanup:

> Configure WebOptimizer more granularly with bundles and related options.

- <https://weboptimizer.azurewebsites.net/>
- <https://www.nuget.org/packages/LigerShark.WebOptimizer.Core/>

These links are preserved as historical references. Their continued accuracy
has not been evaluated as part of this documentation pass.

## Current Behavior

`WebOptimizerModule` registers WebOptimizer services. In Production it adds the
provider's all-file CSS and JavaScript minification assets; in other
environments it registers an empty asset pipeline.

The module does not currently:

- define bundle output routes or ordered source lists;
- activate WebOptimizer middleware in a host;
- configure HTML minification, Sass, source maps, CDN rewriting, or custom file
  providers;
- configure Content Security Policy, nonces, or subresource integrity; or
- introduce an npm dependency.

Service registration alone does not cause runtime transformations. A host must
activate the corresponding middleware in the correct location for the assets
to be served.

## Decisions Still Needed

Before implementing granular bundles:

1. Inventory the scripts and styles that each host and module requires,
   including their ordering constraints.
2. Define stable bundle routes and ownership boundaries so feature modules do
   not silently overwrite one another.
3. Decide whether bundling, minification, and caching differ between
   Development, Staging, and Production.
4. Identify the host responsible for middleware activation and verify its
   placement relative to static-file handling.
5. Reconcile runtime processing with publish-time assets, CDN usage, cache
   invalidation, and the repository's no-npm constraint.
6. Add integration tests for bundle routing, source ordering, cache behavior,
   missing inputs, and non-Production behavior.

## Non-goal

This note does not select a bundle layout or authorize host middleware changes.
Those choices should follow an inventory of the current asset graph.
