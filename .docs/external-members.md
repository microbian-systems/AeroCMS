# External Members: Microsoft Entra External ID and WorkOS

Status: Provider-neutral foundation accepted; provider integrations proposed
Created: 2026-07-20
Scope: External member authentication, provisioning, tenant membership, site
authorization, and session lifecycle

Commerce production integration and delivery order are tracked in
[commerce-production-vertical-slice.md](commerce-production-vertical-slice.md).

## Outcome

Prepare AeroCMS to support external customer and partner members through
Microsoft Entra External ID and WorkOS without coupling CMS authorization to
either vendor.

ASP.NET Core Identity remains the local authentication and account-management
system for internal AeroCMS operators for the first implementation. External
members use a separate local principal model and authenticate through a managed
external identity provider.

The target design must:

- allow an AeroCMS tenant to select Entra External ID or WorkOS as its external
  identity authority;
- support a person who belongs to more than one AeroCMS tenant or site;
- preserve AeroCMS as the authority for tenant, site, role, and permission
  decisions;
- use stable provider identifiers instead of email addresses as identity keys;
- revoke access when a provider membership or directory user is deactivated;
- keep provider secrets and refresh tokens out of user documents and browser
  JavaScript;
- make event processing idempotent, replayable, and tolerant of out-of-order
  delivery; and
- preserve the existing long/Snowflake identifier convention.

The provider-neutral foundation is now implemented. It adds the isolated local
member cookie, local member/session/site-assignment documents, strict principal
validation, host-site authorization, and member session endpoints. It does not
add provider packages, provider tenants, secrets, callbacks, identity links,
organization bindings, invitations, or webhooks.

## Research corrections and current facts

### Microsoft terminology

Microsoft Entra External ID is not merely a rename of Azure AD B2C. It is
Microsoft's current CIAM platform for customer and business-customer
applications. Azure AD B2C stopped being available for new purchases on
2025-05-01, although existing B2C customers remain supported until at least May
2030.

Microsoft distinguishes:

- a **workforce tenant**, used for employees, internal resources, and B2B guest
  collaboration; and
- an **external tenant**, used for consumer and business-customer applications,
  user flows, and external application users.

For AeroCMS customer-facing membership, an **external tenant** is the default
Entra model. Workforce B2B collaboration is appropriate only when AeroCMS is
being exposed as an internal resource of the workforce tenant.

References:

