---
title: Manager and member identity
description: Manager authentication, external members, roles, site permissions, API keys, and authority boundaries.
---

AeroCMS separates manager identity from storefront/external-member identity. They use different schemes, authorities, cookies, and authorization policies. Do not reuse a manager cookie as a member session or infer site permissions from a successful external login.

## Manager authentication

The setup recovery administrator is the fail-safe local authority. The manager can use local login and configured federation. Current federation contracts cover Entra workforce and WorkOS callback/binding flows. Provider activation is recorded against the recovery administrator; a configured provider is not considered authoritative merely because configuration values exist.

Manager routes require authentication. Sensitive screens add roles or `AeroAdmin`; site-scoped APIs add `site:*` policies. Logout clears the relevant manager session state.

## External members

External-member endpoints live under `/api/v1/member` with local activation/login/reset under `/api/v1/member/local`. The dedicated external-member scheme and site policy protect storefront/member operations. Invitations and local password resets are initiated from administrator endpoints.

The authority may be local or external depending on durable setup/configuration. Callback handling must validate the selected site, authority, correlation/state, and returned subject before issuing a member principal.

## Roles and site assignments

Roles express platform-level authority. Site assignments express per-site read/create/update/delete permissions. An administrator can manage assignments at `/manager/users/{userId}/sites`.

Enforcement must combine:

1. an authenticated manager principal;
2. a valid Snowflake user ID;
3. an explicitly selected site;
4. authorization for the requested `site:*` operation;
5. tenant/site consistency in the application query.

## API keys

The Security module supports generated API keys, stores a hash rather than the raw key, and returns a new raw value only at creation/rotation time. Configuration under `Aero:Security:ApiKeys` controls prefix and length.

Treat API-key support as an authentication strategy, not universal authorization. A key-derived principal still needs route and site permissions. Never put raw keys in URLs, logs, docs, source control, or the ingestion corpus.

## Browser and external-client boundaries

Manager cookies require HTTPS, appropriate SameSite/secure settings, data-protection key continuity, and antiforgery protection for browser-originated changes. External programmatic clients should use an explicitly supported authentication mechanism; reproducing the manager cookie and selected-site cookie is not a production OAuth design.

The current MCP HTTP endpoint uses the existing authenticated request principal and `AeroCms.SiteId` selection. It does not advertise a dedicated third-party OAuth authorization server. See [AI and MCP](/guides/ai-and-mcp/).
