using Aero.Cms.Abstractions.Pages.Composition;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>
/// Carries one author-requested content-list query update to the owning PageEditor.
/// </summary>
public sealed record HtmlContentListSettingsRequest
{
    public long ScopeNodeId { get; init; }

    public PageContentListQuery Query { get; init; } = new();

    public PageContentEmptyStateBehavior EmptyState { get; init; }
}
