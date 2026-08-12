using System.Text.Json;
using Aero.Cms.Abstractions.Content.Composition;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Html;
using Aero.Cms.Modules.Pages.Validators;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Pages.Rendering;

/// <summary>Expanded HTML plus the content-type dependencies used to produce it.</summary>
public sealed record PageCompositionExpansion
{
    /// <summary>Gets the independent expanded HTML tree.</summary>
    public HtmlPageContent Content { get; init; } = new();

    /// <summary>Gets the authoritative content-type aliases used during resolution.</summary>
    public IReadOnlyList<string> ContentTypeAliases { get; init; } = [];
}

/// <summary>
/// Expands typed-content scopes into an ephemeral clone of a page HTML tree.
/// </summary>
/// <remarks>
/// Pages owns HTML cloning and binding targets. Content resolution remains behind
/// <see cref="IContentCompositionResolver"/> and never exposes persistence entities.
/// </remarks>
public sealed class PageCompositionExpander
{
    private readonly IContentCompositionResolver _contentResolver;
    private readonly IHtmlContentValidator _contentValidator;
    private readonly IReadOnlyDictionary<PageRenderedFragmentKind, IPageFragmentRenderer> _fragmentRenderers;
    private readonly IPageRegisteredFragmentRegistry? _registeredFragmentRegistry;

    /// <summary>Creates an expander without source-backed fragment strategies.</summary>
    public PageCompositionExpander(
        IContentCompositionResolver contentResolver,
        IHtmlContentValidator contentValidator)
        : this(contentResolver, contentValidator, [], null)
    {
    }

    /// <summary>Creates an expander with the registered source-backed fragment strategies.</summary>
    public PageCompositionExpander(
        IContentCompositionResolver contentResolver,
        IHtmlContentValidator contentValidator,
        IEnumerable<IPageFragmentRenderer> fragmentRenderers)
        : this(contentResolver, contentValidator, fragmentRenderers, null)
    {
    }

    /// <summary>Creates an expander with source-backed and explicitly registered strategies.</summary>
    public PageCompositionExpander(
        IContentCompositionResolver contentResolver,
        IHtmlContentValidator contentValidator,
        IEnumerable<IPageFragmentRenderer> fragmentRenderers,
        IPageRegisteredFragmentRegistry? registeredFragmentRegistry)
    {
        _contentResolver = contentResolver ?? throw new ArgumentNullException(nameof(contentResolver));
        _contentValidator = contentValidator ?? throw new ArgumentNullException(nameof(contentValidator));
        ArgumentNullException.ThrowIfNull(fragmentRenderers);

        var configured = new Dictionary<PageRenderedFragmentKind, IPageFragmentRenderer>();
        foreach (var renderer in fragmentRenderers)
        {
            if (!configured.TryAdd(renderer.Kind, renderer))
            {
                throw new InvalidOperationException(
                    $"More than one page-fragment renderer is registered for '{renderer.Kind}'.");
            }
        }

        _fragmentRenderers = configured;
        _registeredFragmentRegistry = registeredFragmentRegistry;
    }

