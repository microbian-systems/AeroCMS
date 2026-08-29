using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Html;
using FluentValidation;
using System.Text.Json;

namespace Aero.Cms.Modules.Pages.Validators;

/// <summary>
/// Validates that page-composition entries are bounded and target the supplied HTML draft.
/// </summary>
public sealed class PageCompositionValidator : AbstractValidator<PageCompositionDocument>
{
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
        var fragments = composition.RenderedFragments ?? [];
        var registeredFragments = composition.RegisteredFragments ?? [];
        var contentQueries = composition.ContentQueries ?? [];
        var scopeNodeIds = new HashSet<long>();

        foreach (var list in lists)
        {
            if (string.IsNullOrWhiteSpace(list.ContentEntryProvider))
            {
                ValidateScopeIdentity(content, list.NodeId, list.ContentTypeId, list.ContentTypeAlias, scopeNodeIds, context);
            }
            else
            {
                ValidateVirtualListScopeIdentity(content, list, scopeNodeIds, context);
            }

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

            ValidateQuery(list.NodeId, list.Query, context, !string.IsNullOrWhiteSpace(list.ContentEntryProvider));
            if (!Enum.IsDefined(list.EmptyState))
            {
                context.AddFailure(
                    nameof(PageContentListScope.EmptyState),
                    $"Content list scope '{list.NodeId}' has an unsupported empty-state behavior.");
            }
        }

        foreach (var item in items)
        {
            if (item.ContentEntryKey is { } entryKey)
            {
                ValidateVirtualItemScope(content, item, entryKey, scopeNodeIds, context);
                continue;
            }

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

        ValidateRenderedFragments(content, fragments, registeredFragments, scopeNodeIds, bindings, context);
        ValidateRegisteredFragments(content, registeredFragments, fragments, scopeNodeIds, bindings, context);
        ValidateContentQueries(contentQueries, context);
    }

    private static void ValidateVirtualItemScope(
        HtmlPageContent content,
        PageContentItemScope item,
        Aero.Cms.Abstractions.Content.Views.ContentEntryKey entryKey,
        ISet<long> scopeNodeIds,
        ValidationContext<PageCompositionDocument> context)
    {
        if (item.NodeId <= 0 || HtmlTreeOperations.FindById(content.Root, item.NodeId) is not { Kind: HtmlNodeKind.Element })
        {
            context.AddFailure("NodeId", $"Content scope node '{item.NodeId}' must identify an HTML element in the draft.");
        }

        if (!scopeNodeIds.Add(item.NodeId))
        {
            context.AddFailure(nameof(PageCompositionDocument.ContentItems),
                $"HTML node '{item.NodeId}' cannot own more than one content scope.");
        }

        var routeBound = !string.IsNullOrWhiteSpace(item.StableIdRouteParameter);
        if (string.IsNullOrWhiteSpace(entryKey.Provider)
            || (!routeBound && string.IsNullOrWhiteSpace(entryKey.StableId)))
        {
            context.AddFailure(nameof(PageContentItemScope.ContentEntryKey),
                $"Content item scope '{item.NodeId}' must provide a provider and stable entry identifier.");
        }

        if (item.ContentItemId is not null || !string.IsNullOrWhiteSpace(item.Slug))
        {
            context.AddFailure(nameof(PageContentItemScope.ContentEntryKey),
                $"Virtual content item scope '{item.NodeId}' cannot also specify a persisted item lookup.");
        }

        if (routeBound && !string.IsNullOrWhiteSpace(entryKey.StableId))
        {
            context.AddFailure(nameof(PageContentItemScope.StableIdRouteParameter),
                $"Route-bound virtual content scope '{item.NodeId}' cannot also persist a stable entry identifier.");
        }
    }

    private static void ValidateContentQueries(
        IReadOnlyList<ContentQueryDefinition> queries,
        ValidationContext<PageCompositionDocument> context)
    {
        foreach (var error in ContentQueryDefinition.ValidateDefinitions(queries))
        {
            context.AddFailure(
                nameof(PageCompositionDocument.ContentQueries),
                error);
        }
    }

