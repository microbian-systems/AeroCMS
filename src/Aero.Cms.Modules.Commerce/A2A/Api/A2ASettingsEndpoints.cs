using System.Security.Claims;
using Aero.Cms.Modules.Commerce.A2A.Models;
using Aero.Cms.Modules.Commerce.A2A.Services;
using Aero.Cms.Modules.Commerce.Catalog.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Commerce.A2A.Api;

/// <summary>Maps manager-only A2A availability endpoints for the explicitly selected persisted site.</summary>
public static class A2ASettingsEndpoints
{
    /// <summary>Adds the selected-site A2A settings API.</summary>
    public static IEndpointRouteBuilder MapA2ASettingsApi(this IEndpointRouteBuilder builder)
    {
        var manager = builder.MapGroup("/api/v1/admin/commerce/a2a");
        manager.MapGet("/settings", GetAsync).RequireAuthorization("site:read");
        manager.MapPut("/settings", UpdateAsync).RequireAuthorization("site:update");
        return builder;
    }

    private static async Task<IResult> GetAsync(
        IA2ASettingsService service,
        ICommerceManagerScopeResolver scopeResolver,
        CancellationToken ct)
    {
        var scope = await scopeResolver.ResolveAsync(ct);
        if (scope is Result<CommerceManagerScope, AeroError>.Failure scopeFailure)
        {
            return Failure(scopeFailure.Error);
        }

        var value = ((Result<CommerceManagerScope, AeroError>.Ok)scope).Value;
        var result = await service.GetAsync(value.TenantId, value.SiteId, ct);
        return result switch
        {
            Result<A2ASettingsResponse, AeroError>.Ok ok => Results.Ok(ok.Value),
            Result<A2ASettingsResponse, AeroError>.Failure failure => Failure(failure.Error),
            _ => Results.Problem("A2A settings operation failed.", statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private static async Task<IResult> UpdateAsync(
        UpdateA2ASettingsRequest request,
        ClaimsPrincipal user,
        IA2ASettingsService service,
        ICommerceManagerScopeResolver scopeResolver,
        CancellationToken ct)
    {
        // The site policy above proves the selected cookie is an authorized selection. Enabling is
        // intentionally a CMS-administrator action, never a delegated site-permission action.
        if (!IsCmsAdministrator(user))
        {
            return Results.Forbid();
        }

        var scope = await scopeResolver.ResolveAsync(ct);
        if (scope is Result<CommerceManagerScope, AeroError>.Failure scopeFailure)
        {
            return Failure(scopeFailure.Error);
        }

        var value = ((Result<CommerceManagerScope, AeroError>.Ok)scope).Value;
        var actorId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
        var result = await service.UpdateAsync(value.TenantId, value.SiteId, request, actorId, ct);
        return result switch
        {
            Result<A2ASettingsResponse, AeroError>.Ok ok => Results.Ok(ok.Value),
            Result<A2ASettingsResponse, AeroError>.Failure failure => Failure(failure.Error),
            _ => Results.Problem("A2A settings operation failed.", statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private static bool IsCmsAdministrator(ClaimsPrincipal user)
        => user.IsInRole("Admin") || user.HasClaim("is_admin", "true");

    private static IResult Failure(AeroError error) => error switch
    {
        AeroError.NotFound => Results.NotFound(),
        AeroError.Validation validation => Results.BadRequest(new A2ASettingsValidationErrorResponse(validation.Errors)),
        AeroError.BadRequest badRequest => Results.BadRequest(new A2ASettingsErrorResponse(badRequest.msg)),
        AeroError.InvalidRequest invalidRequest => Results.BadRequest(new A2ASettingsErrorResponse(invalidRequest.msg)),
        AeroError.Conflict conflict => Results.Conflict(new A2ASettingsErrorResponse(conflict.msg)),
        _ => Results.Problem("A2A settings operation failed.", statusCode: StatusCodes.Status500InternalServerError)
    };
}

/// <summary>Represents validation errors returned by the A2A settings endpoint.</summary>
public sealed record A2ASettingsValidationErrorResponse(IEnumerable<string> Errors);

/// <summary>Represents a non-validation A2A settings error.</summary>
public sealed record A2ASettingsErrorResponse(string Error);