    /// <summary>
    /// Clones and expands one page composition without mutating the persisted snapshot.
    /// </summary>
    public async Task<Result<PageCompositionExpansion, AeroError>> ExpandAsync(
        long siteId,
        string culture,
        HtmlPageContent content,
        PageCompositionDocument? composition,
        IReadOnlyDictionary<long, int>? pageNumbers = null,
        CancellationToken ct = default,
        PageFragmentRenderContext? fragmentContext = null,
        IReadOnlyDictionary<string, string>? routeValues = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        var candidate = HtmlTreeOperations.ClonePreservingNodeIds(content);
        if (composition is null || IsEmpty(composition))
        {
            return Prelude.Ok<PageCompositionExpansion, AeroError>(new PageCompositionExpansion
            {
                Content = candidate
            });
        }

        var contentTypeAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        fragmentContext ??= new PageFragmentRenderContext
        {
            SiteId = siteId,
            Culture = culture
        };
        var compositionValidation = new PageCompositionValidator(candidate).Validate(composition);
        if (!compositionValidation.IsValid)
        {
            return Prelude.Fail<PageCompositionExpansion, AeroError>(
                AeroError.ValidationError(compositionValidation.Errors
                    .Select(failure => failure.ErrorMessage)
                    .Distinct(StringComparer.Ordinal)));
        }

        var work = BuildCompositionWork(candidate, composition);
        if (work is Result<IReadOnlyList<CompositionWork>, AeroError>.Failure workFailure)
        {
            return Prelude.Fail<PageCompositionExpansion, AeroError>(workFailure.Error);
        }

        foreach (var entry in ((Result<IReadOnlyList<CompositionWork>, AeroError>.Ok)work).Value)
        {
            ct.ThrowIfCancellationRequested();
            Result<bool, AeroError> expansion = entry.RegisteredFragment is not null
                ? await ExpandRegisteredFragmentAsync(
                    candidate,
                    entry.RegisteredFragment,
                    fragmentContext,
                    ct)
                : entry.Fragment is not null
                ? await ExpandFragmentAsync(candidate, entry.Fragment, fragmentContext, ct)
                : entry.List is not null
                ? await ExpandListAsync(
                    siteId,
                    culture,
                    candidate,
                    composition,
                    entry.List,
                    PageFor(entry.NodeId, pageNumbers),
                    contentTypeAliases,
                    ct)
                : await ExpandItemAsync(
                    siteId,
                    culture,
                    candidate,
                    composition,
                    entry.Item!,
                    contentTypeAliases,
                    ct,
                    routeValues);

            if (expansion is Result<bool, AeroError>.Failure expansionFailure)
            {
                return Prelude.Fail<PageCompositionExpansion, AeroError>(expansionFailure.Error);
            }
        }

        var validation = _contentValidator.Validate(candidate);
        return validation switch
        {
            Result<bool>.Ok => Prelude.Ok<PageCompositionExpansion, AeroError>(new PageCompositionExpansion
            {
                Content = candidate,
                ContentTypeAliases = contentTypeAliases.OrderBy(alias => alias, StringComparer.OrdinalIgnoreCase).ToArray()
            }),
            Result<bool>.Failure failure => Prelude.Fail<PageCompositionExpansion, AeroError>(failure.Error),
            _ => Prelude.Fail<PageCompositionExpansion, AeroError>(
                AeroError.CreateError("Unknown expanded HTML validation result state."))
        };
    }

    private async Task<Result<bool, AeroError>> ExpandItemAsync(
        long siteId,
        string culture,
        HtmlPageContent candidate,
        PageCompositionDocument composition,
        PageContentItemScope scope,
        ISet<string> contentTypeAliases,
        CancellationToken ct,
        IReadOnlyDictionary<string, string>? routeValues)
    {
        var scopeNode = HtmlTreeOperations.FindById(candidate.Root, scope.NodeId);
        if (scopeNode is null)
        {
            return Fail($"Content item scope '{scope.NodeId}' no longer exists in the page HTML.");
        }

        var resolvedScope = scope;
        if (!string.IsNullOrWhiteSpace(scope.StableIdRouteParameter))
        {
            var routeValue = routeValues?.FirstOrDefault(pair => string.Equals(
                pair.Key,
                scope.StableIdRouteParameter,
                StringComparison.Ordinal)).Value;
            if (string.IsNullOrWhiteSpace(routeValue) || scope.ContentEntryKey is not { } key)
            {
                return Prelude.Fail<bool, AeroError>(
                    AeroError.NotFoundError($"Route-bound content for scope '{scope.NodeId}' was not found."));
            }

            resolvedScope = scope with
            {
                ContentEntryKey = new Aero.Cms.Abstractions.Content.Views.ContentEntryKey(
                    key.Provider,
                    routeValue)
            };
        }

        var itemResult = await _contentResolver.ResolveItemAsync(siteId, culture, resolvedScope, ct);
        if (itemResult is Result<PublishedContentItemProjection, AeroError>.Failure itemFailure)
        {
            return Prelude.Fail<bool, AeroError>(itemFailure.Error);
        }

        var item = ((Result<PublishedContentItemProjection, AeroError>.Ok)itemResult).Value;
        contentTypeAliases.Add(item.ContentTypeAlias);
        return ApplyBindings(
            scopeNode,
            composition,
            scope.NodeId,
            item,
            nodeMap: null);
    }

