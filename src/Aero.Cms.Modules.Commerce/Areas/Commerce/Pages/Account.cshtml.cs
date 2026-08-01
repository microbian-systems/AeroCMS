using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Commerce.Storefront;
using Aero.Core.Http;
using AeroDB.Sable;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aero.Cms.Modules.Commerce.Areas.Commerce.Pages;

[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class AccountModel(
    ISiteContext siteContext,
    IQuerySession querySession,
    IStorefrontMemberAccessor storefrontMember) : PageModel
{
    public bool IsAuthenticated { get; private set; }
    public StorefrontAuthenticationProviderKind ProviderKind { get; private set; }
    public string ProviderLabel { get; private set; } = "your identity provider";

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var member = await storefrontMember.GetAsync(cancellationToken);
        IsAuthenticated = member.IsAuthorized;
        if (IsAuthenticated)
            return Page();

        if (siteContext.TenantId <= 0 || siteContext.SiteId <= 0)
            return Page();

        try
        {
            var locals = await querySession.Query<ExternalMemberLocalAuthority>()
                .Where(authority => authority.TenantId == siteContext.TenantId && authority.IsActive)
                .ToListAsync(cancellationToken);
            var remotes = await querySession.Query<ExternalOrganizationBinding>()
                .Where(binding => binding.TenantId == siteContext.TenantId && binding.IsActive)
                .ToListAsync(cancellationToken);
            if (locals.Count + remotes.Count != 1)
                return Page();

            if (locals.Count == 1)
            {
                ProviderKind = StorefrontAuthenticationProviderKind.Local;
                ProviderLabel = "AeroCMS";
                return Page();
            }

            ProviderLabel = remotes[0].Provider switch
            {
                ExternalMemberProviders.EntraExternalId => "Microsoft Entra External ID",
                ExternalMemberProviders.WorkOs => "WorkOS",
                _ => string.Empty
            };
            if (ProviderLabel.Length > 0)
                ProviderKind = StorefrontAuthenticationProviderKind.Remote;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            ProviderKind = StorefrontAuthenticationProviderKind.Unavailable;
        }

        return Page();
    }
}

public enum StorefrontAuthenticationProviderKind
{
    Unavailable,
    Local,
    Remote
}
