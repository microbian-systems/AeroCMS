using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Html;
using FluentValidation;

namespace Aero.Cms.Modules.Pages.Validators;

/// <summary>
/// Validates that page-composition entries are bounded and target the supplied HTML draft.
/// </summary>
public sealed class PageCompositionValidator : AbstractValidator<PageCompositionDocument>
{
    private const int MaximumPageSize = 100;

    /// <summary>
    /// Initializes a validator for one candidate HTML draft.
    /// </summary>
    /// <param name="content">The candidate HTML content that owns all referenced node identifiers.</param>
    public PageCompositionValidator(HtmlPageContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        RuleFor(composition => composition).Custom((composition, context) =>
            ValidateComposition(content, composition, context));
    }

    private static void ValidateComposition(
        HtmlPageContent content,
        PageCompositionDocument composition,
        ValidationContext<PageCompositionDocument> context)
    {
        var lists = composition.ContentLists ?? [];
        var items = composition.ContentItems ?? [];
        var bindings = composition.FieldBindings ?? [];
        var scopeNodeIds = new HashSet<long>();

        foreach (var list in lists)
        {
            ValidateScopeIdentity(content, list.NodeId, list.ContentTypeId, list.ContentTypeAlias, scopeNodeIds, context);

            var scopeNode = HtmlTreeOperations.FindById(content.Root, list.NodeId);
            if (list.TemplateRootNodeId <= 0
                || list.TemplateRootNodeId == list.NodeId
                || scopeNode is null
                || HtmlTreeOperations.FindById(scopeNode, list.TemplateRootNodeId) is null)
            {
                context.AddFailure(
                    nameof(PageContentListScope.TemplateRootNodeId),
                    $"Content list scope '{list.NodeId}' must identify a template node inside its scope.");
            }

            ValidateQuery(list.NodeId, list.Query, context);
            if (!Enum.IsDefined(list.EmptyState))
            {
                context.AddFailure(
                    nameof(PageContentListScope.EmptyState),
                    $"Content list scope '{list.NodeId}' has an unsupported empty-state behavior.");
            }
        }

        foreach (var item in items)
        {
            ValidateScopeIdentity(content, item.NodeId, item.ContentTypeId, item.ContentTypeAlias, scopeNodeIds, context);

            if (!Enum.IsDefined(item.LookupMode))
            {
                context.AddFailure(
                    nameof(PageContentItemScope.LookupMode),
                    $"Content item scope '{item.NodeId}' has an unsupported lookup mode.");
                continue;
            }

            if (item.LookupMode == PageContentItemLookupMode.StableId
                && item.ContentItemId is not > 0)
            {
                context.AddFailure(
                    nameof(PageContentItemScope.ContentItemId),
                    $"Content item scope '{item.NodeId}' must identify a stable content item.");
            }

            if (item.LookupMode == PageContentItemLookupMode.Slug
                && string.IsNullOrWhiteSpace(item.Slug))
            {
                context.AddFailure(
                    nameof(PageContentItemScope.Slug),
                    $"Content item scope '{item.NodeId}' must provide a slug for slug lookup.");
            }
        }

        var bindingTargets = new HashSet<(long NodeId, PageFieldBindingTarget Target)>();
        foreach (var binding in bindings)
        {
            if (!bindingTargets.Add((binding.NodeId, binding.Target)))
            {
                context.AddFailure(
                    nameof(PageCompositionDocument.FieldBindings),
                    $"Node '{binding.NodeId}' has more than one binding for target '{binding.Target}'.");
            }

            if (!scopeNodeIds.Contains(binding.ScopeNodeId))
            {
                context.AddFailure(
                    nameof(PageFieldBinding.ScopeNodeId),
                    $"Field binding for node '{binding.NodeId}' references an unknown content scope.");
                continue;
            }

            var scopeNode = HtmlTreeOperations.FindById(content.Root, binding.ScopeNodeId);
            if (binding.NodeId <= 0
                || scopeNode is null
                || HtmlTreeOperations.FindById(scopeNode, binding.NodeId) is null)
            {
                context.AddFailure(
                    nameof(PageFieldBinding.NodeId),
                    $"Field binding node '{binding.NodeId}' must be inside scope '{binding.ScopeNodeId}'.");
            }

            if (string.IsNullOrWhiteSpace(binding.FieldName))
            {
                context.AddFailure(
                    nameof(PageFieldBinding.FieldName),
                    $"Field binding for node '{binding.NodeId}' must identify a content field.");
            }

            if (!Enum.IsDefined(binding.Target))
            {
                context.AddFailure(
                    nameof(PageFieldBinding.Target),
                    $"Field binding for node '{binding.NodeId}' has an unsupported output target.");
            }
        }
    }

    private static void ValidateScopeIdentity(
        HtmlPageContent content,
        long nodeId,
        long contentTypeId,
        string contentTypeAlias,
        ISet<long> scopeNodeIds,
        ValidationContext<PageCompositionDocument> context)
    {
        if (nodeId <= 0 || HtmlTreeOperations.FindById(content.Root, nodeId) is not { Kind: HtmlNodeKind.Element })
        {
            context.AddFailure(
                "NodeId",
                $"Content scope node '{nodeId}' must identify an HTML element in the draft.");
        }

        if (!scopeNodeIds.Add(nodeId))
        {
            context.AddFailure(
                nameof(PageCompositionDocument.ContentLists),
                $"HTML node '{nodeId}' cannot own more than one content scope.");
        }

        if (contentTypeId <= 0)
        {
            context.AddFailure(
                "ContentTypeId",
                $"Content scope '{nodeId}' must identify a stable content type.");
        }

        if (string.IsNullOrWhiteSpace(contentTypeAlias))
        {
            context.AddFailure(
                "ContentTypeAlias",
                $"Content scope '{nodeId}' must retain the content-type alias.");
        }
    }

    private static void ValidateQuery(
        long scopeNodeId,
        PageContentListQuery? query,
        ValidationContext<PageCompositionDocument> context)
    {
        if (query is null)
        {
            context.AddFailure(
                nameof(PageContentListScope.Query),
                $"Content list scope '{scopeNodeId}' must provide a query.");
            return;
        }

        if (query.PageSize is < 1 or > MaximumPageSize)
        {
            context.AddFailure(
                nameof(PageContentListQuery.PageSize),
                $"Content list scope '{scopeNodeId}' page size must be between 1 and {MaximumPageSize}.");
        }

        if (!Enum.IsDefined(query.SortDirection))
        {
            context.AddFailure(
                nameof(PageContentListQuery.SortDirection),
                $"Content list scope '{scopeNodeId}' has an unsupported sort direction.");
        }

        foreach (var filter in query.Filters ?? [])
        {
            if (string.IsNullOrWhiteSpace(filter.FieldName))
            {
                context.AddFailure(
                    nameof(PageContentFilter.FieldName),
                    $"Content list scope '{scopeNodeId}' contains a filter without a field name.");
            }

            if (!Enum.IsDefined(filter.Operator))
            {
                context.AddFailure(
                    nameof(PageContentFilter.Operator),
                    $"Content list scope '{scopeNodeId}' contains an unsupported filter operator.");
            }

            if (filter.Operator is not PageContentFilterOperator.IsEmpty
                and not PageContentFilterOperator.IsNotEmpty
                && string.IsNullOrWhiteSpace(filter.Value))
            {
                context.AddFailure(
                    nameof(PageContentFilter.Value),
                    $"Content list scope '{scopeNodeId}' contains a filter without a comparison value.");
            }
        }
    }
}