    private async Task<Result<bool, AeroError>> ExpandListAsync(
        long siteId,
        string culture,
        HtmlPageContent candidate,
        PageCompositionDocument composition,
        PageContentListScope scope,
        int pageNumber,
        ISet<string> contentTypeAliases,
        CancellationToken ct)
    {
        var scopeNode = HtmlTreeOperations.FindById(candidate.Root, scope.NodeId);
        if (scopeNode is null)
        {
            return Fail($"Content list scope '{scope.NodeId}' no longer exists in the page HTML.");
        }

        var template = HtmlTreeOperations.FindById(scopeNode, scope.TemplateRootNodeId);
        var templateParent = HtmlTreeOperations.FindParentById(scopeNode, scope.TemplateRootNodeId);
        if (template is null || templateParent is null)
        {
            return Fail($"Content list scope '{scope.NodeId}' no longer contains its template node.");
        }

        var listResult = await _contentResolver.ResolveListAsync(siteId, culture, scope, pageNumber, ct);
        if (listResult is Result<PublishedContentPage, AeroError>.Failure listFailure)
        {
            return Prelude.Fail<bool, AeroError>(listFailure.Error);
        }

        var page = ((Result<PublishedContentPage, AeroError>.Ok)listResult).Value;
        if (!string.IsNullOrWhiteSpace(page.ContentTypeAlias))
        {
            contentTypeAliases.Add(page.ContentTypeAlias);
        }

        if (page.Items.Count == 0)
        {
            if (scope.EmptyState == PageContentEmptyStateBehavior.RenderNothing)
            {
                var parent = HtmlTreeOperations.FindParentById(candidate.Root, scope.NodeId);
                if (parent is null)
                {
                    return Fail($"Content list scope '{scope.NodeId}' cannot be removed from the page HTML.");
                }

                parent.Children.Remove(scopeNode);
            }

            return Prelude.Ok<bool, AeroError>(true);
        }

        var templateIndex = templateParent.Children.IndexOf(template);
        if (templateIndex < 0)
        {
            return Fail($"Content list scope '{scope.NodeId}' has an invalid template position.");
        }

        var expanded = new List<HtmlNode>(page.Items.Count);
        foreach (var item in page.Items)
        {
            var nodeMap = new Dictionary<long, HtmlNode>();
            var clone = CloneWithFreshNodeIds(template, nodeMap);
            var binding = ApplyBindings(clone, composition, scope.NodeId, item, nodeMap);
            if (binding is Result<bool, AeroError>.Failure bindingFailure)
            {
                return Prelude.Fail<bool, AeroError>(bindingFailure.Error);
            }

            expanded.Add(clone);
        }

        templateParent.Children.RemoveAt(templateIndex);
        templateParent.Children.InsertRange(templateIndex, expanded);
        return Prelude.Ok<bool, AeroError>(true);
    }

    private static Result<bool, AeroError> ApplyBindings(
        HtmlNode scopeNode,
        PageCompositionDocument composition,
        long scopeNodeId,
        PublishedContentItemProjection item,
        IReadOnlyDictionary<long, HtmlNode>? nodeMap)
    {
        foreach (var binding in (composition.FieldBindings ?? []).Where(binding => binding.ScopeNodeId == scopeNodeId))
        {
            var target = nodeMap is not null && nodeMap.TryGetValue(binding.NodeId, out var mapped)
                ? mapped
                : HtmlTreeOperations.FindById(scopeNode, binding.NodeId);
            if (target is null)
            {
                return Fail($"Field binding node '{binding.NodeId}' no longer exists in scope '{scopeNodeId}'.");
            }

            var value = TryGetField(item.Fields, binding.FieldName, out var field)
                ? ToOutputValue(field)
                : string.Empty;
            var applied = ApplyBinding(target, binding.Target, value);
            if (applied is Result<bool, AeroError>.Failure failure)
            {
                return Prelude.Fail<bool, AeroError>(failure.Error);
            }
        }

        return Prelude.Ok<bool, AeroError>(true);
    }

