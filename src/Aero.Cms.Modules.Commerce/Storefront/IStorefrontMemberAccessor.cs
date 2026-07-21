using System.Security.Claims;
using Aero.Cms.Abstractions.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Modules.Commerce.Storefront;

/// <summary>Explicitly resolves the isolated storefront cookie without relying on the host default identity scheme.</summary>
public interface IStorefrontMemberAccessor
{
    Task<StorefrontMemberState> GetAsync(CancellationToken ct = default);
}

/// <summary>Represents the external-member state for the current storefront request.</summary>
public enum StorefrontMemberStateKind { Unauthenticated, NotCurrentSiteMember, Authorized }

/// <summary>Contains the state and authoritative local member identifier when authorization succeeds.</summary>
public sealed record StorefrontMemberState(StorefrontMemberStateKind Kind, long? MemberId = null)
{
    public bool IsAuthorized => Kind == StorefrontMemberStateKind.Authorized && MemberId is > 0;
}

/// <summary>Authenticates the named member scheme first, then evaluates both member policies against that principal.</summary>
public sealed class StorefrontMemberAccessor(IHttpContextAccessor httpContextAccessor, IAuthenticationService authentication, IAuthorizationService authorization) : IStorefrontMemberAccessor
{
    public async Task<StorefrontMemberState> GetAsync(CancellationToken ct = default)
    {
        var context = httpContextAccessor.HttpContext;
        if (context is null) return new(StorefrontMemberStateKind.Unauthenticated);
        var authenticationResult = await authentication.AuthenticateAsync(context, ExternalMemberAuthenticationDefaults.Scheme);
        if (!authenticationResult.Succeeded || authenticationResult.Principal is null) return new(StorefrontMemberStateKind.Unauthenticated);
        var principal = authenticationResult.Principal;
        var external = await authorization.AuthorizeAsync(principal, null, ExternalMemberAuthenticationDefaults.Policy);
        if (!external.Succeeded) return new(StorefrontMemberStateKind.Unauthenticated);
        var site = await authorization.AuthorizeAsync(principal, null, ExternalMemberAuthenticationDefaults.SitePolicy);
        if (!site.Succeeded) return new(StorefrontMemberStateKind.NotCurrentSiteMember);
        var ids = principal.FindAll(ClaimTypes.NameIdentifier).Select(x => x.Value).ToArray();
        return ids.Length == 1 && long.TryParse(ids[0], out var memberId) && memberId > 0
            ? new(StorefrontMemberStateKind.Authorized, memberId)
            : new(StorefrontMemberStateKind.Unauthenticated);
    }
}
