using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Modules.Commerce.Subscriptions;
using Aero.Core.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aero.Cms.Modules.Commerce.Areas.Commerce.Pages;

/// <summary>Displays only the current member's redacted recurring purchase history for this host site.</summary>
[Authorize(Policy = ExternalMemberAuthenticationDefaults.Policy)]
[Authorize(Policy = ExternalMemberAuthenticationDefaults.SitePolicy)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class SubscriptionsModel(
    ISubscriptionVisibilityService visibility,
    ICurrentPrincipal principal,
    ISiteContext site) : PageModel
{
    public IReadOnlyList<MemberSubscriptionSummary> Subscriptions { get; private set; } = [];
    public bool LoadFailed { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var memberId = principal.PrincipalId;
        if (memberId is not > 0) return NotFound();
        var result = await visibility.ListForMemberAsync(site.TenantId, site.SiteId, memberId.Value, ct);
        if (result is Result<IReadOnlyList<MemberSubscriptionSummary>, AeroError>.Ok ok)
            Subscriptions = ok.Value;
        else
            LoadFailed = true;
        return Page();
    }

    public static string StateLabel(SubscriptionState state) => state switch
    {
        SubscriptionState.PendingProviderConfirmation => "Waiting for provider confirmation",
        SubscriptionState.ManualReview => "Manual review needed",
        SubscriptionState.PastDue => "Payment needs attention",
        _ => state.ToString()
    };
}
