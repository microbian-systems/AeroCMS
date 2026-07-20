# Analytics Markup Rendering Follow-up

**Status:** Deferred investigation  
**Related source:** `src/Aero.Cms.Modules.Analytics/AnalyticsInjectionHook.cs`

## Preserved Source Note

The following TODO was removed from the source during the XML documentation
cleanup and is retained here so the concern is not lost:

> Use view components here. `StringBuilder` is absolutely the wrong way to do
> this.

The wording records the original concern; it is not an approved architectural
decision.

## Current Behavior

`AnalyticsInjectionHook` builds inline analytics markup with a
`StringBuilder`. It includes snippets for configured Google Analytics,
Facebook Pixel, LinkedIn Insight Tag, PostHog, and Microsoft Clarity providers.
When at least one provider contributes markup, the hook stores the combined
string in the page-read context under the `AnalyticsScripts` metadata key.

The hook runs at order `100`. It does not persist events or make outbound
requests itself.

## Concern to Investigate

Raw string construction makes provider markup, encoding, placement, and
provider-specific behavior difficult to isolate and test. The removed TODO
suggested View Components as a replacement, but the intended component
boundaries and integration point were not recorded.

Before choosing an implementation:

1. Locate every consumer of the `AnalyticsScripts` metadata value and document
   where the result is rendered.
2. Define the trust boundary for provider identifiers and host values, including
   the required HTML and JavaScript encoding behavior.
3. Decide whether each provider should own a typed renderer or component.
4. Determine whether scripts belong in the document head, body, or end-of-body
   location and whether that placement differs by provider.
5. Define Content Security Policy and nonce requirements before changing the
   rendering contract.
6. Add focused tests for provider selection, output encoding, placement, and an
   empty configuration.

## Non-goal

This note does not authorize replacing the current hook or changing its
`AnalyticsScripts` metadata contract. That decision requires an architectural
review of the page-rendering pipeline and its security boundary.
