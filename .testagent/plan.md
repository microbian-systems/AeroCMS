# Scoped API key and MCP boundary test plan

1. Rewrite `ApiKeyServiceTests` around `ApiKeyDocument` and the current constructor.
   - Assert hash-only persistence and successful validation.
   - Assert normalized scoped capabilities and safe metadata.
   - Assert MCP read requirement.
   - Assert expired/revoked rejection and ownership boundaries.
2. Extend `ManagerAssistantBoundaryTests`.
   - Exact page-read permission succeeds far enough to invoke the page actor.
   - Wrong permission, wrong tenant, and unallowed site fail before actor access.
3. Extend endpoint metadata coverage.
   - Inspect MCP management and transport endpoints for their explicit policies.
4. Build and run the narrow TUnit filters.
5. Re-open tests and record assertion-quality and gap review in `.testagent/status.md`.

## AI conversation and explicit-memory tests

1. Create a conversation, append an assistant response, and continue it.
   - Assert server history is loaded in order.
   - Assert forged browser history is ignored for an existing conversation.
2. Attempt continuation and append from another tenant/site/principal scope.
   - Assert failure without exposing or modifying the conversation.
3. Save, list, and delete an explicitly confirmed long-term memory.
   - Assert source provenance is same-scope.
   - Assert another principal cannot read or delete it.
   - Assert anonymous public persistence is rejected.
4. Verify manager state retains the SSE conversation ID, sends only the newest turn after that, and clears it on reset/context changes.
5. Build and run the narrow TUnit filter for memory and manager-assistant tests.

## Public and member assistant tests

1. Inspect public and member endpoint metadata.
   - Assert public completion, SSE, and search are anonymous and rate limited.
   - Assert member completion, SSE, and history require member/site policies.
   - Assert member mutations require antiforgery and streams use the concurrency policy.
2. Exercise public grounding.
   - Assert it queries the public audience.
   - Assert it never reads explicit personal memory.
3. Exercise knowledge projection and retrieval.
   - Assert tenant/site/culture/publication/search/AI filters are applied before results are returned.
4. Re-run pipeline-ordering tests.
   - Assert fail-fast behavior and configured stage order.
5. Record assertion quality, known gaps, build constraints, and focused pass counts in `.testagent/status.md`.

## Stream-safe output and provider-budget tests

1. Exercise public citation validation.
   - Accept only identifiers present in the server-supplied citation set.
   - Reject missing and invented citation identifiers.
2. Exercise high-risk output detection.
   - Reject secret-bearing and regulated identifier samples.
   - Retain ordinary public contact information.
3. Exercise the real assistant streaming service with a provider that emits unsafe fragments.
   - Assert no provider delta is emitted before output-policy approval.
   - Assert a rejected response ends with an error and never emits completion.
4. Exercise token reservation under concurrency.
   - Assert atomic reservations never exceed the configured partition allowance.
   - Assert reconciliation is idempotent and refunds unused tokens.
   - Assert overages remain charged and later reservations fail closed.
   - Assert independent security partitions do not share allowance.
5. Rebuild abstractions, assistant, MCP endpoints, and the focused test assembly sequentially, then run the combined AI/MCP security suite.
