using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Core.Entities;
using Aero.Core.Http;
using AeroDB.Sable;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Identity;

/// <summary>Revalidates external-member cookies against local member and session state on every request.</summary>
public sealed class ExternalMemberCookieValidator
{
    /// <summary>Rejects cookies whose claims, local member, or local session are no longer valid.</summary>
    public async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        try
        {
            if (context.Principal is null || !ExternalMemberPrincipal.TryRead(context.Principal, out var claims))
            {
                await RejectAsync(context);
                return;
            }

            var session = context.HttpContext.RequestServices.GetRequiredService<IQuerySession>();
            var siteContext = context.HttpContext.RequestServices.GetRequiredService<ISiteContext>();
            var member = await session.LoadAsync<ExternalMember>(claims.MemberId, context.HttpContext.RequestAborted);
            var externalSession = await session.LoadAsync<ExternalMemberSession>(claims.SessionId, context.HttpContext.RequestAborted);
            var identityLink = externalSession is null || externalSession.ExternalIdentityLinkId <= 0
                ? null
                : await session.LoadAsync<ExternalIdentityLink>(
                    externalSession.ExternalIdentityLinkId,
                    context.HttpContext.RequestAborted);

            if (siteContext.TenantId <= 0 || siteContext.SiteId <= 0 ||
                member is null || !member.IsActive || member.SecurityVersion != claims.SecurityVersion ||
                externalSession is null || externalSession.ExternalMemberId != member.Id ||
                externalSession.TenantId != siteContext.TenantId ||
                externalSession.SiteId != siteContext.SiteId ||
                !string.Equals(externalSession.AuthenticationProvider, claims.Provider, StringComparison.Ordinal) ||
                externalSession.SecurityVersion != claims.SecurityVersion || externalSession.RevokedAt is not null ||
                externalSession.ExpiresAt <= DateTimeOffset.UtcNow ||
                identityLink is null || !identityLink.IsActive || identityLink.ExternalMemberId != member.Id ||
                identityLink.Id != externalSession.ExternalIdentityLinkId ||
                !string.Equals(identityLink.Provider, claims.Provider, StringComparison.Ordinal))
            {
                await RejectAsync(context);
            }
        }
        catch
        {
            await RejectAsync(context);
        }
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(ExternalMemberAuthenticationDefaults.Scheme);
    }
}
