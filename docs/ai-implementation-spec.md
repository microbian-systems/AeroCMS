# AI Implementation Spec

## Status

Proposed

## Goal

Integrate AI-assisted writing into the AeroCMS manager as a normal platform feature with backend services, provider configuration, minimal APIs, and manager UI integration. Start with blog posts, then extend the same foundation to pages and documentation.

The first user-facing feature is an **Enhance** action in the manager post editor. When a manager user opens a blog post, they can ask AI to sharpen, correct, expand, summarize, or otherwise improve a selected field using a prompt. The result must be shown for review before it is applied to the editor.

## Non-Goals

- Do not build a UI-only prototype that calls an LLM directly from Blazor.
- Do not use Microsoft Semantic Kernel.
- Do not use AutoGen.
- Do not introduce npm or frontend package dependencies for this feature.
- Do not auto-save or auto-publish AI-generated output.
- Do not store API keys as plaintext manager settings.

## Architecture Direction

Build this as an AeroCMS manager feature with an AI service behind it.

The AI foundation should live in `Aero.Cms.Modules.Ai`. The existing `Aero.Cms.Modules.Ai` and `Aero.Cms.Modules.AiAssistant` projects currently appear to be placeholders, so `Aero.Cms.Modules.Ai` should become the real module boundary for AI configuration, provider creation, content-enhancement services, and admin endpoints.

Use Microsoft Agent Framework as the agent orchestration layer. Microsoft Agent Framework can create agents from `Microsoft.Extensions.AI.IChatClient`, which fits AeroCMS because Tornado LLM already provides a `LlmTornado.Microsoft.Extensions.AI` adapter and package versions already exist in central package management.

For the posts MVP, use a single constrained content-editing agent rather than a multi-agent workflow. The operation is a bounded writing task: receive content, receive a user prompt, return improved text plus optional rationale and warnings.

## Provider Strategy

### Primary Provider: Tornado LLM

Use Tornado LLM as the primary model client abstraction. It supports multiple providers and custom endpoints, which makes it a good bridge between cloud providers and local model servers.

### Local LLM: LM Studio

Support LM Studio through a custom/OpenAI-compatible endpoint configuration. The manager settings should allow a base URL such as `http://localhost:1234/v1`, a model name, and an optional API key placeholder when required by a local server.

### OpenCode

Support OpenCode as an optional future provider adapter. The OpenCode SDK is TypeScript/REST oriented and exposes sessions, prompts, model selection, events, config, and file APIs. Since AeroCMS should not add npm dependencies for this feature, a .NET adapter should call the OpenCode server API with `HttpClient` if this path is implemented.

OpenCode should be treated as a separate provider adapter, not as the core implementation.

## Manager Settings

Add an AI section to the left manager navigation and/or the global settings area. The AI settings UI should include:

- Enabled: bool
- Provider: select list
  - Tornado
  - LM Studio
  - OpenCode
  - Future providers
- Endpoint/base URL
- Model name
- API key source
  - Environment variable name
  - Secrets module key, if available
  - Empty for local endpoints that do not require auth
- Temperature
- Max output tokens
- Timeout seconds
- Stream responses: bool, optional
- Save usage telemetry: bool

Configuration should be read through typed options or a dedicated configuration service. Avoid scattering string setting keys through UI and service code.

Suggested setting keys:

- `Ai.Enabled`
- `Ai.Provider`
- `Ai.Endpoint`
- `Ai.Model`
- `Ai.ApiKeySecretName`
- `Ai.ApiKeyEnvironmentVariable`
- `Ai.Temperature`
- `Ai.MaxOutputTokens`
- `Ai.TimeoutSeconds`
- `Ai.StreamResponses`
- `Ai.SaveUsageTelemetry`

## Backend Design

### Core Contracts

Create request/response contracts that are shared between the manager client and minimal API endpoint.

```csharp
public sealed record EnhanceContentRequest(
    string ContentKind,
    string TargetField,
    string CurrentText,
    string? UserPrompt,
    string? Title,
    string? Summary,
    string? Slug,
    string? Tone,
    IReadOnlyDictionary<string, string>? Metadata);

public sealed record EnhanceContentResponse(
    string EnhancedText,
    string? Rationale,
    IReadOnlyList<string> Warnings,
    string Provider,
    string Model,
    AiUsage? Usage);

public sealed record AiUsage(
    int? InputTokens,
    int? OutputTokens,
    int? TotalTokens);
```

