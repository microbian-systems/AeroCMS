# Manager and Storefront Member Authentication

Status: Local manager recovery, manager federation, local storefront member
authentication, and direct Entra External ID and WorkOS member adapters are
implemented. Production provider credentials remain blocked on Aero.Vault Host
and client integration.

This guide describes the current code. It covers initial selection, development
credential setup, provider dashboard configuration, and smoke testing.

## Authentication boundaries

AeroCMS has two independent authentication realms:

| Realm | Users | Providers | Cookie |
|---|---|---|---|
| Manager | CMS administrators and managers (`AeroUser`) | Local ASP.NET Core Identity, Entra Workforce, or WorkOS | `.AeroCms.Auth` |
| Storefront member | Customers and partners (`ExternalMember`) | Disabled, local AeroCMS credentials, Entra External ID, or WorkOS | `.AeroCms.Member` |

The member cookie is never a manager authentication scheme. Manager federation
also cannot consume the member cookie. The local recovery administrator uses a
third, short-lived `.AeroCms.ManagerRecovery` cookie through
`/manager/recovery`.

Storefront authorization remains local to AeroCMS. Entra and WorkOS prove an
identity, but AeroCMS decides the tenant, site, assignment, session, and access.
Provider email addresses, groups, domains, roles, and permissions do not grant
an AeroCMS site assignment by themselves.

All persisted AeroCMS identities and authentication records use long Snowflake
IDs. Provider tenant, organization, subject, and session identifiers remain
opaque strings rather than primary keys.

## Setup wizard selection

The Authentication step in `Setup.razor` offers two views:

- The simple view selects a provider family. Local maps to local managers and,
  when storefront members are enabled, local members. Microsoft Entra maps to
  Entra Workforce managers and Entra External ID members. WorkOS maps to WorkOS
  for both realms. Disabling storefront members persists `disabled` regardless
  of the selected manager family.
- Advanced exposes independent manager and storefront-member choices. The
  manager choice is required. The member choice may be Disabled, Local,
  Microsoft Entra External ID, or WorkOS. The Advanced presentation preference
  is not persisted; only the two canonical provider choices are stored.

Setup always creates one local recovery administrator. A remote manager choice
is initially only requested intent: ordinary manager authentication remains
local until the exact authority is configured, the recovery administrator is
linked to the matching remote identity, and that link produces durable
verification and activation evidence. After activation, remote manager login is
effective and `/manager/recovery` remains the break-glass path.

Selecting Local for storefront members creates or reuses one active local
authority for the initial tenant. Local accounts are invitation-only. Managers
issue an invitation handle, the customer activates it with an email and a new
password, and AeroCMS stores only the password hash. Managers also issue
one-time password-reset handles. There is no public reset-request workflow.

## Development HTTPS and callback origin

Complete this prerequisite before configuring any remote provider.

Manager callbacks use the configured `PublicOrigin`; storefront callbacks use
the host that resolved the current AeroCMS site. Both paths enforce all of the
following:

- exact `https`;
- the default HTTPS port, 443;
- no credentials, query, or fragment in the origin;
- a valid, lowercase host; and
- an exact callback path.

Consequently, the repository's usual `https://localhost:333` development URL
and a conventional `https://localhost:5001` URL cannot be federation callback
origins. Do not register either one and expect this implementation to accept it.

Use a trusted development hostname served publicly on HTTPS port 443. Put
AeroCMS behind a reverse proxy or secure tunnel that terminates trusted TLS on
443 and preserves the public HTTPS host when forwarding to the local process.
Then:

1. Add the exact lowercase development hostname as the AeroCMS site's primary
   host or alias.
2. Confirm `https://<dev-host>/shop/account` resolves the intended site.
3. For manager federation, enter exactly `https://<dev-host>` as
   `PublicOrigin`, with no trailing slash or port.
4. Register the exact callback URLs below in the provider dashboard.

The manager `PublicOrigin` and authority identity become immutable after
verification/activation. Choose the final test hostname before linking the
recovery administrator. Storefront callbacks are host-derived, so register a
callback for every storefront host that will be tested.

## Exact callback URLs

Replace `<manager-host>` and `<store-host>` with the trusted HTTPS host on port
443. Paths and provider slugs are case-sensitive.