- [Microsoft Entra External ID overview](https://learn.microsoft.com/entra/external-id/external-identities-overview)
- [Workforce and external tenant configurations](https://learn.microsoft.com/entra/external-id/tenant-configurations)
- [External ID FAQ and Azure AD B2C status](https://learn.microsoft.com/entra/external-id/customers/faq-customers)
- [Microsoft multitenant identity architecture](https://learn.microsoft.com/azure/architecture/guide/multitenant/approaches/identity)

### Pricing and scale

The External ID core offer is currently free for the first 50,000 monthly active
users. Premium add-ons do not inherit that free tier. Pricing is based on
authentication activity, not the total number of stored external members.

This should be treated as a procurement input rather than an architectural
guarantee. Pricing and add-on availability must be checked again before launch.

External tenants also have request limits. Current Microsoft documentation lists
20 requests per second per IP and 200 requests per second per external tenant;
trial tenants are limited to 20 requests per second. Load and recovery testing
must account for these limits instead of assuming that "millions of users" means
unlimited authentication throughput.

References:

- [External ID pricing and billing](https://learn.microsoft.com/entra/external-id/external-identities-pricing)
- [External ID service limits](https://learn.microsoft.com/entra/external-id/customers/reference-service-limits)

### WorkOS .NET SDK

The official package is `WorkOS.net`. At the time of this research, the current
repository release is 5.5.0 and supports .NET 8 or later, which is compatible
with AeroCMS's .NET 10 target.

The SDK provides typed services for AuthKit/User Management, Organizations,
SSO, Directory Sync, Events, Webhooks, and Audit Logs. It also includes URL
builders, code exchange, logout helpers, session/JWT support, idempotency keys,
and built-in retry behavior for selected transient failures.

The SDK should be constructor-injected behind an AeroCMS adapter. Application
code must not use the static `WorkOSConfiguration.WorkOSClient`.

There is one package-policy blocker: the current SDK documentation states that
its runtime uses Newtonsoft.Json when communicating with WorkOS, even though its
generated models also support System.Text.Json. AeroCMS has a System.Text.Json-
only rule. Before adopting the package, choose one of these explicitly:

1. Allow Newtonsoft.Json only as an encapsulated transitive implementation
   detail inside the WorkOS adapter; AeroCMS code continues to use
   System.Text.Json.
2. Keep the zero-Newtonsoft rule absolute and implement the required WorkOS
   endpoints through a small typed `HttpClient`/System.Text.Json adapter.
3. Re-evaluate a later WorkOS SDK version that no longer has the dependency.

References:

- [Official WorkOS .NET repository](https://github.com/workos/workos-dotnet)
- [WorkOS .NET SDK documentation](https://workos.com/docs/sdks/dotnet)
- [Generated .NET API reference](https://workos.github.io/workos-dotnet/)

## Current AeroCMS boundary

The accepted boundary now separates internal CMS operators from storefront
members:

- `Identity.Application` and `.AeroCms.Auth` remain the default internal
  authenticate, challenge, and sign-in scheme for `AeroUser` administrators and
  managers.
- `AeroCms.ExternalMember` and `.AeroCms.Member` are distinct and never become a
  default scheme. Generic manager authorization therefore cannot consume a
  storefront-member cookie.
- `ExternalMember`, `ExternalMemberSession`, and
  `ExternalMemberSiteAssignment` provide local Snowflake ownership, revocation,
  and site-membership state without creating an `AeroUser`.
- Every member-cookie request revalidates the local member and session and fails
  closed on malformed claims, revocation, expiry, stale security version, or
  datastore failure.
- Storefront site authorization uses the host-resolved `ISiteContext` tenant and
  site. It never trusts the manager-selected `AeroCms.SiteId` cookie.
- `/api/v1/member/me` and `/api/v1/member/logout` are isolated from
  `/api/v1/admin/auth/*`; logout clears only the member cookie and reports an
  explicit failure if server-side revocation cannot be persisted.
- `UserSiteAssignment`, `SitePermissionHandler`, and `/auth/me` remain internal
  manager concerns in this bounded slice. Broader principal-aware manager and
  audit refactors are deferred.
- `SitesModel` separates `TenantId` from `SiteId`. A future WorkOS organization
  or Entra customer organization binds at `TenantId`; local assignments select
  sites within that tenant.

Relevant code:

- `src/Aero.Cms.Modules.Identity/IdentityModule.cs`
- `src/Aero.Cms.Modules.Identity/IdentityApi.cs`
- `src/Aero.Cms.Web.Bootstrap/AeroCmsExtensions.cs`
- `src/Aero.Cms.Core.Entities/UserSiteAssignment.cs`
- `src/Aero.Cms.Modules.Sites/SitePermissionHandler.cs`
- `src/Aero.Cms.Core.Entities/SitesModel.cs`

## Proposed decisions

### 1. Keep authentication and authorization separate

Entra External ID and WorkOS prove who authenticated and report provider
membership state. AeroCMS remains authoritative for:

- the local principal ID;
- AeroCMS tenant and site access;
- the currently selected site;
- CMS roles and permissions;
- resource-level authorization; and
- audit attribution.

Provider roles, Entra groups, WorkOS organization roles, email domains, and token
claims must not directly grant `site:*` permissions. A synchronization policy
may translate them into local assignments, but authorization always reads the
local current state.

This preserves the existing site-policy model and prevents a provider
configuration change from silently becoming a CMS authorization change.

### 2. Add a provider-neutral external principal

Do not require external members to be `AeroUser` password accounts. Introduce
clean current-model documents similar to:

| Document | Purpose |
|---|---|
| `ExternalMember` | Local Snowflake principal, profile snapshot, lifecycle state, and security version |
| `ExternalIdentityLink` | Unique provider identity mapped to one local member |
| `ExternalOrganizationBinding` | Maps an AeroCMS `TenantId` to a provider organization/directory |
| `ExternalMemberSiteAssignment` | Grants an active local external member storefront access to a tenant/site pair |
| `ExternalMemberSession` | Revocable local member session with provider name, security version, and absolute expiry |
| `ExternalIdentityEventReceipt` | Deduplication and processing status for provider events |

All persisted domain documents use long Snowflake IDs. Provider IDs remain
opaque strings.

Suggested identity-link uniqueness:

```text
(Provider, Issuer, Subject) -> one ExternalMemberId
```

Suggested organization-binding uniqueness:

```text
(Provider, ExternalOrganizationId) -> one TenantId
(TenantId, Provider) -> one active binding
```

An identity link must never be created from email matching alone. Email is
mutable, may be recycled, and is not an authorization key. Linking two provider
identities requires one of:

- an authenticated member explicitly linking another provider;
- an unexpired invitation bound to the intended tenant;
- a verified administrator action; or
- a signed, provider-originated membership event whose organization is already
  bound to the tenant.

### 3. Replace user-only request assumptions with a local principal abstraction

The foundation adds an application-owned `ICurrentPrincipal`/`CurrentPrincipal`
abstraction for the strict external-member cookie. Its stable fields are:

- `PrincipalId` (`long`);
- `PrincipalKind` (`InternalUser` or `ExternalMember`);
- `AuthenticationProvider`;
- display name and verified-email snapshot;
- external session ID when applicable; and
- local security version.

The application cookie's `NameIdentifier` remains the local Snowflake
`PrincipalId`. Add explicit `principal_kind` and `auth_provider` claims.

Refactor these consumers before enabling a real Entra or WorkOS sign-in flow:

- `/auth/me`;
- site selection;
- `SitePermissionHandler`;
- audit actor resolution;
- manager authentication-state serialization; and
- logout.

The accepted bounded model keeps `UserSiteAssignment` internal-only and adds
`ExternalMemberSiteAssignment` for storefront membership. A future unified
principal-assignment model may replace both only when manager selection,
permissions, audit attribution, and UI can move atomically.

### 4. Use isolated local application sessions

Internal and external authentication paths use separate server-side cookies:

- internal operators: `Identity.Application` / `.AeroCms.Auth`;
- external members: `AeroCms.ExternalMember` / `.AeroCms.Member`.

This corrects the earlier shared-cookie proposal. The live manager surface has
many endpoints that use default `RequireAuthorization()`, so a shared default
cookie would allow a storefront member to satisfy the manager authentication
boundary before principal-kind checks ran.

The member cookie validation contract:

1. Require exactly one authenticated identity from the dedicated member scheme.
2. Resolve the local `PrincipalId`, provider, session ID, and security version.
3. Confirm the local member is active and the security version is current.
4. Confirm the local session owner/provider/version, expiry, and revocation
   state.
5. Check tenant/site membership through the host-site authorization policy.
6. Reject and clear the member cookie on malformed state or datastore failure.

Member logout:

1. attempts to revoke the owned local external session;
2. always clears `.AeroCms.Member`, including when persistence fails;
3. never clears `.AeroCms.Auth`; and
4. reports an explicit server error when local revocation was not persisted.

Provider-aware upstream logout is deferred until the Entra and WorkOS adapters
exist. Browser code must not receive provider refresh tokens or API keys.

### 5. Select one external authority per AeroCMS tenant

Do not expose Entra External ID and WorkOS as competing authorities for the same
customer tenant by default. That creates duplicate identities, conflicting
membership lifecycles, and ambiguous logout/revocation behavior.

Each `TenantId` selects one mode:

- `EntraExternalId`;
- `WorkOS`;
- `Disabled`; or
- a future explicitly designed migration/coexistence mode.

If an enterprise uses Microsoft Entra as its corporate IdP while AeroCMS uses
WorkOS, Entra should normally be configured upstream through WorkOS SSO or
Directory Sync. AeroCMS then integrates with one external control plane for that
tenant.

Temporary coexistence must be treated as a migration, with explicit identity
linking and a declared source of truth for membership.

## Microsoft Entra External ID integration

### Tenant and protocol choice

Use an Entra **external tenant** and a registered web application. External
tenant authorities use `ciamlogin.com`, not the workforce
`login.microsoftonline.com` authority.

Use server-side OpenID Connect authorization-code flow with PKCE. Do not
implement authentication as a public browser client and do not expose provider
tokens to Blazor WebAssembly.

Because AeroCMS will support more than one external provider, prefer a named
ASP.NET Core OpenID Connect scheme for Entra and keep provider-specific options
isolated. Microsoft recommends `Microsoft.Identity.Web` for Microsoft identity
providers, but ASP.NET Core documentation notes that the default OIDC handler is
normally safer when multiple OIDC provider clients share one application
because provider libraries can overwrite shared options. The implementation
spike must prove whichever option is selected.

References:

- [ASP.NET Core OIDC guidance](https://learn.microsoft.com/aspnet/core/security/authentication/configure-oidc-web-authentication?view=aspnetcore-10.0)
- [External ID ASP.NET Core setup](https://learn.microsoft.com/entra/identity-platform/tutorial-web-app-dotnet-prepare-app)
- [External-tenant token endpoints and issuers](https://learn.microsoft.com/entra/identity-platform/security-tokens)

### Entra identity key

Validate issuer, audience, signature, lifetime, nonce, and state before
provisioning.

Prefer:

```text
Provider = EntraExternalId
Issuer   = validated iss
Subject  = validated sub
```

If cross-application correlation inside the same Entra tenant is required,
store the validated `tid` and `oid` as additional provider metadata. Do not
assume `sub == oid`; Microsoft documents `sub` as pairwise per application.

Never use `email`, `preferred_username`, display name, or an unvalidated tenant
hint as the identity key or as proof of tenant membership.

### Entra onboarding

Start invite-only:

1. A tenant/site administrator creates a local pending invitation.
2. AeroCMS generates an Entra sign-in challenge with signed state that contains
   only a nonce/invitation reference and safe return path.
3. The callback validates the OIDC response.
4. AeroCMS resolves or creates the external member and identity link.
5. The invitation is atomically consumed and local site assignments are
   created.
6. AeroCMS issues the isolated local external-member cookie.

Self-service sign-up, domain auto-join, federation, and custom authentication
extensions remain later opt-in capabilities. Domain ownership must be verified
before domain-based membership is enabled.

## WorkOS integration

### Product boundary

Use WorkOS when an AeroCMS customer needs a managed B2B identity control plane,
especially:

- AuthKit-hosted sign-in;
- enterprise SAML/OIDC SSO;
- organizations and organization memberships;
- invitations and JIT provisioning;
- Directory Sync/SCIM lifecycle;
- customer IT self-service through Admin Portal; or
- provider-normalized events and reconciliation.

One WorkOS organization maps to one AeroCMS `TenantId`, not directly to a
`SiteId`. Local site assignments determine which sites within that tenant the
member may use.

WorkOS explicitly models users separately from organization memberships and
supports many-to-many membership. Membership can be pending, active, or
inactive. Deactivating a membership revokes its active WorkOS sessions. This
maps well to AeroCMS's separation of a tenant from its sites.

References:

- [WorkOS users and organizations](https://workos.com/docs/authkit/users-organizations)
- [WorkOS invitations](https://workos.com/docs/authkit/invitations)
- [WorkOS JIT provisioning](https://workos.com/docs/authkit/jit-provisioning)
- [WorkOS sessions](https://workos.com/docs/authkit/sessions)

### WorkOS login

1. Resolve the intended AeroCMS tenant before building the authorization URL.
2. Load the tenant's active WorkOS organization binding.
3. Generate and persist short-lived `state`; use PKCE where appropriate.
4. Redirect through the SDK's AuthKit/SSO URL builder.
5. On callback, validate state and exchange the code server-side.
6. Require the returned WorkOS organization/membership to match the bound
   AeroCMS tenant.
7. Resolve or create `(WorkOS, issuer, WorkOS user ID)`.
8. Apply local membership/assignment policy.
9. Issue the isolated local AeroCMS external-member cookie.

WorkOS access tokens contain `sub`, `sid`, and—when an organization is
selected—`org_id`, role, and permissions. AeroCMS may use these as validated
provider inputs, but the local database remains authoritative for CMS
authorization.

Keep WorkOS access-token duration short. If refresh tokens are retained, store
them only in an encrypted server-side session or secure HTTP-only cookie and
replace rotated refresh tokens after use.

### Directory Sync and webhooks

Use WorkOS events to synchronize:

- users;
- organization memberships;
- membership activation/deactivation;
- directories and directory users;
- groups and group memberships; and
- connection/organization lifecycle needed by AeroCMS.

The webhook endpoint must:

1. read the raw request body;
2. verify the `WorkOS-Signature` timestamp and signature with the endpoint
   secret;
3. persist the provider event ID and payload in an inbox;
4. return `200` quickly; and
5. dispatch durable background processing through Wolverine.

The processor must:

- be idempotent by provider event ID;
- accept duplicate and out-of-order events;
- compare provider `updated_at` values before overwriting newer state;
- upsert complete provider objects;
- disable access immediately on deactivation;
- separate state projection from one-time side effects;
- retain failures with retry visibility; and
- support event replay and periodic/full reconciliation.

WorkOS recommends queueing webhook work, responding immediately, handling
duplicate and out-of-sequence events, and maintaining a reconciliation path.

References:

- [WorkOS webhook synchronization](https://workos.com/docs/events/data-syncing/webhooks)
- [WorkOS data reconciliation](https://workos.com/docs/events/data-syncing/data-reconciliation)
- [WorkOS Directory Sync](https://workos.com/docs/directory-sync)

## Authorization mapping

External authentication success never implies site access.

The minimum authorization chain is:

```text
validated provider identity
  -> active local ExternalMember
  -> active provider organization binding for TenantId
  -> local ExternalMemberSiteAssignment for the host-resolved SiteId
  -> resource ownership check
```

Rules:

- A selected site must belong to the tenant represented by the active external
  organization binding.
- Provider organization IDs must never be accepted directly from a browser as
  authorization proof.
- WorkOS role/permission claims and Entra group/app-role claims are inputs to an
  explicit mapping policy, not direct CMS permissions.
- Removing or deactivating an external membership disables local assignments
  according to the tenant's lifecycle policy.
- An external member never receives the internal `Admin` bypass by default.
- Cross-tenant identifiers remain concealed using the established `404` rule.

## Configuration and secret boundaries

Suggested configuration shape:

```text
AeroCms:ExternalMembers:Enabled
AeroCms:ExternalMembers:CookieLifetime
AeroCms:ExternalMembers:InviteOnly

AeroCms:ExternalMembers:Entra:Enabled
AeroCms:ExternalMembers:Entra:Authority
AeroCms:ExternalMembers:Entra:ClientId
AeroCms:ExternalMembers:Entra:CallbackPath

AeroCms:ExternalMembers:WorkOS:Enabled
AeroCms:ExternalMembers:WorkOS:ClientId
AeroCms:ExternalMembers:WorkOS:CallbackPath
```

Secrets are not stored in ordinary configuration documents:

- Entra client credential/certificate;
- WorkOS API key;
- WorkOS webhook signing secret;
- provider refresh tokens; and
- any session-encryption keys.

Production secrets belong in the configured external secret store. Provider
clients use `IHttpClientFactory`, bounded timeouts, cancellation, structured
retry policy, and OpenTelemetry instrumentation without logging tokens,
authorization codes, email addresses, or raw webhook payloads.

## Failure and consistency model

Provider API calls and the AeroCMS database cannot share a transaction.

Use these rules:

- Local authorization state changes commit atomically in Sable.
- Outbound provider operations use stable idempotency keys where supported.
- Inbound provider events enter an idempotent inbox before projection.
- A failed audit/event publish after a local commit must not make the caller
  believe the commit failed.
- Provisioning workflows record explicit pending/succeeded/failed states.
- Deprovisioning fails closed: uncertain provider membership disables new
  sessions until reconciliation succeeds.
- Sign-in availability and already-issued local session availability are
  separate failure domains.

## Delivery phases

### Phase 0 — Architectural decisions

- [ ] Decide whether the WorkOS SDK's internal Newtonsoft.Json dependency gets
  a narrow exception.
- [ ] Confirm one external authority per `TenantId`.
- [x] Confirm external members remain separate from `AeroUser`.
- [ ] Confirm invite-only onboarding as the default.
- [ ] Define external-session and revocation latency targets.
- [ ] Define which provider roles/groups, if any, map into local site
  assignments.

### Phase 1 — Provider-neutral principal foundation

- [x] Add the narrow storefront `ExternalMember`, `ExternalMemberSession`, and
  local external-member/site-membership models.
- [x] Keep `UserSiteAssignment` internal-only and add
  `ExternalMemberSiteAssignment` for the storefront foundation. This is an
  intentional additive deviation: the existing manager assignment API and
  selected-site cookie remain unchanged until a complete principal-aware
  replacement can be delivered safely.
- [x] Add `ICurrentPrincipal` for strict external-member claims.
- [x] Add a distinct `.AeroCms.Member` cookie under the non-default
  `AeroCms.ExternalMember` scheme. Manager/default `Identity.Application` and
  `.AeroCms.Auth` remain internal-only.
- [x] Add local member/session validation on every external-cookie request and
  host-site membership authorization that never reads `AeroCms.SiteId`.
- [x] Add non-admin `GET /api/v1/member/me` and `POST /api/v1/member/logout`.
- [ ] Refactor `/auth/me`, site selection, audit attribution, and
  `SitePermissionHandler`.
- [ ] Add provider identity links, organization bindings, receipts, and
  provider callbacks when Entra/WorkOS integration begins.
- [ ] Add invitation state and one-time consumption.

Exit criterion for the bounded foundation: a test-only cookie harness can issue
an external principal that is authorized entirely through local tenant/site
assignments without creating an `AeroUser`. This criterion is met. Real
provider callbacks remain later phases.

Current foundation note: this slice intentionally does not introduce a test
login endpoint or a provider callback. The principal factory is available to a
future validated provider adapter; no external cookie is issued by production
routes yet. Setup continues to configure Local Identity only for CMS
administrators and managers; external providers are configured per tenant.

### Phase 2 — Entra External ID

- [ ] Register a named external-tenant OIDC scheme.
- [ ] Add login, callback, remote-failure, and provider-aware logout endpoints.
- [ ] Implement issuer/subject identity resolution and invite consumption.
- [ ] Add Entra tenant/application setup documentation.
- [ ] Add token-validation, state, nonce, PKCE, linking, revocation, and
  cross-tenant tests.

### Phase 3 — WorkOS AuthKit and SSO

- [ ] Resolve the SDK dependency decision.
- [ ] Add a constructor-injected WorkOS adapter.
- [ ] Implement tenant-to-organization binding.
- [ ] Add AuthKit/SSO login, callback, organization selection, refresh, and
  logout.
- [ ] Add invitation and membership synchronization.
- [ ] Verify provider organization IDs against local bindings on every callback.

### Phase 4 — Directory lifecycle

- [ ] Add signed webhook ingestion and durable inbox processing.
- [ ] Synchronize directory users, groups, and memberships.
- [ ] Implement immediate local deactivation and session revocation.
- [ ] Add replay, reconciliation, dead-letter visibility, and operator repair
  tooling.
- [ ] Add WorkOS Admin Portal onboarding if required.

### Phase 5 — Production hardening

- [ ] Threat-model login CSRF, callback mix-up, account linking, invitation
  theft, tenant confusion, session fixation, replay, and deprovisioning races.
- [ ] Run two-tenant IDOR and forged-organization tests.
- [ ] Load-test Entra and WorkOS rate-limit behavior.
- [ ] Verify multi-instance Data Protection key sharing.
- [ ] Verify secret rotation and provider credential rollover.
- [ ] Add provider health, callback failure, webhook lag, reconciliation drift,
  and deactivation-latency telemetry.
- [ ] Complete privacy retention/export/deletion policies for external profiles
  and event payloads.

## Required tests

### Identity and linking

- same issuer/subject resolves the same external member;
- same email with a different issuer/subject does not auto-link;
- identity link uniqueness is race-safe;
- expired or consumed invitations cannot be reused;
- callback state cannot be moved between tenants/providers;
- an unbound provider organization cannot create local access.

### Tenant and site isolation

- an external member assigned to tenant A cannot select a site in tenant B;
- a WorkOS `org_id` for tenant A cannot authorize a tenant B callback;
- provider role/group claims do not bypass local `site:*` assignments;
- external principals never acquire the internal Admin bypass implicitly;
- switching sites rechecks current local assignment state.

### Sessions

- disabled/deprovisioned members cannot create or refresh sessions;
- local security-version change invalidates an existing cookie;
- WorkOS logout clears local and upstream sessions;
- Entra logout clears local and upstream sessions;
- internal Identity logout remains unchanged;
- `/auth/me` resolves both principal kinds.

### Events and reconciliation

- invalid signatures and stale timestamps are rejected;
- duplicate events are acknowledged once and projected once;
- old out-of-order events cannot overwrite newer state;
- deactivation revokes access even when delivery is retried;
- replay rebuilds projections without repeating one-time side effects;
- full reconciliation detects local members missing upstream.

## Alternatives considered

### Store external members as passwordless `AeroUser` records

This minimizes changes to `/auth/me`, `SignInManager`, and
`UserSiteAssignment`. It also makes ASP.NET Core Identity the local account
store for both internal and external users.

Rejected for the proposed design because the stated boundary keeps ASP.NET
Identity internal for now, and because external lifecycle/session state differs
from local password/security-stamp state. This alternative can be reconsidered
if a single Identity account store is explicitly preferred over the separate
principal model.

### Trust provider roles and permissions directly

This reduces local synchronization work.

Rejected because AeroCMS site permissions and resource ownership are
application-domain rules. Direct trust also creates provider lock-in and makes
cross-provider behavior inconsistent.

### Enable both providers for every tenant

This offers maximum login choice.

Rejected as the default because identity linking, membership authority,
deprovisioning, and logout become ambiguous. Provider coexistence is allowed
only as an explicit migration mode.

### Build SAML and SCIM directly

This avoids a WorkOS dependency.

Deferred. Direct implementation creates substantial protocol, compatibility,
security, reconciliation, and support obligations. It is justified only if
WorkOS cost, dependency policy, hosting requirements, or customer constraints
outweigh those obligations.

## Open decisions for implementation

1. Is the WorkOS SDK allowed a narrow Newtonsoft.Json transitive-dependency
   exception, or must the adapter use direct System.Text.Json HTTP calls?
2. Does every WorkOS organization map one-to-one to `TenantModel`, including
   tenants with multiple sites?
3. Are invitations required for every first membership, or may verified-domain
   JIT provisioning be enabled per tenant?
4. Which local site permissions may be derived from WorkOS roles, Entra app
   roles, directory groups, or custom attributes?
5. What is the maximum acceptable delay between upstream deactivation and local
   access revocation?
6. Must an external member be allowed to belong to tenants that use different
   providers?

## Definition of ready

Implementation can start when:

- Phase 0 decisions are answered;
- the principal and assignment schema is accepted;
- the WorkOS dependency decision is recorded;
- Entra and WorkOS development environments are available;
- callback/logout/webhook URLs are reserved;
- secret storage and Data Protection key sharing are configured; and
- the two-tenant security test matrix is approved.