Content kinds should start with:

- `post`
- `page`
- `doc`

Target fields should start with:

- `body`
- `title`
- `summary`
- `seoTitle`
- `seoDescription`

### Services

Suggested interfaces:

```csharp
public interface IAiContentEnhancementService
{
    Task<Result<EnhanceContentResponse, AeroError>> EnhanceAsync(
        EnhanceContentRequest request,
        CancellationToken cancellationToken = default);
}

public interface IAiChatClientFactory
{
    Task<Result<IChatClient, AeroError>> CreateAsync(
        CancellationToken cancellationToken = default);
}

public interface IAiSettingsProvider
{
    Task<Result<AiSettings, AeroError>> GetAsync(
        CancellationToken cancellationToken = default);
}
```

The enhancement service should:

1. Validate the input.
2. Load AI settings.
3. Create an `IChatClient` through the provider factory.
4. Wrap the chat client with Microsoft Agent Framework.
5. Run the content-enhancement agent with strict instructions.
6. Return structured output.

### Agent Instructions

The post enhancement agent should be explicitly constrained:

```text
You are an editorial assistant inside AeroCMS.
Improve the supplied CMS content according to the user's prompt.
Preserve the original meaning, factual claims, markdown structure, links, code blocks, and front matter unless the user explicitly asks to change them.
Do not invent facts, quotes, statistics, sources, product claims, or dates.
Return only structured output matching the requested schema.
If the request is unsafe, ambiguous, or would require inventing facts, return a warning and keep the text conservative.
```

### Minimal API

Add an authenticated admin endpoint:

```http
POST /api/admin/ai/content/enhance
```

The endpoint should:

- Require manager/admin authorization.
- Accept `EnhanceContentRequest`.
- Use FluentValidation for request validation.
- Return `EnhanceContentResponse`.
- Use AeroCMS `Result<T>` / `AeroError` conventions.
- Avoid leaking provider exception details to the client.
- Log provider, model, latency, success/failure, and token usage if available.

### HTTP Client

Add an `IAiHttpClient` to `Aero.Cms.Abstractions.Http.Clients` following the existing manager typed-client pattern.

The post editor should call the typed client, not the backend service directly.

## Posts MVP UI

Add an **Enhance** button to the post editor near the existing Save/Preview actions.

When clicked, open a modal or side panel with:

- Target field selector
  - Body
  - Title
  - Summary
  - SEO Title
  - SEO Description
- Prompt textarea
- Quick actions
  - Sharpen
  - Fix grammar
  - Make concise
  - Expand
  - Improve SEO
- Preview of the generated suggestion
- Apply button
- Discard button

Behavior:

1. If the user is editing in the Monaco Code tab, sync Monaco content before sending the request.
2. Send the selected field value and metadata to the AI endpoint.
3. Show the enhanced result without mutating editor state.
4. On Apply, update the selected local field and mark the post dirty.
5. Save remains a separate user action.
6. Publish remains a separate user action.

## Pages and Docs Follow-Up

After posts works end to end, extend the same foundation.

### Pages

Pages likely need block-aware enhancement. The AI UI should allow selecting:

- Whole page metadata
- A block
- A layout region
- SEO fields

For page blocks, the service should preserve block structure and only update the selected text-bearing block unless the user asks for a wider rewrite.

### Docs

Docs should support article/category-aware context. Suggested targets:

- Article body
- Article title
- Category description
- Summary/excerpt
- SEO fields

Future docs enhancement can use retrieval over existing docs content, but the MVP should stay simple and avoid vector/RAG work until the editing loop is proven.

## Implementation Plan

### Phase 1: Foundation

- Convert `Aero.Cms.Modules.Ai` from placeholder into the real AI module.
- Add Microsoft Agent Framework packages to central package management and the AI module project.
- Reuse existing `LlmTornado` and `LlmTornado.Microsoft.Extensions.AI` package entries.
- Add `AiSettings`, `AiProviderKind`, and `IAiSettingsProvider`.
- Add `IAiChatClientFactory` for provider creation.

Acceptance criteria:

- AI settings can be loaded as a typed object.
- A Tornado-backed `IChatClient` can be created from configured settings.
- Misconfiguration returns `AeroError` instead of throwing through the UI.

### Phase 2: Content Enhancement Service