| Mode | Exact redirect URI |
|---|---|
| Manager: Entra Workforce | `https://<manager-host>/api/v1/admin/auth/callback/entra-workforce` |
| Manager: WorkOS | `https://<manager-host>/api/v1/admin/auth/callback/workos` |
| Member: Entra External ID | `https://<store-host>/api/v1/member/callback` |
| Member: WorkOS | `https://<store-host>/api/v1/member/callback` |

The two member providers intentionally share one provider-neutral callback.
Protected server state selects the provider; no provider name in the callback
path or browser input is trusted.

## Development provider credentials

The Web project has the `Aero.Cms.Web` user-secrets ID. Development credentials
are read only when the process environment is `Development` **and** the
realm-specific opt-in flag is `true`. Use placeholder values below, never real
credentials in source control or this document.

Run commands from the repository root:

```powershell
# Opt in to the development-only manager secret source.
dotnet user-secrets set --project src/Aero.Cms.Web/Aero.Cms.Web.csproj "AeroCms:Authentication:ManagerFederation:EnableDevelopmentProviderSecrets" "true"

# Manager - Microsoft Entra Workforce.
dotnet user-secrets set --project src/Aero.Cms.Web/Aero.Cms.Web.csproj "AeroCms:Authentication:ManagerFederation:DevelopmentSecrets:entra_workforce:ClientId" "<manager-entra-client-id>"
dotnet user-secrets set --project src/Aero.Cms.Web/Aero.Cms.Web.csproj "AeroCms:Authentication:ManagerFederation:DevelopmentSecrets:entra_workforce:ClientSecret" "<manager-entra-client-secret>"

# Manager - WorkOS.
dotnet user-secrets set --project src/Aero.Cms.Web/Aero.Cms.Web.csproj "AeroCms:Authentication:ManagerFederation:DevelopmentSecrets:workos:ClientId" "<manager-workos-client-id>"
dotnet user-secrets set --project src/Aero.Cms.Web/Aero.Cms.Web.csproj "AeroCms:Authentication:ManagerFederation:DevelopmentSecrets:workos:ApiKey" "<manager-workos-api-key>"

# Opt in to the development-only storefront-member secret source.
dotnet user-secrets set --project src/Aero.Cms.Web/Aero.Cms.Web.csproj "AeroCms:Authentication:ExternalMembers:EnableDevelopmentProviderSecrets" "true"

# Member - Microsoft Entra External ID.
dotnet user-secrets set --project src/Aero.Cms.Web/Aero.Cms.Web.csproj "AeroCms:Authentication:ExternalMembers:DevelopmentSecrets:entra_external_id:ClientId" "<member-entra-client-id>"
dotnet user-secrets set --project src/Aero.Cms.Web/Aero.Cms.Web.csproj "AeroCms:Authentication:ExternalMembers:DevelopmentSecrets:entra_external_id:ClientSecret" "<member-entra-client-secret>"

# Member - WorkOS.
dotnet user-secrets set --project src/Aero.Cms.Web/Aero.Cms.Web.csproj "AeroCms:Authentication:ExternalMembers:DevelopmentSecrets:workos:ClientId" "<member-workos-client-id>"
dotnet user-secrets set --project src/Aero.Cms.Web/Aero.Cms.Web.csproj "AeroCms:Authentication:ExternalMembers:DevelopmentSecrets:workos:ApiKey" "<member-workos-api-key>"
```

The manager and member namespaces are deliberately separate. Add only the keys
for the providers being exercised. Secret values must be nonempty, trimmed,
bounded UTF-8 values. The non-secret authority forms still require a positive
Aero.Vault ID and a canonical environment such as `development`; these identify
the approved credential reference even when the Development source supplies
the bytes.

In every non-Development environment, both development secret sources are
disabled regardless of configuration. The production source currently returns
Unavailable and fails closed because the deployable Aero.Vault Host and typed
client are not yet integrated.

## Configure Microsoft Entra Workforce managers

