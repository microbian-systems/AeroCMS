using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Aero.Core;
using Aero.Core.Railway;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Core.Entities;
using AeroDB.Sable;
using System.Globalization;
using System.Security.Claims;

namespace Aero.Cms.Modules.Identity;

public static class ExternalIdentityAdminApi
{
    public static void MapExternalIdentityAdminApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/admin/external-identity/authority", GetAuthorityAsync)
            .RequireAuthorization("AeroAdmin");

        endpoints.MapPut("/api/v1/admin/external-identity/authority", async (
            [FromBody] ConfigureExternalIdentityAuthorityRequest request,
            [FromServices] IExternalIdentityManagerScopeResolver scopeResolver,
            [FromServices] IExternalIdentityAuthorityService service,
            CancellationToken ct) =>
        {
            if (request.AdditionalProperties is { Count: > 0 })
                return Results.BadRequest(new { message = "External authority request is invalid." });
            var scope = await scopeResolver.ResolveAsync(ct);
            if (scope is not Result<ExternalIdentityManagerScope, AeroError>.Ok(var resolved)) return Results.NotFound();
            var result = await service.ConfigureAsync(resolved, request, ct);
            return result switch
            {
                Result<ExternalIdentityAuthorityResult, AeroError>.Ok(var value) => Results.Ok(ToSafeState(value)),
                Result<ExternalIdentityAuthorityResult, AeroError>.Failure(AeroError.Validation) => Results.BadRequest(new { message = "External authority request is invalid." }),
                Result<ExternalIdentityAuthorityResult, AeroError>.Failure(AeroError.Conflict) => Results.Conflict(new { message = "External authority conflicts with the existing binding." }),
                Result<ExternalIdentityAuthorityResult, AeroError>.Failure(AeroError.NotFound) => Results.NotFound(new { message = "Site not found." }),
                _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "External authority could not be configured.")
            };
        }).RequireAuthorization("AeroAdmin")
            .WithMetadata(new RequireAntiforgeryTokenAttribute());

        endpoints.MapPost("/api/v1/admin/external-identity/invitations", CreateInvitationAsync)
            .RequireAuthorization("AeroAdmin")
            .WithMetadata(new RequireAntiforgeryTokenAttribute());

        endpoints.MapPost("/api/v1/admin/external-members/{memberId:long}/local-password-reset", IssueLocalPasswordResetAsync)
            .RequireAuthorization("AeroAdmin")
            .WithMetadata(new RequireAntiforgeryTokenAttribute());
    }

    private static async Task<IResult> GetAuthorityAsync(
        [FromServices] IExternalIdentityManagerScopeResolver scopeResolver,
        [FromServices] IQuerySession querySession,
        CancellationToken cancellationToken)
    {
        var scopeResult = await scopeResolver.ResolveAsync(cancellationToken);
        if (scopeResult is not Result<ExternalIdentityManagerScope, AeroError>.Ok(var scope))
            return Results.NotFound();
        try
        {
            var bindings = await querySession.Query<ExternalOrganizationBinding>()
                .Where(binding => binding.TenantId == scope.TenantId)
                .ToListAsync(cancellationToken);
            var locals = await querySession.Query<ExternalMemberLocalAuthority>()
                .Where(authority => authority.TenantId == scope.TenantId)
                .ToListAsync(cancellationToken);
            if (bindings.Count == 0 && locals.Count == 0)
                return Results.Ok(new ExternalIdentityAuthorityState(false, null, null, null, null, null, false));
            if (bindings.Count == 0 && locals.Count == 1)
                return Results.Ok(new ExternalIdentityAuthorityState(true,
                    LocalExternalMemberAuthentication.Provider, null, null, null, null, locals[0].IsActive));
            if (locals.Count == 1 && bindings.Count == 1 && locals[0].IsActive != bindings[0].IsActive)
            {
                if (locals[0].IsActive)
                    return Results.Ok(new ExternalIdentityAuthorityState(true,
                        LocalExternalMemberAuthentication.Provider, null, null, null, null, true));
                bindings = [bindings[0]];
            }
            else if (locals.Count != 0)
            {
                return Results.Problem(statusCode: StatusCodes.Status500InternalServerError,
                    title: "External authority is unavailable.");
            }
            if (bindings.Count != 1 ||
                !ExternalProviderAuthorityProjector.TryProject(bindings[0], scope.TenantId, out var authority,
                    requireActive: false))
                return Results.Problem(statusCode: StatusCodes.Status500InternalServerError,
                    title: "External authority is unavailable.");

            return Results.Ok(new ExternalIdentityAuthorityState(true, authority.Provider, authority.Authority,
                authority.OrganizationId, authority.SecretReference.VaultId,
                authority.SecretReference.VaultEnvironment, bindings[0].IsActive));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError,
                title: "External authority could not be loaded.");
        }
    }

    private static ExternalIdentityAuthorityState ToSafeState(ExternalIdentityAuthorityResult value) =>
        new(true, value.Provider, value.Authority, value.OrganizationId, value.VaultId,
            value.VaultEnvironment, value.Enabled);

    private static async Task<IResult> CreateInvitationAsync(
        [FromBody] CreateExternalIdentityInvitationRequest request,
        [FromServices] IExternalIdentityManagerScopeResolver scopeResolver,
        [FromServices] IExternalMemberIssuanceService issuanceService,
        [FromServices] ILocalExternalMemberAuthenticationService localAuthentication,
        [FromServices] IQuerySession querySession,
        CancellationToken cancellationToken)
    {
        if (request.AdditionalProperties is { Count: > 0 })
            return InvitationInvalid();

        var scopeResult = await scopeResolver.ResolveAsync(cancellationToken);
        if (scopeResult is not Result<ExternalIdentityManagerScope, AeroError>.Ok(var scope))
            return Results.NotFound(new { message = "Site not found." });

        try
        {
            var bindings = await querySession.Query<ExternalOrganizationBinding>()
                .Where(binding => binding.TenantId == scope.TenantId && binding.IsActive)
                .ToListAsync(cancellationToken);
            var locals = await querySession.Query<ExternalMemberLocalAuthority>()
                .Where(authority => authority.TenantId == scope.TenantId && authority.IsActive)
                .ToListAsync(cancellationToken);
            if (bindings.Count + locals.Count != 1)
                return Results.Conflict(new { message = "External authority is unavailable." });

            if (locals.Count == 1)
            {
                var localResult = await localAuthentication.CreateInvitationAsync(new(
                    scope.TenantId, scope.SiteId, locals[0].Id, request.Email, request.ExpiresAt), cancellationToken);
                return InvitationResult(localResult);
            }

            var binding = bindings[0];
            if (!ExternalProviderAuthorityProjector.TryProject(binding, scope.TenantId, out var authority))
                return Results.Conflict(new { message = "External authority is unavailable." });

            var result = await issuanceService.CreateInvitationAsync(new(
                scope.TenantId,
                scope.SiteId,
                authority.BindingId,
                authority.Provider,
                request.Email,
                request.ExpiresAt), cancellationToken);
            return InvitationResult(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "External invitation could not be created.");
        }
    }

    private static IResult InvitationResult(Result<ExternalMemberInvitationHandle, AeroError> result) => result switch
    {
        Result<ExternalMemberInvitationHandle, AeroError>.Ok(var invitation) =>
            Results.Ok(new ExternalIdentityInvitationResponse(invitation.Handle, invitation.ExpiresAt)),
        Result<ExternalMemberInvitationHandle, AeroError>.Failure(AeroError.Validation) => InvitationInvalid(),
        Result<ExternalMemberInvitationHandle, AeroError>.Failure(AeroError.Conflict) =>
            Results.Conflict(new { message = "External invitation could not be created." }),
        _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError,
            title: "External invitation could not be created.")
    };

    private static async Task<IResult> IssueLocalPasswordResetAsync(
        long memberId,
        [FromBody] IssueLocalExternalMemberPasswordResetAdminRequest request,
        HttpContext httpContext,
        [FromServices] IExternalIdentityManagerScopeResolver scopeResolver,
        [FromServices] ILocalExternalMemberAuthenticationService localAuthentication,
        [FromServices] ManagerLocalPasswordResetRateLimiter rateLimiter,
        CancellationToken cancellationToken)
    {
        if (memberId <= 0 || request.AdditionalProperties is { Count: > 0 })
            return Results.BadRequest(new { message = "Local password-reset request is invalid." });
        var managerClaims = httpContext.User.FindAll(ClaimTypes.NameIdentifier).ToArray();
        if (managerClaims.Length != 1 ||
            !long.TryParse(managerClaims[0].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var managerId) || managerId <= 0)
            return Results.Forbid();
        var scopeResult = await scopeResolver.ResolveAsync(cancellationToken);
        if (scopeResult is not Result<ExternalIdentityManagerScope, AeroError>.Ok(var scope))
            return Results.NotFound(new { message = "Site not found." });
        if (!rateLimiter.TryAcquire(httpContext, scope.TenantId, scope.SiteId))
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);

        var result = await localAuthentication.IssuePasswordResetAsync(new(
            scope.TenantId, scope.SiteId, memberId, managerId, request.ExpiresAt), cancellationToken);
        return result switch
        {
            Result<LocalExternalMemberPasswordResetHandle, AeroError>.Ok(var reset) =>
                Results.Ok(new LocalExternalMemberPasswordResetResponse(reset.Handle, reset.ExpiresAt)),
            Result<LocalExternalMemberPasswordResetHandle, AeroError>.Failure(AeroError.Validation) =>
                Results.BadRequest(new { message = "Local password-reset request is invalid." }),
            Result<LocalExternalMemberPasswordResetHandle, AeroError>.Failure(AeroError.Conflict) =>
                Results.Conflict(new { message = "Local password reset could not be issued." }),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError,
                title: "Local password reset could not be issued.")
        };
    }

    private static IResult InvitationInvalid() => Results.BadRequest(new
    {
        message = "External invitation request is invalid."
    });

}
