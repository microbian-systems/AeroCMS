using Aero.Cms.Html;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>
/// A browser-derived move request expressed only with stable node identities
/// and semantic placement. The HTML tree remains the source of truth.
/// </summary>
public sealed record HtmlSortMoveIntent(
    long NodeId,
    long TargetNodeId,
    HtmlRelativePlacement Placement);
