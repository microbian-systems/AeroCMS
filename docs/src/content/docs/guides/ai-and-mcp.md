---
title: AI and MCP
description: AI providers, grounded assistants, protected settings, scoped MCP API keys, rate limits, and current limitations.
---

AI authoring, site assistants, and MCP integration are experimental. Anonymous and member assistants are deliberately narrower than manager authoring and MCP capabilities; none is an autonomous authority. The application remains responsible for authentication, site selection, permission checks, validation, persistence, and publication.

## AI settings and providers

The manager page `/manager/ai` configures provider profiles. Current defaults include configurable OpenAI, Anthropic, OpenRouter, LM Studio, and OpenCode profiles, with capability flags determining whether a provider supports enhancement or translation.

Admin APIs under `/api/v1/admin/ai` expose:

- `POST /content/enhance`
- `POST /content/enhance/stream`
- `POST /content/translate`
- `GET|POST /settings`
- `GET /providers/options`

Provider keys are write-only in requests and protected before persistence. Read models expose only whether a key exists. Provider calls use a typed HTTP client with a bounded transport timeout and disabled redirects. Keep endpoints allowlisted and never return a stored key to the browser.

## Streaming

Enhancement streaming uses server-sent events with metadata, delta, completion, and failure semantics. The manager clients used by page, post, and docs authoring must handle cancellation, partial output, provider failure, invalid structured output, and a non-streaming fallback.

AI output is a draft suggestion. Validate, sanitize, preview, and require the normal publish permission; a provider response must not bypass the page/content command service.

## Grounding, exposure, and budgets

Assistant requests pass through registered normalization, scope-resolution, input-safety, and telemetry stages. Terminal assistant orchestration applies the token budget, CMS grounding, output policy, and conversation persistence. Grounding is site-, tenant-, culture-, audience-, and principal-scoped. Responses cite retrieved CMS sections with stable markers such as `[CMS-1]`; model output remains untrusted.

Structured content is eligible for public AI only when it is published and its content type enables both `IncludeInSearch` and `IncludeInPublicAi`. Structured-content fields default to `Internal`; `Public` fields may enter public/member grounding, `Internal` fields are manager-only, and `Sensitive` or `Secret` fields are denied by the standard retrieval path. Page, post, and docs knowledge projections separately mark approved sections as public after their record-level public-AI opt-in.

Token budgets under `AeroCms:Ai:TokenBudget` are partitioned and fail closed when exhausted or unavailable. The current budget coordinator is process-local, so multiple host instances do not share a cluster-global token budget. Public assistant calls are ephemeral and load no personal memory. Member and manager conversations are durable and isolated by tenant, site, audience, principal, and culture. Explicit memory management is manager-only, and only explicitly saved memories are loaded.

## Public, member, and manager assistants

Anonymous public-corpus-only endpoints are:

- `POST /api/v1/ai/assistant/complete`
- `POST /api/v1/ai/assistant/stream`
- `GET /api/v1/ai/search`

Authenticated external members use `POST /api/v1/member/assistant/complete` and `/stream`, plus scoped conversation list/get/delete routes. Member mutations require antiforgery validation.

The assistant exposes authenticated JSON and SSE endpoints:

- `POST /api/v1/admin/mcp/assistant/complete`
- `POST /api/v1/admin/mcp/assistant/stream`

Manager routes require an authenticated principal and `site:read`. The same group exposes scoped conversation list/get/delete and explicit memory list/create/update/delete routes. API-key principals cannot access user assistant conversations. Responses carry a correlation ID, and the assistant obtains tools from the same site-scoped executor used by MCP.

## MCP server

`/mcp` is a stateless Streamable HTTP MCP endpoint protected by the dedicated `AeroApiKey.Mcp` policy and the `Aero.Mcp.Transport` rate-limit policy. Supply a service key in `X-Aero-Api-Key` or as `Authorization: ApiKey <key>`. A key is tenant-scoped, explicitly enabled for MCP, permission-scoped, optionally expiring, and restricted to an allowed set of sites. Use `X-Aero-Site-Id` when a key allows multiple sites; a single allowed site can be selected implicitly.

Available tools cover current site, bounded page/post/docs/content-type/content-item lists and gets, draft creation, and bounded content hierarchy reads. List sizes are capped; IDs cross MCP as decimal strings. Create tools require their declared create permission in the executor and create drafts, not published content.

Each tool also enforces its permission domain and an application-level read, write, or destructive rate limit. The current HTTP and application-level rate limiters are process-local, not cluster-global. The API-key principal, tenant, allowed-site membership, selected site, expiry, MCP enablement, and tool permission are revalidated at invocation.

## API-key management and external-client limitation

Authenticated managers with `AeroAdmin` and `site:read` can list, create, and revoke keys under `/api/v1/admin/mcp/api-keys`. The raw key is returned only at creation; persist only its protected representation and rotate or revoke it when exposure is suspected.

The service-key boundary is intended for controlled machine clients, but focused tests do not yet cover a complete HTTP authentication and multi-site-header lifecycle. Manager assistant and key-management mutations also need a reviewed origin/antiforgery posture. AeroCMS does not provide a verified OAuth authorization server, delegated consent flow, token exchange, or dynamic client registration. Before internet exposure, require TLS and complete a deployment-specific threat model covering key provisioning, storage, rotation, revocation, audit, origin behavior, distributed rate-limit sizing, and incident response. Interactive delegated clients need an OAuth-style boundary rather than shared service keys.
