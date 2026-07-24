using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Http.Clients;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor;

/// <summary>Collects the redirect decision for a previously published page URL.</summary>
public partial class PageRouteChangeDialog
{
    private bool _redirectFromOldUrl = true;

    [Inject]
    private DialogService DialogService { get; set; } = default!;

    /// <summary>Gets or sets the route-change impact calculated by the Pages service.</summary>
    [Parameter, EditorRequired]
    public PageRouteChangeImpact Impact { get; set; } = default!;

    private void Confirm() => DialogService.Close(
        _redirectFromOldUrl
            ? PreviousPathBehavior.CreatePermanentRedirect
            : PreviousPathBehavior.Discard);

    private void Cancel() => DialogService.Close(null);
}
