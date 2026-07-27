# Public API documentation gaps

Baseline: `ece0dc3915de1005fba89357f4957830a963206e`.

The API inventory found at least 41 public types without useful type summaries. This report records the debt without changing executable source merely to suppress documentation warnings.

## Aero.Cms.Abstractions

Authentication and style contracts contain 31 known type-level gaps across:

- `Authentication/ExternalIdentityProviderContracts.cs`
- `Authentication/ExternalMemberIssuanceContracts.cs`
- `Authentication/LocalExternalMemberAuthenticationContracts.cs`
- `Authentication/ManagerFederationContracts.cs`
- `Models/SiteStyleProfileViewModel.cs`
- `Requests/UpdateSiteStyleProfileRequest.cs`

The baseline also adds summarized public AI/MCP types under `Ai/Assistant`, `Ai/Budget`, `Ai/Knowledge`, `Ai/Memory`, `Ai/Pipeline`, and `Security`. Their type summaries preserve the known type-level count above, but member-level XML debt remains in constants, enum values, properties, and interface methods across those contract files. A later source-documentation pass should explain audience and scope rules, cancellation, failures, security constraints, and one-time secret behavior rather than merely restating member names.

## Aero.Cms.Core

Content search/validation code contains 10 known type-level gaps across:

- `Content/Indexing/ContentIndexService.cs`
- `Content/Search/ContentSearchConstants.cs`
- `Content/Search/ContentSearchRequest.cs`
- `Content/Search/IContentEmbeddingGenerator.cs`
- `Content/Services/CompositeContentFieldValidation.cs`

Member-level gaps may also remain. Address these in a separate source-documentation change that explains contracts, inputs, outputs, failure behavior, and security constraints; do not add comments that only restate names.
