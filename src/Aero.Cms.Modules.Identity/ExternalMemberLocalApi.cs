using Aero.Cms.Abstractions.Authentication;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authorization;

namespace Aero.Cms.Modules.Identity;

/// <summary>Maps invitation activation, password sign-in, and password reset for local storefront members.</summary>
public static class ExternalMemberLocalApi
{
    public sealed record MemberTokenRequest(string Email, string Password);
    private static readonly IReadOnlySet<string> ActivationFields =
        new HashSet<string>(["invitationHandle", "email", "password", "displayName", "returnPath"], StringComparer.Ordinal);
    private static readonly IReadOnlySet<string> LoginFields =
        new HashSet<string>(["email", "password", "returnPath"], StringComparer.Ordinal);
    private static readonly IReadOnlySet<string> ResetFields =
        new HashSet<string>(["resetHandle", "newPassword", "returnPath"], StringComparer.Ordinal);

    public static void MapExternalMemberLocalApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/member/local")
            .WithTags("Storefront - Local member")
            .AllowAnonymous()
            .WithMetadata(new RequireAntiforgeryTokenAttribute());

        group.MapPost("/activate", ActivateAsync)
            .RequireRateLimiting(LocalExternalMemberAuthentication.ActivationRateLimitPolicy);
        group.MapPost("/login", LoginAsync)
            .RequireRateLimiting(LocalExternalMemberAuthentication.LoginRateLimitPolicy);
        endpoints.MapPost("/api/v1/member/local/token", TokenAsync).AllowAnonymous().DisableAntiforgery()
            .RequireRateLimiting(LocalExternalMemberAuthentication.TokenRateLimitPolicy);
        endpoints.MapPut("/api/v1/member/local/assignments/{memberId:long}/role", SetMemberRoleAsync)
            .RequireAuthorization(ExternalMemberAuthenticationDefaults.MemberUsersManageBearerPolicy);
        group.MapPost("/reset", ResetAsync)
            .RequireRateLimiting(LocalExternalMemberAuthentication.PasswordResetRateLimitPolicy);
    }

    private static async Task<IResult> ActivateAsync(
        HttpContext context,
        ISiteContext siteContext,
        ILocalExternalMemberAuthenticationService authentication,
        ExternalMemberCookieIssuer cookieIssuer,
        CancellationToken cancellationToken)
    {
        if (!TryGetScope(siteContext, out var tenantId, out var siteId))
            return PublicFailure();
        var form = await TryReadFormAsync(context.Request, ActivationFields, cancellationToken);
        if (form is null ||
            !TryRequired(form, "invitationHandle", out var invitationHandle) ||
            !TryRequired(form, "email", out var email) ||
            !TryRequired(form, "password", out var password) ||
            !TryOptional(form, "displayName", out var displayName) ||
            !TryRequired(form, "returnPath", out var returnPath))
            return PublicFailure();

        var result = await authentication.ActivateInvitationAsync(new(
            tenantId, siteId, invitationHandle, email, password, displayName, returnPath), cancellationToken);
        return result is Result<ExternalMemberIssuanceReceipt, AeroError>.Ok(var receipt) &&
            await cookieIssuer.TryIssueAsync(context, receipt)
                ? Results.LocalRedirect(receipt.ReturnPath)
                : PublicFailure();
    }

    private static async Task<IResult> LoginAsync(
        HttpContext context,
        ISiteContext siteContext,
        ILocalExternalMemberAuthenticationService authentication,
        ExternalMemberCookieIssuer cookieIssuer,
        CancellationToken cancellationToken)
    {
        if (!TryGetScope(siteContext, out var tenantId, out var siteId))
            return PublicFailure();
        var form = await TryReadFormAsync(context.Request, LoginFields, cancellationToken);
        if (form is null ||
            !TryRequired(form, "email", out var email) ||
            !TryRequired(form, "password", out var password) ||
            !TryRequired(form, "returnPath", out var returnPath))
            return PublicFailure();

        var result = await authentication.LoginAsync(new(
            tenantId, siteId, email, password, returnPath), cancellationToken);
        return result is Result<ExternalMemberIssuanceReceipt, AeroError>.Ok(var receipt) &&
            await cookieIssuer.TryIssueAsync(context, receipt)
                ? Results.LocalRedirect(receipt.ReturnPath)
                : PublicFailure();
    }

    private static async Task<IResult> TokenAsync(
        [FromBody] MemberTokenRequest request,
        [FromServices] ISiteContext siteContext,
        [FromServices] IConfiguration config,
        [FromServices] ILocalExternalMemberAuthenticationService authentication,
        [FromServices] ExternalMemberJwtIssuer issuer,
        CancellationToken cancellationToken)
    {
        if (!config.GetValue<bool>(LocalExternalMemberAuthentication.JwtEnabledConfigurationKey) || !TryGetScope(siteContext, out var tenantId, out var siteId) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password)) return PublicFailure();
        var result = await authentication.LoginAsync(new(tenantId, siteId, request.Email, request.Password, "/"), cancellationToken);
        if (result is not Result<ExternalMemberIssuanceReceipt, AeroError>.Ok(var receipt)) return PublicFailure();
        var token = await issuer.TryIssueAsync(receipt, cancellationToken);
        return token is null ? PublicFailure() : Results.Ok(new { accessToken = token.Value, expiresAt = token.ExpiresAt });
    }

    private static async Task<IResult> SetMemberRoleAsync(
        long memberId,
        [FromBody] ExternalIdentityAdminApi.ExternalMemberRoleAssignmentRequest request,
        HttpContext context,
        [FromServices] ISiteContext site,
        [FromServices] AeroDB.Sable.IDocumentSession session,
        [FromServices] FluentValidation.IValidator<ExternalIdentityAdminApi.ExternalMemberRoleAssignmentRequest> validator,
        CancellationToken ct)
    {
        if (memberId <= 0 || !(await validator.ValidateAsync(request, ct)).IsValid || !Enum.TryParse<Aero.Cms.Core.Entities.ExternalMemberCustomerRole>(request.Role, false, out var role) || !Enum.IsDefined(role) ||
            !ExternalMemberPrincipal.TryRead(context.User, out _) || site is not { TenantId: > 0, SiteId: > 0 }) return Results.BadRequest();
        var assignment = await session.Query<Aero.Cms.Core.Entities.ExternalMemberSiteAssignment>().FirstOrDefaultAsync(x => x.ExternalMemberId == memberId && x.TenantId == site.TenantId && x.SiteId == site.SiteId, ct);
        if (assignment is null) return Results.NotFound();
        assignment.CustomerRole = role; assignment.ModifiedOn = DateTimeOffset.UtcNow; session.Store(assignment);
        try { await session.SaveChangesAsync(ct); return Results.NoContent(); } catch (AeroDB.Sable.ConcurrencyException) { session.ClearChanges(); return Results.Conflict(); }
    }

    private static async Task<IResult> ResetAsync(
        HttpContext context,
        ISiteContext siteContext,
        ILocalExternalMemberAuthenticationService authentication,
        CancellationToken cancellationToken)
    {
        if (!TryGetScope(siteContext, out var tenantId, out var siteId))
            return PublicFailure();
        var form = await TryReadFormAsync(context.Request, ResetFields, cancellationToken);
        if (form is null ||
            !TryRequired(form, "resetHandle", out var resetHandle) ||
            !TryRequired(form, "newPassword", out var newPassword) ||
            !TryRequired(form, "returnPath", out var returnPath))
            return PublicFailure();

        var result = await authentication.ResetPasswordAsync(new(
            tenantId, siteId, resetHandle, newPassword, returnPath), cancellationToken);
        if (result is not Result<LocalExternalMemberPasswordResetReceipt, AeroError>.Ok(var receipt))
            return PublicFailure();

        try
        {
            await context.SignOutAsync(ExternalMemberAuthenticationDefaults.Scheme);
        }
        catch
        {
            // Server-side versions and sessions were already revoked atomically.
        }

        return Results.LocalRedirect(receipt.ReturnPath);
    }

    private static bool TryGetScope(ISiteContext context, out long tenantId, out long siteId)
    {
        tenantId = context.TenantId;
        siteId = context.SiteId;
        return tenantId > 0 && siteId > 0;
    }

    private static async Task<IFormCollection?> TryReadFormAsync(
        HttpRequest request,
        IReadOnlySet<string> allowedFields,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
            return null;
        try
        {
            var form = await request.ReadFormAsync(cancellationToken);
            return form.Keys.All(key => key == "__RequestVerificationToken" || allowedFields.Contains(key))
                ? form
                : null;
        }
        catch (Exception exception) when (exception is InvalidDataException or BadHttpRequestException)
        {
            return null;
        }
    }

    private static bool TryRequired(IFormCollection form, string key, out string value)
    {
        var values = form[key];
        value = values.Count == 1 ? values[0] ?? string.Empty : string.Empty;
        return values.Count == 1 && !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryOptional(IFormCollection form, string key, out string? value)
    {
        var values = form[key];
        value = values.Count == 1 && !string.IsNullOrWhiteSpace(values[0]) ? values[0] : null;
        return values.Count <= 1;
    }

    private static IResult PublicFailure() => Results.BadRequest(new
    {
        message = "Local member authentication could not be completed."
    });
}