- Add `IAiContentEnhancementService`.
- Build the Microsoft Agent Framework content-editing agent from the configured `IChatClient`.
- Add structured request/response DTOs.
- Add validation for required fields, supported targets, and max content length.

Acceptance criteria:

- Service can enhance a body field with Tornado/LM Studio configuration.
- Service returns enhanced text and warnings.
- Service does not mutate posts directly.

### Phase 3: Admin API and Manager Client

- Add `POST /api/admin/ai/content/enhance`.
- Add `IAiHttpClient` in the abstractions project.
- Register the typed client in existing HTTP client registration.
- Add API tests with Alba where practical.

Acceptance criteria:

- Authenticated manager requests can call the endpoint.
- Invalid requests return validation errors.
- Provider failures return safe errors.

### Phase 4: Posts UI

- Add an Enhance button to the post editor.
- Add an enhancement modal or side panel.
- Support body/title/summary/SEO targets.
- Apply results locally and mark the post dirty.

Acceptance criteria:

- User can enhance an existing post body.
- User can preview, apply, or discard AI output.
- Save and publish behavior remains unchanged.

### Phase 5: Pages and Docs

- Add the same enhancement UX to page editor targets.
- Add the same enhancement UX to docs article targets.
- Consider block-aware structured enhancement for page content.

Acceptance criteria:

- Pages and docs use the same backend AI service and typed client.
- No duplicate provider logic is introduced.

## Verification Strategy

- Unit tests for settings parsing and provider selection.
- Unit tests for prompt/request construction.
- Unit tests for validation rules.
- Alba integration test for the admin enhancement endpoint.
- Manual test with LM Studio running locally.
- Manual test with Tornado configured for a cloud provider.
- Manual test that Apply changes editor state but does not save or publish.

## Risks and Mitigations

| Risk | Mitigation |
| --- | --- |
| AI invents facts | Use conservative system instructions, warnings, and human review before Apply. |
| Provider config leaks secrets | Store only environment variable or secret names in settings. |
| Local endpoint is unavailable | Return a safe configuration/connectivity error. |
| Editor content gets out of sync | Sync Monaco before enhancement, same as save/preview behavior. |
| Pages block structure is damaged | Start with posts; require block-aware enhancement before pages. |
| OpenCode integration pulls in npm | Treat OpenCode as an HTTP provider adapter only. |

## Open Questions

- Should AI settings live in Global Settings or as a dedicated left-nav AI section with its own page?
- Should API keys use the existing Secrets module, environment variables only, or both?
- Should the first post enhancement support streaming, or keep the MVP request/response only?
- Should enhancement history be stored for audit/review?
- What is the maximum content size for a single enhancement request?

## Reference Links

### Microsoft Agent Framework

- [Microsoft Agent Framework overview](https://learn.microsoft.com/en-us/agent-framework/overview/?pivots=programming-language-csharp)
- [Microsoft Agent Framework documentation root](https://learn.microsoft.com/en-us/agent-framework/)
- [Microsoft Agent Framework agents](https://learn.microsoft.com/en-us/agent-framework/agents/?pivots=programming-language-csharp)
- [Microsoft Agent Framework GitHub repository](https://github.com/microsoft/agent-framework)
- [Introducing Microsoft Agent Framework: The Open-Source Engine for Agentic AI Apps](https://devblogs.microsoft.com/foundry/introducing-microsoft-agent-framework-the-open-source-engine-for-agentic-ai-apps/)
- [Microsoft Agent Framework Version 1.0](https://devblogs.microsoft.com/agent-framework/microsoft-agent-framework-version-1-0/)

### .NET AI Building Blocks Series

- [.NET AI Essentials - The Core Building Blocks Explained](https://devblogs.microsoft.com/dotnet/dotnet-ai-essentials-the-core-building-blocks-explained/)
- [Vector Data in .NET - Building Blocks for AI Part 2](https://devblogs.microsoft.com/dotnet/vector-data-in-dotnet--building-blocks-for-ai-part-2/)
- [Microsoft Agent Framework - Building Blocks for AI Part 3](https://devblogs.microsoft.com/dotnet/microsoft-agent-framework-building-blocks-for-ai-part-3/)

### Provider and Client References

- [OpenCode SDK](https://opencode.ai/docs/sdk/)
- [LLM Tornado getting started](https://llmtornado.ai/getting-started)
- [LLM Tornado GitHub repository](https://github.com/lofcz/LLMTornado)
