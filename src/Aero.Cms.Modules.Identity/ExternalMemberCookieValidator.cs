using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Core.Entities;
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
            var member = await session.LoadAsync<ExternalMember>(claims.MemberId, context.HttpContext.RequestAborted);
            var externalSession = await session.LoadAsync<ExternalMemberSession>(claims.SessionId, context.HttpContext.RequestAborted);

            if (member is null || !member.IsActive || member.SecurityVersion != claims.SecurityVersion ||
                externalSession is null || externalSession.ExternalMemberId != member.Id ||
                !string.Equals(externalSession.AuthenticationProvider, claims.Provider, StringComparison.Ordinal) ||
                externalSession.SecurityVersion != claims.SecurityVersion || externalSession.RevokedAt is not null ||
                externalSession.ExpiresAt <= DateTimeOffset.UtcNow)
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
