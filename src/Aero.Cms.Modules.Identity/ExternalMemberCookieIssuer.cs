using Aero.Cms.Abstractions.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Modules.Identity;

/// <summary>Issues the isolated storefront-member cookie only from a committed local session receipt.</summary>
public sealed class ExternalMemberCookieIssuer(
    IExternalMemberSessionRevocationService revocationService,
    TimeProvider timeProvider)
{
    public async Task<bool> TryIssueAsync(
        HttpContext context,
        ExternalMemberIssuanceReceipt receipt)
    {
        if (!IsCanonical(receipt))
        {
            await CompensateAndClearAsync(context, receipt);
            return false;
        }

        try
        {
            var principal = ExternalMemberPrincipal.Create(
                receipt.ExternalMemberId,
                receipt.Provider,
                receipt.ExternalMemberSessionId,
                receipt.SecurityVersion);
            await context.SignInAsync(
                ExternalMemberAuthenticationDefaults.Scheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = false,
                    AllowRefresh = false,
                    ExpiresUtc = receipt.ExpiresAt
                });
            return true;
        }
        catch
        {
            await CompensateAndClearAsync(context, receipt);
            return false;
        }
    }

    private bool IsCanonical(ExternalMemberIssuanceReceipt receipt) =>
        receipt.ExternalMemberId > 0 && receipt.ExternalIdentityLinkId > 0 &&
        receipt.ExternalMemberSessionId > 0 && receipt.TenantId > 0 && receipt.SiteId > 0 &&
        receipt.SecurityVersion > 0 && receipt.ExpiresAt > timeProvider.GetUtcNow() &&
        ExternalMemberSessionProviders.IsSupported(receipt.Provider) &&
        ExternalMemberIssuanceRules.IsSafeLocalReturnPath(receipt.ReturnPath);

    private async Task CompensateAndClearAsync(HttpContext context, ExternalMemberIssuanceReceipt receipt)
    {
        if (receipt.ExternalMemberId > 0 && receipt.ExternalMemberSessionId > 0 &&
            receipt.SecurityVersion > 0 && ExternalMemberSessionProviders.IsSupported(receipt.Provider))
        {
            try
            {
                await revocationService.RevokeAsync(new(
                    receipt.TenantId,
                    receipt.SiteId,
                    receipt.ExternalMemberId,
                    receipt.ExternalMemberSessionId,
                    receipt.Provider,
                    receipt.SecurityVersion), CancellationToken.None);
            }
            catch
            {
                // Cookie issuance remains failed closed even if compensation cannot be persisted.
            }
        }

        try
        {
            await context.SignOutAsync(ExternalMemberAuthenticationDefaults.Scheme);
        }
        catch
        {
            // No usable storefront-member cookie is returned from this response.
        }
    }
}