    private static Result<bool, AeroError> ApplyBinding(
        HtmlNode target,
        PageFieldBindingTarget bindingTarget,
        string value)
    {
        if (bindingTarget == PageFieldBindingTarget.TextContent)
        {
            if (target.Kind == HtmlNodeKind.Text)
            {
                target.Text = value;
                return Prelude.Ok<bool, AeroError>(true);
            }

            if (target.Kind != HtmlNodeKind.Element)
            {
                return Fail($"Node '{target.NodeId}' cannot receive text content.");
            }

            target.Children.Clear();
            target.Children.Add(HtmlNode.CreateText(value));
            return Prelude.Ok<bool, AeroError>(true);
        }

        if (target.Kind != HtmlNodeKind.Element)
        {
            return Fail($"Node '{target.NodeId}' cannot receive an HTML attribute binding.");
        }

        var attributeName = bindingTarget switch
        {
            PageFieldBindingTarget.Hyperlink => "href",
            PageFieldBindingTarget.Source => "src",
            PageFieldBindingTarget.AlternativeText => "alt",
            PageFieldBindingTarget.Title => "title",
            PageFieldBindingTarget.Value => "value",
            _ => null
        };
        if (attributeName is null)
        {
            return Fail($"Node '{target.NodeId}' has an unsupported field-binding target.");
        }

        target.Attributes[attributeName] = value;
        return Prelude.Ok<bool, AeroError>(true);
    }

    private async Task<Result<bool, AeroError>> ExpandFragmentAsync(
        HtmlPageContent candidate,
        PageRenderedFragment fragment,
        PageFragmentRenderContext context,
        CancellationToken cancellationToken)
    {
        var target = HtmlTreeOperations.FindById(candidate.Root, fragment.NodeId);
        if (target is not { Kind: HtmlNodeKind.Element })
        {
            return Fail($"Rendered fragment node '{fragment.NodeId}' no longer exists in the page HTML.");
        }

        if (!_fragmentRenderers.TryGetValue(fragment.Kind, out var renderer))
        {
            return Fail($"No page-fragment renderer is registered for '{fragment.Kind}'.");
        }

        var rendered = await renderer.RenderAsync(fragment, context, cancellationToken);
        if (rendered is Result<HtmlPageContent>.Failure failure)
        {
            return Prelude.Fail<bool, AeroError>(failure.Error);
        }

        target.Children = [.. ((Result<HtmlPageContent>.Ok)rendered).Value.Root.Children];
        return Prelude.Ok<bool, AeroError>(true);
    }

    private async Task<Result<bool, AeroError>> ExpandRegisteredFragmentAsync(
        HtmlPageContent candidate,
        PageRegisteredFragment fragment,
        PageFragmentRenderContext context,
        CancellationToken cancellationToken)
    {
        var target = HtmlTreeOperations.FindById(candidate.Root, fragment.NodeId);
        if (target is not { Kind: HtmlNodeKind.Element })
        {
            return Fail($"Registered fragment node '{fragment.NodeId}' no longer exists in the page HTML.");
        }

        if (_registeredFragmentRegistry is null)
        {
            return Fail($"No registered page-fragment registry can resolve '{fragment.Key}'.");
        }

        var rendered = await _registeredFragmentRegistry.RenderAsync(fragment, context, cancellationToken);
        if (rendered is Result<HtmlPageContent>.Failure failure)
        {
            return Prelude.Fail<bool, AeroError>(failure.Error);
        }

        target.Children = [.. ((Result<HtmlPageContent>.Ok)rendered).Value.Root.Children];
        return Prelude.Ok<bool, AeroError>(true);
    }

