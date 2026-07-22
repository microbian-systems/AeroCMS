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

namespace Aero.Cms.Modules.Identity;

/// <summary>Maps invitation activation, password sign-in, and password reset for local storefront members.</summary>
public static class ExternalMemberLocalApi
{
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
