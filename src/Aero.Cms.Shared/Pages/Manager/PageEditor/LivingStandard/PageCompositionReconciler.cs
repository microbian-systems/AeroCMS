using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Html;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>
/// Removes composition entries whose stable HTML targets no longer exist.
/// Domain and content-reference validation remain at the Pages/Content module boundary.
/// </summary>
internal static class PageCompositionReconciler
{
    public static PageCompositionDocument RemoveOrphans(
        HtmlPageContent content,
        PageCompositionDocument composition)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(composition);

        var contentLists = (composition.ContentLists ?? [])
            .Where(scope => IsValidListScope(content, scope))
            .Select(scope => scope.CreateSnapshot())
            .ToArray();

        var contentItems = (composition.ContentItems ?? [])
            .Where(scope => IsElement(content, scope.NodeId))
            .ToArray();

        var scopeNodeIds = contentLists
            .Select(scope => scope.NodeId)
            .Concat(contentItems.Select(scope => scope.NodeId))
            .ToHashSet();

        var fieldBindings = (composition.FieldBindings ?? [])
            .Where(binding => IsValidBinding(content, binding, scopeNodeIds))
            .ToArray();

        var renderedFragments = (composition.RenderedFragments ?? [])
            .Where(fragment => IsElement(content, fragment.NodeId))
            .ToArray();

        var registeredFragments = (composition.RegisteredFragments ?? [])
            .Where(fragment => IsElement(content, fragment.NodeId))
            .Select(fragment => fragment.CreateSnapshot())
            .ToArray();

        return new PageCompositionDocument
        {
            ContentLists = contentLists,
            ContentItems = contentItems,
            FieldBindings = fieldBindings,
            RenderedFragments = renderedFragments,
            RegisteredFragments = registeredFragments,
            ContentQueries = (composition.ContentQueries ?? [])
                .Select(query => query is null ? null! : query.CreateSnapshot())
                .ToArray()
        };
    }

    private static bool IsValidListScope(
        HtmlPageContent content,
        PageContentListScope scope)
    {
        var scopeNode = HtmlTreeOperations.FindById(content.Root, scope.NodeId);
        return scopeNode is { Kind: HtmlNodeKind.Element }
            && scope.TemplateRootNodeId != scope.NodeId
            && HtmlTreeOperations.FindById(scopeNode, scope.TemplateRootNodeId) is not null;
    }

    private static bool IsElement(HtmlPageContent content, long nodeId) =>
        HtmlTreeOperations.FindById(content.Root, nodeId) is { Kind: HtmlNodeKind.Element };

    private static bool IsValidBinding(
        HtmlPageContent content,
        PageFieldBinding binding,
        ISet<long> scopeNodeIds)
    {
        if (!scopeNodeIds.Contains(binding.ScopeNodeId))
        {
            return false;
        }

        var scopeNode = HtmlTreeOperations.FindById(content.Root, binding.ScopeNodeId);
        return scopeNode is not null
            && HtmlTreeOperations.FindById(scopeNode, binding.NodeId) is not null;
    }
}