    private static void ValidateRenderedFragments(
        HtmlPageContent content,
        IReadOnlyList<PageRenderedFragment> fragments,
        IReadOnlyList<PageRegisteredFragment> registeredFragments,
        ISet<long> scopeNodeIds,
        IReadOnlyList<PageFieldBinding> bindings,
        ValidationContext<PageCompositionDocument> context)
    {
        if (fragments.Count > PageRenderedFragment.MaximumFragmentsPerPage)
        {
            context.AddFailure(
                nameof(PageCompositionDocument.RenderedFragments),
                $"A page cannot contain more than {PageRenderedFragment.MaximumFragmentsPerPage} rendered fragments.");
        }

        var fragmentNodeIds = new HashSet<long>();
        foreach (var fragment in fragments)
        {
            var fragmentNode = HtmlTreeOperations.FindById(content.Root, fragment.NodeId);
            if (fragment.NodeId <= 0 || fragmentNode is not { Kind: HtmlNodeKind.Element })
            {
                context.AddFailure(
                    nameof(PageRenderedFragment.NodeId),
                    $"Rendered fragment node '{fragment.NodeId}' must identify an HTML element in the draft.");
                continue;
            }

            if (!fragmentNodeIds.Add(fragment.NodeId))
            {
                context.AddFailure(
                    nameof(PageCompositionDocument.RenderedFragments),
                    $"HTML node '{fragment.NodeId}' cannot own more than one rendered fragment.");
            }

            if (!Enum.IsDefined(fragment.Kind))
            {
                context.AddFailure(
                    nameof(PageRenderedFragment.Kind),
                    $"Rendered fragment node '{fragment.NodeId}' has an unsupported renderer.");
            }

            if ((fragment.Source?.Length ?? 0) > PageRenderedFragment.MaximumSourceLength)
            {
                context.AddFailure(
                    nameof(PageRenderedFragment.Source),
                    $"Rendered fragment node '{fragment.NodeId}' cannot exceed " +
                    $"{PageRenderedFragment.MaximumSourceLength} characters.");
            }

            if (string.IsNullOrWhiteSpace(fragment.Source))
            {
                context.AddFailure(
                    nameof(PageRenderedFragment.Source),
                    $"Rendered fragment node '{fragment.NodeId}' must provide source content.");
            }

            if (scopeNodeIds.Contains(fragment.NodeId))
            {
                context.AddFailure(
                    nameof(PageRenderedFragment.NodeId),
                    $"HTML node '{fragment.NodeId}' cannot be both a content scope and a rendered fragment.");
            }

            if (scopeNodeIds.Any(scopeNodeId => scopeNodeId != fragment.NodeId
                    && HtmlTreeOperations.FindById(fragmentNode, scopeNodeId) is not null))
            {
                context.AddFailure(
                    nameof(PageRenderedFragment.NodeId),
                    $"Rendered fragment node '{fragment.NodeId}' cannot contain a content scope because its children are replaced during rendering.");
            }

            if (bindings.Any(binding => HtmlTreeOperations.FindById(fragmentNode, binding.NodeId) is not null))
            {
                context.AddFailure(
                    nameof(PageRenderedFragment.NodeId),
                    $"Rendered fragment node '{fragment.NodeId}' cannot contain a field binding because its children are replaced during rendering.");
            }

            if (registeredFragments.Any(registered =>
                    HtmlTreeOperations.FindById(fragmentNode, registered.NodeId) is not null))
            {
                context.AddFailure(
                    nameof(PageRenderedFragment.NodeId),
                    $"Rendered fragment node '{fragment.NodeId}' cannot contain a registered fragment because its children are replaced during rendering.");
            }
        }
    }

