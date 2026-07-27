# AI and MCP infrastructure test status

## Iteration log

1. Rebuilt the AI module and test assembly without rebuilding locked running-app project references.
2. Ran the memory store tests. The update test exposed a timestamp precision mismatch between an in-memory response and Sable's persisted second precision.
3. Normalized explicit-memory timestamps to the persistent precision at creation/update time and retained the strict equality assertion.
4. Re-ran all focused AI/MCP boundary suites.
5. Added complete-response output policy enforcement and buffered SSE delivery so unsafe provider fragments cannot escape before approval.
6. Added scoped provider-token reservation and reconciliation with atomic process-local concurrency control behind a replaceable coordinator contract.
7. Added focused policy, streaming, budget-partition, refund, overage, idempotency, and concurrency tests.

## Final focused results

| Suite | Passed | Failed |
| --- | ---: | ---: |
| `AeroAiMemoryStoreTests` | 4 | 0 |
| `AeroCmsAssistantGroundingTests` | 3 | 0 |
| `ManagerAssistantBoundaryTests` | 15 | 0 |
| `SiteAssistantBoundaryTests` | 1 | 0 |
| `AeroAiKnowledgeProjectionTests` | 6 | 0 |
| `AeroAiRequestPipelineTests` | 3 | 0 |
| `AeroCmsAssistantOutputPolicyTests` | 3 | 0 |
| `AeroAiTokenBudgetCoordinatorTests` | 3 | 0 |
| **Total** | **38** | **0** |

The focused test project build succeeded with zero errors. Existing repository package-version and vulnerability warnings remain and were not introduced by this feature.

## Assertion-quality review

- Persistence tests assert externally meaningful invariants: complete scope isolation, deterministic ordering, bounded history, forged-history rejection, provenance ownership, update identity, deletion, and anonymous-memory rejection.
- Endpoint tests inspect authorization, antiforgery, and rate-limit metadata rather than only checking route existence.
- Grounding tests assert the audience passed to retrieval and verify that the public path does not invoke explicit memory.
- Knowledge tests exercise pre-retrieval eligibility and cross-scope exclusion.
- Pipeline tests assert stage ordering and fail-fast behavior.
- Output-policy tests assert that public citations are constrained to server-grounded sources and that high-risk output is rejected without overblocking ordinary contact text.
- The streaming service test uses an unsafe multi-fragment provider response and asserts that no delta or completion event reaches the caller.
- Budget tests assert strict concurrent reservation limits, partition isolation, refunds, actual-usage overages, and idempotent reconciliation.

## Remaining gaps

- No provider-backed end-to-end SSE test yet verifies cancellation and concurrency-lease release.
- Public/member browser components have not been implemented, so UI-level Playwright coverage is deferred.
- Documentation ingestion cannot be exercised until the curated Starlight/DocFX corpus is available.
- Monetary accounting and distributed rate/provider token budgets need multi-instance integration coverage; the current strict token coordinator is process-local and replaceable.
