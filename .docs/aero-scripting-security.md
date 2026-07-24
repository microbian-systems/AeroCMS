# AeroCMS Scripting Security

**Status:** Accepted temporary risk and future hardening plan  
**Date:** 2026-07-24  
**Applies to:** Custom HTML, HTMX, Scriban, SharpTS, and runtime Razor templates

## Current decision

During the alpha, AeroCMS permits `hx-on:*` attributes in the shared HTML attribute policy. Their values are stored, versioned, and rendered as client-side JavaScript.

This is a trusted-author executable-code capability. It is not a sanitizer guarantee and must not be described as safe HTML authoring.

The executable-code exception is intentionally narrow:

- attribute names must begin with `hx-on:` and include an event name;
- HTMX shorthand such as `hx-on::before-request` is included;
- native inline handlers such as `onclick` remain denied;
- a bounded declarative HTMX set is also accepted for the experimental HTMX
  renderer, including request, target, trigger, swap, selection, indicator,
  confirmation, and history attributes;
- HTMX request URLs must be relative and same-origin;
- `js:` and `javascript:` forms in `hx-vals` and `hx-headers` are denied; and
- `script` elements, inline `style`, executable URL schemes, and unknown `hx-*`
  attributes remain denied.

Until the hardening work in this document is complete, sites should grant page and template authoring only to users who are trusted to run code in the public site origin.

## Why accept this temporarily

The alpha needs practical HTMX experimentation in Custom HTML and future HTMX page and fragment renderers. Supporting `hx-on:*` makes small client-side behaviors possible before the declarative action registry is complete.

The tradeoff is deliberate: development velocity is favored temporarily, while the risk and the exit conditions are made explicit.

## Accepted SaaS risk posture

For a deployment where one AeroCMS runtime and its database credential can reach only one tenant's or site's data, the expected blast radius of malicious authored code is that tenant or site. AeroCMS accepts that a trusted tenant administrator or a deliberately empowered user can author executable behavior for that boundary.

This is a containment decision, not a claim that the script is harmless. Public visitors, other users of the same site, privileged previewers, secrets available to the runtime, and the integrity of the tenant's content remain in scope.

A compromised credential is an incident owned jointly by the credential holder and the platform controls around that credential. AeroCMS cannot prevent every action available to a stolen key, but it can and should limit the damage through site and tenant scope, least privilege, expiration, revocation, rotation, auditing, and prompt invalidation.

### Current implementation does not yet prove these boundaries

The current repository uses one configured Sable connection with the default SurrealDB namespace and database both named `aero`. Site-owned documents are separated primarily by `SiteId` queries and ownership checks. The application does not currently select a different namespace or database for each site.

The current headless API-key path also resolves a key to an Identity user and issues a token from that user's roles. The persisted API-account key is not intrinsically bound to a tenant, site, or scripting capability.

Therefore the narrower SaaS threat model is valid only when the deployment layer actually provisions one isolated runtime/database credential per tenant or site, or after AeroCMS adds equivalent enforced tenant and site scoping. Documentation and configuration alone are not evidence of that isolation.

## Threat model

An author who can save `hx-on:*` can execute arbitrary JavaScript when a visitor, editor, or administrator renders the content. This creates a stored cross-site scripting boundary and can enable:

- authenticated same-origin requests made as the viewer;
- reading and exfiltrating data available to the page;
- modification of page content or administrative UI;
- phishing or credential capture within the site origin;
- persistence through saved versions and published content.

CORS does not protect same-origin endpoints from same-origin script. Antiforgery protects correctly configured state-changing requests but does not neutralize script already running in the authenticated page. `HttpOnly` cookies prevent direct cookie reads, not authenticated requests. `SameSite` is not a same-origin script boundary.

HTML encoding also does not make an intentional `hx-on:*` value safe. Encoding preserves the attribute boundary; HTMX still evaluates the decoded handler.

## Engine-specific boundaries

| Surface | Execution location | Primary risk | Required trust model |
| --- | --- | --- | --- |
| Custom HTML / `hx-on:*` | Browser | Stored XSS in the public site origin | Trusted author during alpha; capability-gated later |
| Scriban | Server | Data disclosure, resource exhaustion, and unsafe emitted HTML | Bounded template context and validated output |
| SharpTS | Server | Server-side code execution, .NET capability abuse, data or secret disclosure, and resource exhaustion | Isolated worker with an allowlisted host API |
| RazorEngineCore | Server | Compilation and execution of arbitrary Razor/C# | Trusted deployment or tightly isolated runtime only |

Server-side execution changes the threat, but does not make a language safe by default.

Scriban is the safer runtime-template option only while AeroCMS supplies a deliberately bounded object model, disables unsafe member access, applies execution limits, and validates generated markup. Scriban output containing `hx-on:*` still creates the browser-side risk described above.

SharpTS does not execute in the browser, so SharpTS source does not directly receive browser DOM or cookie access. However, SharpTS is a .NET language with .NET interop. If arbitrary imports or host objects are exposed, untrusted code may reach the file system, network, processes, assemblies, application services, or secrets.