    private static void ValidateRegisteredFragments(
        HtmlPageContent content,
        IReadOnlyList<PageRegisteredFragment> registeredFragments,
        IReadOnlyList<PageRenderedFragment> renderedFragments,
        ISet<long> scopeNodeIds,
        IReadOnlyList<PageFieldBinding> bindings,
        ValidationContext<PageCompositionDocument> context)
    {
        if (registeredFragments.Count > PageRegisteredFragment.MaximumFragmentsPerPage)
        {
            context.AddFailure(
                nameof(PageCompositionDocument.RegisteredFragments),
                $"A page cannot contain more than {PageRegisteredFragment.MaximumFragmentsPerPage} registered fragments.");
        }

        var nodeIds = new HashSet<long>();
        foreach (var fragment in registeredFragments)
        {
            var node = HtmlTreeOperations.FindById(content.Root, fragment.NodeId);
            if (fragment.NodeId <= 0 || node is not { Kind: HtmlNodeKind.Element })
            {
                context.AddFailure(
                    nameof(PageRegisteredFragment.NodeId),
                    $"Registered fragment node '{fragment.NodeId}' must identify an HTML element in the draft.");
                continue;
            }

            if (!nodeIds.Add(fragment.NodeId))
            {
                context.AddFailure(
                    nameof(PageCompositionDocument.RegisteredFragments),
                    $"HTML node '{fragment.NodeId}' cannot own more than one registered fragment.");
            }

            if (!PageRegisteredFragment.IsValidKey(fragment.Key)
                || !string.Equals(fragment.Key, PageRegisteredFragment.NormalizeKey(fragment.Key), StringComparison.Ordinal))
            {
                context.AddFailure(
                    nameof(PageRegisteredFragment.Key),
                    $"Registered fragment node '{fragment.NodeId}' must use a normalized lowercase dotted/kebab key.");
            }

            var parameters = fragment.Parameters ?? new Dictionary<string, JsonElement>();
            if (parameters.Count > PageRegisteredFragment.MaximumParameterCount
                || parameters.Keys.Any(name => string.IsNullOrWhiteSpace(name)
                    || name.Length > PageRegisteredFragment.MaximumParameterNameLength))
            {
                context.AddFailure(
                    nameof(PageRegisteredFragment.Parameters),
                    $"Registered fragment node '{fragment.NodeId}' contains invalid parameter names or too many parameters.");
            }

            int parameterSize;
            try
            {
                parameterSize = JsonSerializer.SerializeToUtf8Bytes(parameters).Length;
            }
            catch (Exception)
            {
                parameterSize = int.MaxValue;
            }

            if (parameterSize > PageRegisteredFragment.MaximumParametersUtf8Bytes)
            {
                context.AddFailure(
                    nameof(PageRegisteredFragment.Parameters),
                    $"Registered fragment node '{fragment.NodeId}' parameters are invalid or exceed the 16 KiB limit.");
            }

            var collidesWithScope = scopeNodeIds.Contains(fragment.NodeId)
                || scopeNodeIds.Any(scopeNodeId =>
                    HtmlTreeOperations.FindById(node, scopeNodeId) is not null);
            var collidesWithBinding = bindings.Any(binding =>
                HtmlTreeOperations.FindById(node, binding.NodeId) is not null);
            var collidesWithSource = renderedFragments.Any(source =>
                HtmlTreeOperations.FindById(node, source.NodeId) is not null);
            var containsRegistered = registeredFragments.Any(candidate =>
                candidate.NodeId != fragment.NodeId
                && HtmlTreeOperations.FindById(node, candidate.NodeId) is not null);

            if (collidesWithScope || collidesWithBinding || collidesWithSource || containsRegistered)
            {
                context.AddFailure(
                    nameof(PageRegisteredFragment.NodeId),
                    $"Registered fragment node '{fragment.NodeId}' cannot overlap another composition target because its children are replaced during rendering.");
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

    private static void ValidateVirtualListScopeIdentity(
        HtmlPageContent content,
        PageContentListScope scope,
        ISet<long> scopeNodeIds,
        ValidationContext<PageCompositionDocument> context)
    {
        if (scope.NodeId <= 0 || HtmlTreeOperations.FindById(content.Root, scope.NodeId) is not { Kind: HtmlNodeKind.Element })
        {
            context.AddFailure("NodeId", $"Content scope node '{scope.NodeId}' must identify an HTML element in the draft.");
        }

        if (!scopeNodeIds.Add(scope.NodeId))
        {
            context.AddFailure(nameof(PageCompositionDocument.ContentLists),
                $"HTML node '{scope.NodeId}' cannot own more than one content scope.");
        }

        var provider = scope.ContentEntryProvider.Trim();
        if (provider.Length is 0 or > 128
            || provider.Any(character => !(char.IsLetterOrDigit(character) || character is ':' or '_' or '-')))
        {
            context.AddFailure(nameof(PageContentListScope.ContentEntryProvider),
                $"Virtual content list scope '{scope.NodeId}' must use a bounded provider key.");
        }

        if (string.IsNullOrWhiteSpace(scope.ContentTypeAlias))
        {
            context.AddFailure(nameof(PageContentListScope.ContentTypeAlias),
                $"Virtual content list scope '{scope.NodeId}' must retain a presentation alias.");
        }
    }

    private static void ValidateQuery(
        long scopeNodeId,
        PageContentListQuery? query,
        ValidationContext<PageCompositionDocument> context,
        bool isVirtualProvider = false)
    {
        if (query is null)
        {
            context.AddFailure(
                nameof(PageContentListScope.Query),
                $"Content list scope '{scopeNodeId}' must provide a query.");
            return;
        }

        if (query.PageSize is < PageContentListQuery.MinimumPageSize
            or > PageContentListQuery.MaximumPageSize)
        {
            context.AddFailure(
                nameof(PageContentListQuery.PageSize),
                $"Content list scope '{scopeNodeId}' page size must be between " +
                $"{PageContentListQuery.MinimumPageSize} and {PageContentListQuery.MaximumPageSize}.");
        }

        if (!Enum.IsDefined(query.SortDirection))
        {
            context.AddFailure(
                nameof(PageContentListQuery.SortDirection),
                $"Content list scope '{scopeNodeId}' has an unsupported sort direction.");
        }

        var filters = query.Filters ?? [];
        if (filters.Count > PageContentListQuery.MaximumFilterCount)
        {
            context.AddFailure(
                nameof(PageContentListQuery.Filters),
                $"Content list scope '{scopeNodeId}' cannot contain more than " +
                $"{PageContentListQuery.MaximumFilterCount} filters.");
        }

        foreach (var filter in filters)
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

        if (!isVirtualProvider) return;

        if (!string.IsNullOrWhiteSpace(query.SortField))
        {
            context.AddFailure(nameof(PageContentListQuery.SortField),
                $"Virtual content list scope '{scopeNodeId}' does not support sorting.");
        }

        if (filters.Count > 1 || filters.Any(filter => filter.Operator != PageContentFilterOperator.Contains
            || !string.Equals(filter.FieldName, "$search", StringComparison.Ordinal)))
        {
            context.AddFailure(nameof(PageContentListQuery.Filters),
                $"Virtual content list scope '{scopeNodeId}' supports at most one Contains search filter on '$search'.");
        }
    }
}
