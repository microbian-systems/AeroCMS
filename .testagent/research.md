# Scoped API key and MCP boundary research

## Target inventory

- `src/Aero.Cms.Modules.Security/ApiKeyService.cs`
- `src/Aero.Cms.Modules.Security/AeroApiKeyAuthenticationHandler.cs`
- `src/Aero.Cms.Modules.Mcp/AeroCmsToolExecutor.cs`
- `src/Aero.Cms.Modules.Mcp/AeroMcpApiKeyEndpoints.cs`
- `src/Aero.Cms.Modules.Jwt/Areas/Api/v1/JwtApi.cs`
- `tests/Aero.Cms.Core.Tests/Services/ApiKeyServiceTests.cs`
- `tests/Aero.Cms.Core.Tests/Ai/ManagerAssistantBoundaryTests.cs`
- `tests/Aero.Cms.Core.Tests/Integration/AdminEndpointAuthorizationMetadataTests.cs`

## Existing conventions

- TUnit attributes (`[Test]`, `[Before(Test)]`, `[After(Test)]`).
- Shouldly assertions.
- NSubstitute for collaborators.
- Sable with `SurrealDbMemoryClient` for document-service tests.
- Endpoint metadata tests build a `WebApplication` and inspect `RouteEndpoint` metadata.

## Acceptance checklist

- Raw API-key values are never persisted.
- User-session keys validate to the owning user and expire.
- Scoped keys preserve tenant, site, MCP, administrator, and normalized CRUD permissions.
- Non-administrator MCP keys require at least one read capability.
- Revoked and expired keys fail validation.
- Listing and revocation are constrained to the owning user and tenant.
- API-key MCP tool calls require the exact domain operation.
- API-key MCP tool calls cannot cross their tenant or allowed-site scope.
- MCP management routes require `AeroAdmin`, `site:read`, and the management rate policy.
- MCP transport requires the dedicated scoped-key policy.

## AI conversation and explicit-memory target inventory

- `src/Aero.Cms.Abstractions/Ai/Assistant/AeroCmsAssistantContracts.cs`
- `src/Aero.Cms.Abstractions/Ai/Memory/AeroAiMemoryContracts.cs`
- `src/Aero.Cms.Modules.Ai/Memory/`
- `src/Aero.Cms.Modules.AiAssistant/AeroCmsAssistantService.cs`
- `src/Aero.Cms.Shared/Services/ManagerAssistantState.cs`
- `tests/Aero.Cms.Core.Tests/Ai/AeroAiMemoryStoreTests.cs`

## AI memory acceptance checklist

- Conversation ownership repeats tenant, site, audience, principal kind, principal ID, and culture.
- Every existing-conversation load and append applies that full ownership predicate.
- Existing conversations ignore browser-supplied prior history and accept only the newest user turn.
- Provider history is bounded by both message count and total characters.
- Anonymous public and MCP calls cannot create durable personal memory.
- SSE metadata and REST fallback return the durable conversation ID.
- Reset and user/site context changes clear the browser-held conversation ID.
- Long-term memory is written only by an explicit store call; no automatic extraction or promotion exists.
- Explicit-memory source conversation/message references must belong to the same security scope.

## Public and member assistant target inventory

- `src/Aero.Cms.Modules.AiAssistant/AeroSiteAssistantEndpoints.cs`
- `src/Aero.Cms.Modules.AiAssistant/AeroCmsAssistantService.cs`
- `src/Aero.Cms.Modules.AiAssistant/AeroCmsAssistantGroundingService.cs`
- `src/Aero.Cms.Modules.Ai/Knowledge/`
- `tests/Aero.Cms.Core.Tests/Ai/SiteAssistantBoundaryTests.cs`
- `tests/Aero.Cms.Core.Tests/Ai/AeroCmsAssistantGroundingTests.cs`
- `tests/Aero.Cms.Core.Tests/Ai/AeroAiKnowledgeProjectionTests.cs`

## Public and member acceptance checklist

- Anonymous completion and streaming use only the public knowledge audience and do not persist conversation or explicit memory.
- Member completion and streaming require both external-member authentication and site scope.
- Member history is scoped by tenant, site, member principal, audience, and culture.
- Public/member assistant profiles do not receive manager tools or internal AeroCMS documentation.
- A public corpus miss returns a fixed closed-book unavailable answer.
- Public search is bounded and retrieves only records already filtered for publication, search inclusion, public-AI inclusion, site, tenant, and culture.
- SSE streams use the shared stream concurrency policy and disable response buffering.
- Member mutations require antiforgery metadata.
- Manager memory UI performs only explicit, confirmed writes and supports correction and deletion.

## Output-policy and provider-budget target inventory

- `src/Aero.Cms.Abstractions/Ai/Budget/AeroAiTokenBudgetContracts.cs`
- `src/Aero.Cms.Modules.AiAssistant/AeroAiTokenBudgetCoordinator.cs`
- `src/Aero.Cms.Modules.AiAssistant/AeroCmsAssistantOutputPolicy.cs`
- `src/Aero.Cms.Modules.AiAssistant/AeroCmsAssistantService.cs`
- `src/Aero.Cms.Modules.AiAssistant/AeroSiteAssistantEndpoints.cs`
- `src/Aero.Cms.Modules.Mcp/AeroCmsAssistantEndpoints.cs`
- `tests/Aero.Cms.Core.Tests/Ai/AeroCmsAssistantOutputPolicyTests.cs`
- `tests/Aero.Cms.Core.Tests/Ai/AeroAiTokenBudgetCoordinatorTests.cs`

## Output-policy and provider-budget acceptance checklist

- Public/member output must cite only server-supplied retrieval identifiers.
- Secret material, bearer tokens, credential assignments, SSNs, and Luhn-valid payment-card values are rejected.
- Ordinary public contact text such as email addresses and telephone numbers remains usable.
- Provider streaming output is buffered until the complete response is approved; rejected streams emit no delta and no completion event.
- A conservative token allowance is reserved before provider execution.
- Reconciliation is idempotent, refunds unused tokens, and charges actual overages.
- Concurrent reservations cannot exceed a partition allowance.
- Tenant, site, audience, principal, provider, and model dimensions isolate budget partitions.
- The process-local coordinator is replaceable through the abstraction; distributed multi-instance accounting remains separate integration work.