The current alpha SharpTS renderer is an explicitly experimental trusted-author
exception: it interprets in the web process, denies imports, CommonJS imports,
and `@DotNetType`, exposes only detached page/site/content data, bounds source
and output, and validates returned HTML. It has no hard CPU-kill boundary.
SharpTS must therefore move outside the trusted web process before it is offered
to untrusted authors. It must never receive direct Sable, Orleans,
service-provider, or `HttpContext` access.

RazorEngineCore compiles Razor and C#. Its ordinary .NET host API can be invoked from SharpTS, but that interoperability does not reduce the Razor template's authority. Runtime Razor remains a trusted-code feature.

## Current controls that remain in force

- The HTML element catalog remains an allowlist.
- Native `on*` attributes remain blocked.
- Unsupported elements such as `script` remain blocked.
- URL-bearing attributes continue through scheme validation.
- Content and template versions remain attributable and auditable.
- Content data supplied to scripts remains immutable, bounded, culture-selected, and pre-shaped.
- Scriban and SharpTS do not query Sable or Orleans directly.
- Authorization and site scoping remain required for authoring, preview, and publication.

These controls reduce adjacent risks; none converts `hx-on:*` into untrusted-safe markup.

## Accepted authorization model

Use one site-scoped scripting capability across Custom HTML/HTMX, Scriban, and SharpTS:

- **Administrator:** may author and publish scripts for any site the administrator is authorized to administer.
- **Power user:** is not a new global role. It is a non-administrator with the `script` permission in that site's `UserSiteAssignment`.
- **Ordinary editor or contributor:** cannot create, modify, import, preview, or publish executable source.
- **API client:** must be bound to the same tenant, site, and scripting permission. A user-level API key without that binding is insufficient.

Register the stored `script` permission as the `site:script` authorization policy, following the existing `site:read`, `site:create`, `site:update`, and `site:delete` pattern. Script mutations require both the ordinary content mutation policy and `site:script`; scripting permission must not imply general content write access.

The permission applies consistently to:

- `hx-on:*` and any future raw JavaScript surface;
- Scriban page and fragment source;
- SharpTS page and fragment source; and
- future runtime Razor source, if that feature is ever enabled for CMS authors.

One authorization permission does not imply one runtime sandbox. Each engine retains separate site-level enablement and execution controls:

- `Scripting:AllowInlineHtmxHandlers`
- `Scripting:AllowScriban`
- `Scripting:AllowSharpTs`
- `Scripting:AllowRuntimeRazor`

SharpTS and runtime Razor remain higher-authority server execution surfaces and require worker isolation even when the author holds `site:script`.

Authorization must be checked on the server at source creation, import, update, preview, renderer conversion, and publication. Hiding Monaco or disabling a button is only a user-interface affordance.

## Future hardening plan

1. Add the site-level engine settings listed above, defaulting executable engines to `false` outside explicitly trusted alpha sites.
2. Register and enforce `site:script` in addition to the normal content mutation policy.
3. Record handler creation, modification, preview, and publication in the audit trail.
4. Add an emergency site-level kill switch that suppresses inline handlers without deleting source.
5. Prefer named declarative actions such as `data-aero-action="dismiss"` backed by an allowlisted client action registry.
6. Complete the HTMX endpoint registry so authors select logical action keys rather than entering arbitrary routes, headers, or request behavior.
7. Use separate trusted and untrusted Content Security Policy profiles. Do not claim that a restrictive no-eval HTMX profile is compatible with `hx-on:*`; inline handler evaluation must be disabled when the safe profile is active.
8. Render untrusted previews on a separate origin with a sandboxed iframe and no manager credentials.
9. Run SharpTS in a dedicated process or worker identity with a capability manifest, restricted references, no ambient application service provider, and enforced time, memory, output, and cancellation limits.
10. Treat RazorEngineCore templates as trusted code, disabled by default for ordinary authors, and isolate compilation artifacts and execution from the web process where runtime authoring is enabled.
11. Apply trust-profile-specific output validation after Scriban, SharpTS, and Razor rendering rather than trusting the source language.
12. Add publication approval for executable templates and notify reviewers when a change introduces or modifies executable behavior.

## Exit criteria for ordinary-author safety

The temporary exception can be retired from the default authoring profile when:

- ordinary authors cannot save or publish `hx-on:*`;
- trusted scripting requires both an enabled site engine and `site:script`;
- API credentials used for scripting are tenant-, site-, and capability-scoped, revocable, and expiring;
- executable changes are versioned, audited, reviewable, and immediately disableable;
- preview is isolated from authenticated manager sessions;
- the safe CSP and HTMX configuration are tested in production-like hosting;
- existing inline handlers are detected and quarantined or suppressed when the capability is disabled;
- SharpTS runs outside the web process with enforceable capability and resource limits; and
- tests cover both the safe author profile and the explicitly trusted scripting profile.

## Open decisions

- Whether executable changes always require a second-person publication approval.
- Whether trusted previews use the public origin or a separate privileged preview origin.
- Which named client actions AeroCMS will ship as safe replacements for common `hx-on:*` use cases.
- Whether runtime Razor will remain deployment-trusted only or gain a separately isolated authoring tier after 1.0.
