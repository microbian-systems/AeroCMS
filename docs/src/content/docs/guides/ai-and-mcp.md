---
title: AI and MCP
description: AI providers, protected settings, streaming authoring tools, manager assistant, MCP tools, and current limitations.
---

AI authoring and MCP integration are experimental. They are manager capabilities, not autonomous authorities. The application remains responsible for authentication, site selection, permission checks, validation, persistence, and publication.

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

## Manager assistant

The assistant exposes authenticated JSON and SSE endpoints:

- `POST /api/v1/admin/mcp/assistant/complete`
- `POST /api/v1/admin/mcp/assistant/stream`

Both require an authenticated principal and `site:read`. Responses carry a correlation ID. The assistant obtains tools from the same site-scoped executor used by MCP.

## MCP server

`/mcp` is a stateless Streamable HTTP MCP endpoint. It requires authentication and `site:read`. For every tool call, the context factory re-reads the current principal, Snowflake user ID, `AeroCms.SiteId` cookie, site permission, and tenant-bearing site record.

Available tools cover current site, bounded page/post/docs/content-type/content-item lists and gets, draft creation, and bounded content hierarchy reads. List sizes are capped; IDs cross MCP as decimal strings. Create tools require their declared create permission in the executor and create drafts, not published content.

## External-client limitation

The HTTP endpoint currently relies on the AeroCMS host's existing authenticated principal and selected-site cookie. There is no verified dedicated OAuth authorization-server, token-exchange, or client-registration flow for arbitrary external MCP clients at this baseline. A browser manager session can reach the endpoint; a production external client needs an explicitly designed authentication and consent boundary.

Do not expose `/mcp` to the internet until authentication, site selection, CSRF/origin behavior, transport limits, audit, rate limiting, and external-client authorization have been threat-modeled and tested.
