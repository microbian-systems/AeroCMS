# Public API documentation gaps

Baseline: `35ec154fb3b57e838d4fe6211f9d9f193e53d812`.

The API inventory found at least 41 public types without useful type summaries. This report records the debt without changing executable source merely to suppress documentation warnings.

## Aero.Cms.Abstractions

Authentication and style contracts contain 31 known type-level gaps across:

- `Authentication/ExternalIdentityProviderContracts.cs`
- `Authentication/ExternalMemberIssuanceContracts.cs`
- `Authentication/LocalExternalMemberAuthenticationContracts.cs`
- `Authentication/ManagerFederationContracts.cs`
- `Models/SiteStyleProfileViewModel.cs`
- `Requests/UpdateSiteStyleProfileRequest.cs`

## Aero.Cms.Core

Content search/validation code contains 10 known type-level gaps across:

- `Content/Indexing/ContentIndexService.cs`
- `Content/Search/ContentSearchConstants.cs`
- `Content/Search/ContentSearchRequest.cs`
- `Content/Search/IContentEmbeddingGenerator.cs`
- `Content/Services/CompositeContentFieldValidation.cs`

Member-level gaps may also remain. Address these in a separate source-documentation change that explains contracts, inputs, outputs, failure behavior, and security constraints; do not add comments that only restate names.