Use a workforce tenant and a single-tenant web application for internal CMS
operators. Follow Microsoft's [app registration
quickstart](https://learn.microsoft.com/entra/identity-platform/quickstart-register-app).

1. In the intended workforce tenant, register a web application whose supported
   account type is accounts in this organizational directory only.
2. Add the exact manager Entra callback URI from the table as a Web redirect
   URI.
3. Create a client secret for development testing. Record its **value** at
   creation time, not its identifier.
4. Record the Application (client) ID and Directory (tenant) ID. AeroCMS requires
   the tenant ID as a lowercase canonical GUID.
5. Add the recovery administrator's Entra account to the tenant/application as
   required by the tenant's assignment policy. The authorization request uses
   `openid profile`, authorization code flow, query response mode, nonce, and
   PKCE S256.
6. Put the client ID and secret in the manager user-secret keys above.
7. In Setup, select Microsoft Entra for managers. After Setup, sign in locally,
   open `/manager/authentication`, and enter:

   - Organization identifier: the lowercase Directory (tenant) ID;
   - Canonical authority:
     `https://login.microsoftonline.com/<tenant-id>/v2.0`;
   - Public AeroCMS origin: `https://<manager-host>`;
   - a positive Aero.Vault ID and canonical environment, for example
     `development`.

8. Save the authority. While signed in as the exact recovery administrator,
   choose **Link recovery administrator and activate**, then authenticate with
   the intended Entra account.

The callback validates the exact tenant (`tid`), issuer, client audience,
subject, nonce, signature, lifetime, and link. A different tenant account or a
different local administrator cannot activate the authority.

## Configure Microsoft Entra External ID members

Use an Entra **external tenant**, not the workforce tenant. Microsoft's
[External ID web-app prerequisites and user-flow
guide](https://learn.microsoft.com/entra/identity-platform/quickstart-web-app-sign-in#prerequisites)
describes the tenant, app registration, and sign-up/sign-in flow.

1. Create or select an external tenant.
2. Register a web application in that external tenant and add the exact member
   Entra callback URI from the table as a Web redirect URI.
3. Create a client secret and record the client ID, secret value, lowercase
   external tenant ID GUID, and the external tenant's lowercase
   `<tenant-name>.ciamlogin.com` label.
4. Create a sign-up/sign-in user flow, select Email as an identity method, add
   the application to it, and select the available email and display-name token
   claims. AeroCMS requests `openid profile email` and its current adapter
   requires exact `email` plus `email_verified: true` claims for an
   invitation-gated first link. Confirm those exact claim names in a staging ID
   token. If the External ID flow emits `emails` or omits `email_verified`, the
   first link will fail closed and the adapter needs an explicit claim-mapping
   change before this smoke test can pass.
5. Put the client ID and client secret in the member Entra user-secret keys.
6. Select Microsoft Entra External ID for members in Setup, or open
   `/manager/external-members` for the selected site and enter:

   - Provider: Microsoft Entra External ID;
   - Organization identifier: the lowercase external tenant ID GUID;
   - Authority:
     `https://<tenant-name>.ciamlogin.com/<tenant-id>/v2.0`;
   - a positive Aero.Vault ID and `development` environment;
   - Enabled: checked.

7. On `/manager/external-members`, issue an invitation for the exact customer
   email and copy the one-time handle.
8. On that storefront host, open `/shop/account`, enter the handle, and continue
   to Entra. Complete the external user flow with the invited email.

The first callback requires a valid invitation and matching verified email.
The issuer is validated as
`https://<tenant-id>.ciamlogin.com/<tenant-id>/v2.0`; the configured authority
host may use the external tenant's canonical lowercase label. Returning members
with an active local link and site assignment can sign in without another
invitation. Entra self-sign-up does not itself grant AeroCMS membership.

## Configure WorkOS managers and members

Use a [WorkOS staging
environment](https://workos.com/docs/authkit/environments) for this test. Obtain
the [API key and client
ID](https://workos.com/docs/reference/api-authentication), and configure exact
[redirect URIs](https://workos.com/docs/reference/authkit/redirect-uri).

Manager and member credential references and callback routes are separate.
Separate WorkOS applications/configurations and organizations are recommended
for testing the two trust realms independently; the code does not require the
two organization IDs to be equal. If one WorkOS application is deliberately
shared, register both exact redirect URIs and keep the AeroCMS manager/member
secret namespaces separate.

For each realm:

1. Configure AuthKit in the WorkOS staging environment.
2. Register that realm's exact callback URL.
3. Create an organization and record its exact `org_...` organization ID.
4. Create or invite a test user and add an active organization membership. See
   [WorkOS users and
   organizations](https://workos.com/docs/authkit/users-organizations).
5. Record the staging client ID and secret API key, then add them to the matching
   manager or member user-secret keys. AeroCMS sends the API key server-side
   during code exchange; it is never returned to the browser.

For managers, select WorkOS in Setup. At `/manager/authentication`, configure:

- Organization identifier: the exact WorkOS organization ID;
- Authority: `https://api.workos.com` (fixed and read-only in the UI);
- Public origin: `https://<manager-host>`;
- a positive Aero.Vault ID and `development` environment.

Save, then have the exact recovery administrator link the WorkOS user and
activate the authority. WorkOS must return the configured organization ID; an
impersonated response is rejected.

For members, select the intended manager site, open
`/manager/external-members`, and configure:

- Provider: WorkOS;
- Organization identifier: the exact member WorkOS organization ID;
- Authority: `https://api.workos.com`;
- a positive Aero.Vault ID and `development` environment;
- Enabled: checked.

Issue an AeroCMS invitation for the WorkOS user's verified email. On the correct
storefront host, open `/shop/account`, enter the one-time handle, and continue
to WorkOS. WorkOS must return the exact configured organization and a verified
email. A returning, linked member no longer needs the AeroCMS invitation.

## Local storefront member workflow

1. Select Local for storefront members during Setup. This seeds the tenant-wide
   local authority.
2. Select the intended site in the manager and open
   `/manager/external-members`.
3. Issue an invitation for the customer email. Expiry must be in the future and
   no more than seven days away. Copy the handle when shown; AeroCMS persists
   only its digest and does not show it again.
4. On that site's host, open `/shop/account`. Under **Activate an invitation**,
   enter the handle, exact invited email, optional display name, and a password
   from 12 through 256 characters.
5. Subsequent sign-in uses the local email/password form. Five failed attempts
   lock the credential for 15 minutes. A successful login issues an eight-hour
   storefront session.
6. To reset a password, the manager enters the member's Snowflake ID on
   `/manager/external-members` and issues a handle. The UI currently chooses a
   one-hour expiry. Deliver it through a trusted channel immediately.
7. The member completes **Complete a password reset** on `/shop/account` with a
   new 12-to-256-character password. Completion consumes the handle, bumps the
   security version, revokes existing storefront sessions, clears the member
   cookie, and does not automatically sign the member back in.

Invitation and reset handles are bearer secrets. Do not put them in logs,
tickets, analytics, or long-lived notes.

## Smoke-test checklist

Run each provider in a fresh development data set or with an authority state
that matches the selected provider. Authority provider/organization identity is
immutable once bound; local and remote active authorities cannot coexist for a
tenant.

### Manager: Entra Workforce or WorkOS

1. Before authority configuration, verify `/manager/login` still offers local
   sign-in and the manager Authentication page reports the remote selection as
   pending.
2. Configure the exact origin/authority and try linking as a non-recovery
   administrator. Expected: activation fails.
3. Link as the recovery administrator with the correct provider user and
   organization. Expected: callback returns to the manager, the authority is
   verified/active, and the effective provider becomes remote.
4. Sign out and use the remote manager login. Expected: a `.AeroCms.Auth`
   session is issued; no `.AeroCms.Member` cookie is issued.
5. Change the callback host, scheme, port, path, state, tenant/organization, or
   provider account. Expected: sign-in fails closed and no cookie is issued. A
   request that still reaches the valid callback route returns the manager
   login error state; a different route may simply return not found.
6. Disable the manager user or remove all CMS roles, then retry or reuse the
   cookie. Expected: manager authentication fails.
7. Remove the development secret or make the provider unavailable. Open
   `/manager/recovery`, sign in with the original local recovery credentials,
   and verify recovery access still works. The recovery cookie is nonpersistent,
   non-sliding, and lasts at most 15 minutes.
8. Log out, then retry a protected manager page. Expected: the local manager
   cookie is cleared and the durable federated session is revoked when
   persistence is available.

### Member: Entra External ID or WorkOS

1. On the selected site, configure and enable exactly one remote authority and
   issue an invitation.
2. On `/shop/account`, start sign-in with that handle and the matching verified
   provider email. Expected: callback consumes the invitation, creates the
   provider link/site assignment/local session, and issues only
   `.AeroCms.Member`.
3. Replay the invitation. Expected: it is rejected. A normal returning login
   without the invitation should still succeed for the already linked member.
4. Start the flow on one site host and send the callback to an alias for another
   tenant/site, a different host, HTTP, or a non-443 port. Expected: callback is
   rejected; no cookie is issued.
5. For WorkOS, use a user from a different organization. For Entra, use a token
   from a different tenant. Expected: callback is rejected.
6. Present the member cookie to a manager route. Expected: it does not satisfy
   manager authorization.
7. Log out from the original site. Expected: the local session is revoked and
   `.AeroCms.Member` is cleared. Provider logout is best effort after local
   revocation. Reusing the old cookie must fail.
8. Try the old cookie on another tenant or a site without the assignment.
   Expected: host/site authorization rejects it.

### Local members

1. Activate a valid invite on its site. Expected: success and a member cookie.
2. Replay the invite, alter its email, or submit it on another site/tenant.
   Expected: the same generic failure and no cookie.
3. Log out and verify the old session cannot be reused.
4. Issue a reset, complete it once, and verify all previous sessions fail.
5. Replay the reset or submit it on another site/tenant. Expected: generic
   failure, no password change, and no automatic login.
6. Enter five incorrect passwords. Expected: the credential locks for 15
   minutes and valid credentials do not bypass the active lockout.

For all modes, also verify that missing provider secrets, duplicate authorities,
datastore errors, malformed antiforgery submissions, and unresolved site hosts
fail closed without exposing provider keys, invitation digests, reset digests,
or whether a particular email exists.

## Implemented status and current limitations

Implemented and directly testable from the current UI/code:

- simple and Advanced Setup selections;
- immutable, pending-to-active manager federation with Entra Workforce and
  WorkOS;
- a permanent local recovery-administrator route;
- isolated manager, recovery, and storefront cookies;
- tenant-scoped Entra External ID and WorkOS authorities;
- invitation-gated provider linking and provider-neutral member callback;
- local storefront invitation activation, login, lockout, manager-issued reset,
  and session revocation;
- `/manager/authentication`, `/manager/external-members`, and `/shop/account`;
- antiforgery and rate limiting on browser mutation/login paths; and
- host-, tenant-, site-, organization-, state-, nonce-, and PKCE-bound flows.

Not production-ready or not implemented:

- Production provider credentials require Aero.Vault and fail closed. The
  deployable Aero.Vault Host and typed client integration is not yet present.
- Entra External ID staging must prove that the configured user flow emits the
  exact `email` and `email_verified` claims required for invitation matching;
  otherwise the adapter needs a claim-mapping update.
- Local reset handles are manually delivered by a manager. There is no public
  forgot-password request or email delivery.
- Public self-service AeroCMS signup, passkeys, and member MFA are not present.
  A provider may offer its own authentication factors, but AeroCMS does not yet
  manage them as a storefront feature.
- Provider webhooks, directory reconciliation, automatic deprovisioning, and
  provider role/group mapping are not present.
- The source contains focused unit/integration coverage for the security
  boundaries, but clean full-solution build and real-provider browser E2E runs
  remain release gates. Complete the smoke tests above with real staging tenants
  before treating any remote mode as verified for deployment.

## Provider references

- [Microsoft Entra app registration](https://learn.microsoft.com/entra/identity-platform/quickstart-register-app)
- [Microsoft Entra External ID web-app prerequisites](https://learn.microsoft.com/entra/identity-platform/quickstart-web-app-sign-in#prerequisites)
- [WorkOS staging and production environments](https://workos.com/docs/authkit/environments)
- [WorkOS API authentication](https://workos.com/docs/reference/api-authentication)
- [WorkOS redirect URIs](https://workos.com/docs/reference/authkit/redirect-uri)
- [WorkOS users and organizations](https://workos.com/docs/authkit/users-organizations)
