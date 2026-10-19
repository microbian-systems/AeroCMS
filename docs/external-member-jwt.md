# External member bearer access

`POST /api/v1/member/local/token` is disabled unless `AeroCms:ExternalMemberJwt:Enabled=true`.
It reuses invitation-gated local member credentials and creates a short-lived access token only; it does not create a cookie or refresh token. The endpoint is rate limited and returns generic failures.

The member bearer scheme is `AeroCms.ExternalMemberBearer`. It accepts only the CMS-issued principal-kind and token-use discriminators, rejects manager/service claims and rechecks the enabled site, tenant/site scope, member, identity link, assignment, session revocation/expiry, and security version on every request. Roles and permissions are deliberately not token claims.

Assignments are site scoped. `GeneralUser` can request `paid-analysis.request`; `PowerUser` and `CustomerAdmin` can also use `paid-analysis.approve`; only `CustomerAdmin` can use `member.users.manage`. The approval permission is workflow eligibility only: dollar, provider, tenant, and per-user budget enforcement belongs to the ContractorAI host integration and is not enforced by AeroCMS.

Platform `Admin` remains the recovery/bootstrap boundary. The first CustomerAdmin is assigned through the manager API (`PUT /api/v1/admin/external-members/{memberId}/role`) in its selected site; CustomerAdmin updates use `PUT /api/v1/member/local/assignments/{memberId}/role` and cannot cross tenant/site boundaries. Assignment updates use Sable optimistic concurrency.

Do not enable direct member tokens in production while signing keys are process-local/in-memory. A durable key-store and production key-management review are required first.

## Cancellation safety

Caller cancellation during access-token issuance is propagated without revoking
the otherwise-valid session. Request cancellation during bearer live-state
validation is propagated instead of being reported as an invalid token.
Unrelated cancellation and ordinary internal failures retain the existing
fail-closed behavior; issuance failures still revoke the session.

The 2026-10-18 ContractorAI stock-Wolverine verification reproduced both bugs
before the repair: 2 failures and 15 passing controls across 17 TUnit cases.
The same unchanged tests pass 17/17 after the two guarded catch filters, with
zero skips. This is source-graph verification, not a published-package or live
authentication acceptance claim. No authentication scope was broadened.