    private static Result<IReadOnlyList<CompositionWork>, AeroError> BuildCompositionWork(
        HtmlPageContent content,
        PageCompositionDocument composition)
    {
        var work = new List<CompositionWork>();
        foreach (var list in composition.ContentLists ?? [])
        {
            var depth = FindDepth(content.Root, list.NodeId);
            if (depth < 0)
            {
                return Prelude.Fail<IReadOnlyList<CompositionWork>, AeroError>(
                    AeroError.ValidationError([$"Content list scope '{list.NodeId}' no longer exists in the page HTML."]));
            }

            work.Add(new CompositionWork(list.NodeId, depth, list, null, null, null));
        }

        foreach (var item in composition.ContentItems ?? [])
        {
            var depth = FindDepth(content.Root, item.NodeId);
            if (depth < 0)
            {
                return Prelude.Fail<IReadOnlyList<CompositionWork>, AeroError>(
                    AeroError.ValidationError([$"Content item scope '{item.NodeId}' no longer exists in the page HTML."]));
            }

            work.Add(new CompositionWork(item.NodeId, depth, null, item, null, null));
        }

        foreach (var fragment in composition.RenderedFragments ?? [])
        {
            var depth = FindDepth(content.Root, fragment.NodeId);
            if (depth < 0)
            {
                return Prelude.Fail<IReadOnlyList<CompositionWork>, AeroError>(
                    AeroError.ValidationError([$"Rendered fragment node '{fragment.NodeId}' no longer exists in the page HTML."]));
            }

            work.Add(new CompositionWork(fragment.NodeId, depth, null, null, fragment, null));
        }

        foreach (var fragment in composition.RegisteredFragments ?? [])
        {
            var depth = FindDepth(content.Root, fragment.NodeId);
            if (depth < 0)
            {
                return Prelude.Fail<IReadOnlyList<CompositionWork>, AeroError>(
                    AeroError.ValidationError([$"Registered fragment node '{fragment.NodeId}' no longer exists in the page HTML."]));
            }

            work.Add(new CompositionWork(fragment.NodeId, depth, null, null, null, fragment));
        }

        return Prelude.Ok<IReadOnlyList<CompositionWork>, AeroError>(work
            .OrderByDescending(entry => entry.Depth)
            .ThenBy(entry => entry.Fragment is null && entry.RegisteredFragment is null ? 1 : 0)
            .ThenBy(entry => entry.NodeId)
            .ToArray());
    }

    private static int FindDepth(HtmlNode root, long nodeId, int depth = 0)
    {
        if (root.NodeId == nodeId)
        {
            return depth;
        }

        foreach (var child in root.Children)
        {
            var found = FindDepth(child, nodeId, depth + 1);
            if (found >= 0)
            {
                return found;
            }
        }

        return -1;
    }

    private static HtmlNode CloneWithFreshNodeIds(HtmlNode source, IDictionary<long, HtmlNode> nodeMap)
    {
        var clone = new HtmlNode
        {
            NodeId = Snowflake.NewId(),
            Kind = source.Kind,
            TagName = source.TagName,
            Text = source.Text,
            Attributes = new Dictionary<string, string>(source.Attributes, StringComparer.Ordinal),
            ThemeClasses = [.. source.ThemeClasses],
            Style = HtmlTreeOperations.CloneStyle(source.Style)
        };
        nodeMap[source.NodeId] = clone;
        clone.Children = source.Children.Select(child => CloneWithFreshNodeIds(child, nodeMap)).ToList();
        return clone;
    }

    private static bool TryGetField(
        IReadOnlyDictionary<string, JsonElement> fields,
        string fieldName,
        out JsonElement value)
    {
        if (fields.TryGetValue(fieldName, out value))
        {
            return true;
        }

        foreach (var field in fields)
        {
            if (string.Equals(field.Key, fieldName, StringComparison.OrdinalIgnoreCase))
            {
                value = field.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string ToOutputValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Undefined or JsonValueKind.Null => string.Empty,
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => value.GetRawText()
    };

    private static int PageFor(long scopeNodeId, IReadOnlyDictionary<long, int>? pageNumbers)
        => pageNumbers is not null
           && pageNumbers.TryGetValue(scopeNodeId, out var pageNumber)
           && pageNumber > 0
            ? pageNumber
            : 1;

    private static bool IsEmpty(PageCompositionDocument composition)
        => (composition.ContentLists?.Count ?? 0) == 0
           && (composition.ContentItems?.Count ?? 0) == 0
           && (composition.FieldBindings?.Count ?? 0) == 0
           && (composition.RenderedFragments?.Count ?? 0) == 0
           && (composition.RegisteredFragments?.Count ?? 0) == 0;

    private static Result<bool, AeroError> Fail(string error)
        => Prelude.Fail<bool, AeroError>(AeroError.ValidationError([error]));

    private sealed record CompositionWork(
        long NodeId,
        int Depth,
        PageContentListScope? List,
        PageContentItemScope? Item,
        PageRenderedFragment? Fragment,
        PageRegisteredFragment? RegisteredFragment);
}
