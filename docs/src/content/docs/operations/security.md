---
title: Security hardening
description: Production-readiness checklist for AeroCMS hosts, sites, content, scripting, media, AI, MCP, and operations.
---

AeroCMS is not production ready. The following controls are minimum acceptance gates, not a certification.

## Identity and authorization

- Keep recovery-administrator credentials offline and monitored.
- Require HTTPS, secure cookies, appropriate SameSite, short-lived federation state, and persistent protected key rings.
- Require authentication plus least-privilege `site:*` policies on every manager/admin endpoint.
- Revalidate site assignment after reading `AeroCms.SiteId`; never trust the cookie alone.
- Separate manager and external-member schemes, logout, authorities, and claims.
- Hash API keys, show raw values once, rotate them, and apply route/site authorization after authentication.
- Add antiforgery or explicit origin/token defenses to every browser mutation.

## Tenant and content isolation

- Resolve host to site fail-closed.
- Add site, tenant, and culture predicates to every load/query/update/delete.
- Test foreign IDs for direct-object-reference failures.
- Return drafts only from authenticated preview routes with no-store headers.
- Include site, culture, theme, representation, and query in cache keys/variation.

## Rendering

- Keep raw script and inline event-handler execution outside the HTML importer.
- Treat Scriban as constrained template execution, not a proof of harmless input.
- Permit SharpTS only for trusted authors; it is in-process and not an OS sandbox.
- Bound hierarchy depth/items, template size/loops/time/output, and provider payloads.
- Encode HTMX fragments and validate every dynamic URL, header, selector, and server parameter.
- Maintain a restrictive Content Security Policy compatible with required CDN assets.

## Media

Do not deploy the current general media upload path unchanged. Add site-ownership authorization, canonical path containment, extension and content-signature allowlists, request/file quotas, malware scanning, non-executable object storage, random server filenames, rollback/cleanup around metadata writes, and antiforgery/origin protection.

## AI and MCP

- Keep provider keys protected and write-only.
- Allowlist provider endpoints and disable redirects.
- Treat model output as untrusted draft content.
- Require normal validation and publish permissions after AI generation.
- Rate-limit and audit assistant/MCP requests and tool calls.
- Keep tool list/take/output bounds.
- Do not expose `/mcp` externally until a dedicated client authentication, consent, site-selection, and revocation design is implemented and tested.

## Operations

- Remove or rotate any credentials ever committed to development files; never reuse example values.
- Pin container images and verify supply-chain artifacts.
- Restrict SurrealDB, cache, health, dashboards, and telemetry collectors to private networks.
- Encrypt backups and prove restore procedures.
- Configure structured audit events without storing secrets or excessive PII.
- Apply request limits, timeouts, cancellation, rate limiting, and safe error responses.
- Run dependency, secret, SAST, DAST, authorization-metadata, cross-site, and browser security tests before release.

## Release gate

Production approval should require zero known critical/high findings, completed media and MCP boundaries, verified backup restore, explicit dependency health checks, a reviewed data-retention policy, incident response contacts, and an accepted threat model for the chosen topology.
