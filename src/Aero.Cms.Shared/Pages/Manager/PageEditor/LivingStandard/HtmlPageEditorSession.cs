using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Html;
using Aero.Core;
using Aero.Core.Railway;
using System.Text.Json;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>
/// Owns the editable HTML document, typed-content composition, selection, derived
/// styles, and aggregate Memento history for one page-editor session.
/// </summary>
public sealed class HtmlPageEditorSession
{
    private readonly HtmlElementCatalog _catalog;
    private readonly IHtmlContentModelPolicy _contentPolicy;
    private readonly IHtmlContentValidator _contentValidator;
    private readonly IHtmlComponentTemplateFactory _componentFactory;
    private readonly IHtmlLayoutStarterFactory _layoutFactory;
    private readonly IStyleCompiler _styleCompiler;
    private readonly IStyleProfile _styleProfile;
    private readonly PageEditorDocumentHistory _history = new();
    private HtmlTreeEditor _treeEditor;

    public HtmlPageEditorSession(
        HtmlPageContent content,
        HtmlElementCatalog catalog,
        IHtmlContentModelPolicy contentPolicy,
        IHtmlContentValidator contentValidator,
        IHtmlLayoutStarterFactory layoutFactory,
        IHtmlComponentTemplateFactory componentFactory,
        IStyleCompiler styleCompiler,
        IStyleProfile styleProfile,
        PageCompositionDocument? composition = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _contentPolicy = contentPolicy ?? throw new ArgumentNullException(nameof(contentPolicy));
        _contentValidator = contentValidator ?? throw new ArgumentNullException(nameof(contentValidator));
        _layoutFactory = layoutFactory ?? throw new ArgumentNullException(nameof(layoutFactory));
        _componentFactory = componentFactory ?? throw new ArgumentNullException(nameof(componentFactory));
        _styleCompiler = styleCompiler ?? throw new ArgumentNullException(nameof(styleCompiler));
        _styleProfile = styleProfile ?? throw new ArgumentNullException(nameof(styleProfile));
        _treeEditor = new HtmlTreeEditor(content, contentPolicy, validateCandidate: ValidateCandidate);
        Composition = (composition ?? new PageCompositionDocument()).CreateSnapshot();

        RefreshCompiledStyles();
    }

    public HtmlPageContent Content => _treeEditor.Content;

    /// <summary>
    /// Gets the typed-content sidecar that shares this session's undo/redo history.
    /// </summary>
    public PageCompositionDocument Composition { get; private set; }

    public long? SelectedNodeId { get; private set; }

    public HtmlNode? SelectedNode => SelectedNodeId is { } nodeId
        ? HtmlTreeOperations.FindById(Content.Root, nodeId)
        : null;

    public CompiledPageStyles? CompiledStyles { get; private set; }

    public AeroError? StyleCompilationError { get; private set; }

    public bool CanUndo => _history.CanUndo;

    public bool CanRedo => _history.CanRedo;

    /// <summary>
    /// Gets whether the selected node has a preceding sibling in its current parent.
    /// </summary>
    public bool CanMoveSelectedUp => CanMoveSelectedSibling(-1);

    /// <summary>
    /// Gets whether the selected node has a following sibling in its current parent.
    /// </summary>
    public bool CanMoveSelectedDown => CanMoveSelectedSibling(1);

    public void Select(long? nodeId)
    {
        SelectedNodeId = nodeId is { } value
            && HtmlTreeOperations.FindById(Content.Root, value) is not null
                ? value
                : null;
    }

    /// <summary>
    /// Replaces the typed-content sidecar as one undoable editor command.
    /// Structurally orphaned entries are removed against the current HTML tree.
    /// </summary>
    /// <param name="composition">The candidate composition document.</param>
    /// <returns>The independent composition snapshot owned by this session.</returns>
    public Result<PageCompositionDocument> ReplaceComposition(PageCompositionDocument composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        var memento = PageEditorDocumentMemento.Capture(Content, Composition);
        Composition = PageCompositionReconciler.RemoveOrphans(Content, composition);
        _history.CaptureBeforeChange(memento);
        return Composition;
    }

    public Result<HtmlNode> AddElement(string tagName, long? parentNodeId = null)
    {
        if (!_catalog.TryGet(tagName, out var definition) || definition is null)
        {
            return AeroError.ValidationError([$"The HTML element <{tagName}> is not supported by this editor."]);
        }

        var node = CreatePaletteNode(definition);
        return Insert(node, parentNodeId);
    }

    public Result<HtmlNode> AddElementRelative(
        string tagName,
        long targetNodeId,
        HtmlRelativePlacement placement)
    {
        if (!_catalog.TryGet(tagName, out var definition) || definition is null)
        {
            return AeroError.ValidationError([$"The HTML element <{tagName}> is not supported by this editor."]);
        }

        return InsertRelative(CreatePaletteNode(definition), targetNodeId, placement);
    }

    public Result<HtmlNode> AddLayout(HtmlLayoutStarterKind kind, long? parentNodeId = null)
    {
        var starter = _layoutFactory.Create(kind);
        return starter switch
        {
            Result<HtmlNode>.Ok ok => Insert(ok.Value, parentNodeId),
            Result<HtmlNode>.Failure failure => failure,
            _ => AeroError.CreateError("The layout starter returned an unknown result state.")
        };
    }

    public Result<HtmlNode> AddLayoutRelative(
        HtmlLayoutStarterKind kind,
        long targetNodeId,
        HtmlRelativePlacement placement)
    {
        var starter = _layoutFactory.Create(kind);
        return starter switch
        {
            Result<HtmlNode>.Ok ok => InsertRelative(ok.Value, targetNodeId, placement),
            Result<HtmlNode>.Failure failure => failure,
            _ => AeroError.CreateError("The layout starter returned an unknown result state.")
        };
    }

    public Result<HtmlNode> AddComponent(
        HtmlComponentTemplateKind kind,
        long? parentNodeId = null)
    {
        var component = _componentFactory.Create(kind);
        return component switch
        {
            Result<HtmlNode>.Ok ok => Insert(ok.Value, parentNodeId),
            Result<HtmlNode>.Failure failure => failure,
            _ => AeroError.CreateError("The component template returned an unknown result state.")
        };
    }

    public Result<HtmlNode> AddComponentRelative(
        HtmlComponentTemplateKind kind,
        long targetNodeId,
        HtmlRelativePlacement placement)
    {
        var component = _componentFactory.Create(kind);
        return component switch
        {
            Result<HtmlNode>.Ok ok => InsertRelative(ok.Value, targetNodeId, placement),
            Result<HtmlNode>.Failure failure => failure,
            _ => AeroError.CreateError("The component template returned an unknown result state.")
        };
    }

    /// <summary>
    /// Adds an ordinary HTML container backed by one source-rendered fragment.
    /// </summary>
    public Result<HtmlNode> AddRenderedFragment(
        PageRenderedFragmentKind kind,
        string source,
        long? parentNodeId = null)
    {
        var validation = ValidateRenderedFragment(kind, source);
        if (validation is Result<bool>.Failure failure)
        {
            return failure.Error;
        }

        var node = CreateRenderedFragmentNode(kind);
        return Insert(
            node,
            parentNodeId,
            (inserted, composition) => AddRenderedFragmentEntry(
                composition,
                inserted.NodeId,
                kind,
                source));
    }

    /// <summary>
    /// Adds a source-rendered fragment at an explicit canvas drop location.
    /// </summary>
    public Result<HtmlNode> AddRenderedFragmentRelative(
        PageRenderedFragmentKind kind,
        string source,
        long targetNodeId,
        HtmlRelativePlacement placement)
    {
        var validation = ValidateRenderedFragment(kind, source);
        if (validation is Result<bool>.Failure failure)
        {
            return failure.Error;
        }

        var node = CreateRenderedFragmentNode(kind);
        return InsertRelative(
            node,
            targetNodeId,
            placement,
            (inserted, composition) => AddRenderedFragmentEntry(
                composition,
                inserted.NodeId,
                kind,
                source));
    }

    /// <summary>Adds an ordinary HTML container backed by an explicit application-fragment key.</summary>
    public Result<HtmlNode> AddRegisteredFragment(
        string key,
        IReadOnlyDictionary<string, JsonElement>? parameters = null,
        long? parentNodeId = null)
    {
        if ((Composition.RegisteredFragments?.Count ?? 0)
            >= PageRegisteredFragment.MaximumFragmentsPerPage)
        {
            return AeroError.ValidationError(
                [$"A page cannot contain more than {PageRegisteredFragment.MaximumFragmentsPerPage} registered fragments."]);
        }

        var validation = ValidateRegisteredFragment(key, parameters);
        if (validation is Result<string>.Failure failure)
        {
            return failure.Error;
        }

        var normalizedKey = ((Result<string>.Ok)validation).Value;
        return Insert(
            CreateRegisteredFragmentNode(normalizedKey),
            parentNodeId,
            (inserted, composition) => AddRegisteredFragmentEntry(
                composition,
                inserted.NodeId,
                normalizedKey,
                parameters));
    }

    /// <summary>Adds a registered fragment at an explicit canvas drop location.</summary>
    public Result<HtmlNode> AddRegisteredFragmentRelative(
        string key,
        IReadOnlyDictionary<string, JsonElement>? parameters,
        long targetNodeId,
        HtmlRelativePlacement placement)
    {
        if ((Composition.RegisteredFragments?.Count ?? 0)
            >= PageRegisteredFragment.MaximumFragmentsPerPage)
        {
            return AeroError.ValidationError(
                [$"A page cannot contain more than {PageRegisteredFragment.MaximumFragmentsPerPage} registered fragments."]);
        }

        var validation = ValidateRegisteredFragment(key, parameters);
        if (validation is Result<string>.Failure failure)
        {
            return failure.Error;
        }

        var normalizedKey = ((Result<string>.Ok)validation).Value;
        return InsertRelative(
            CreateRegisteredFragmentNode(normalizedKey),
            targetNodeId,
            placement,
            (inserted, composition) => AddRegisteredFragmentEntry(
                composition,
                inserted.NodeId,
                normalizedKey,
                parameters));
    }

    /// <summary>
    /// Adds a pageable typed-content scope using an ordinary HTML template subtree.
    /// </summary>
    public Result<HtmlNode> AddContentList(
        long contentTypeId,
        string contentTypeAlias,
        long? parentNodeId = null)
    {
        var identityValidation = ValidateContentTypeIdentity(contentTypeId, contentTypeAlias);
        if (identityValidation is Result<bool>.Failure failure)
        {
            return failure.Error;
        }

        var scopeNode = CreateContentListScopeNode(out var templateRootNodeId);
        return Insert(
            scopeNode,
            parentNodeId,
            (inserted, composition) => AddContentListScope(
                composition,
                inserted.NodeId,
                templateRootNodeId,
                contentTypeId,
                contentTypeAlias));
    }

    /// <summary>
    /// Adds a pageable typed-content scope at an explicit canvas drop location.
    /// </summary>
    public Result<HtmlNode> AddContentListRelative(
        long contentTypeId,
        string contentTypeAlias,
        long targetNodeId,
        HtmlRelativePlacement placement)
    {
        var identityValidation = ValidateContentTypeIdentity(contentTypeId, contentTypeAlias);
        if (identityValidation is Result<bool>.Failure failure)
        {
            return failure.Error;
        }

        var scopeNode = CreateContentListScopeNode(out var templateRootNodeId);
        return InsertRelative(
            scopeNode,
            targetNodeId,
            placement,
            (inserted, composition) => AddContentListScope(
                composition,
                inserted.NodeId,
                templateRootNodeId,
                contentTypeId,
                contentTypeAlias));
    }

    /// <summary>
    /// Adds a stable typed-content item scope using an ordinary HTML container.
    /// </summary>
    public Result<HtmlNode> AddContentItem(
        long contentTypeId,
        string contentTypeAlias,
        long contentItemId,
        string? contentItemSlug,
        string? contentItemTitle,
        long? parentNodeId = null)
    {
        var identityValidation = ValidateContentItemIdentity(
            contentTypeId,
            contentTypeAlias,
            contentItemId);
        if (identityValidation is Result<bool>.Failure failure)
        {
            return failure.Error;
        }

        var scopeNode = CreateContentItemScopeNode();
        return Insert(
            scopeNode,
            parentNodeId,
            (inserted, composition) => AddContentItemScope(
                composition,
                inserted.NodeId,
                contentTypeId,
                contentTypeAlias,
                contentItemId,
                contentItemSlug));
    }

    /// <summary>
    /// Adds a stable typed-content item scope at an explicit canvas drop location.
    /// </summary>
    public Result<HtmlNode> AddContentItemRelative(
        long contentTypeId,
        string contentTypeAlias,
        long contentItemId,
        string? contentItemSlug,
        string? contentItemTitle,
        long targetNodeId,
        HtmlRelativePlacement placement)
    {
        var identityValidation = ValidateContentItemIdentity(
            contentTypeId,
            contentTypeAlias,
            contentItemId);
        if (identityValidation is Result<bool>.Failure failure)
        {
            return failure.Error;
        }

        var scopeNode = CreateContentItemScopeNode();
        return InsertRelative(
            scopeNode,
            targetNodeId,
            placement,
            (inserted, composition) => AddContentItemScope(
                composition,
                inserted.NodeId,
                contentTypeId,
                contentTypeAlias,
                contentItemId,
                contentItemSlug));
    }

    /// <summary>
    /// Adds a field output target inside the nearest matching content scope.
    /// </summary>
    public Result<HtmlNode> AddContentField(
        long contentTypeId,
        string fieldName,
        string fieldType,
        string? fieldLabel,
        long? parentNodeId = null)
    {
        var fieldValidation = ValidateContentField(contentTypeId, fieldName, fieldType);
        if (fieldValidation is Result<bool>.Failure failure)
        {
            return failure.Error;
        }

        var fieldNode = CreateContentFieldNode(fieldName, fieldType, fieldLabel, out var bindingTarget);
        var effectiveParentNodeId = ResolveContentFieldParent(
            contentTypeId,
            parentNodeId ?? SelectedNodeId);
        var insertionParentNodeId = FindInsertionParent(fieldNode, effectiveParentNodeId);
        if (insertionParentNodeId is null)
        {
            return AeroError.NotAllowedError("This field cannot be added at the selected location.");
        }

        var scopeResult = FindCompatibleContentScope(insertionParentNodeId.Value, contentTypeId);
        if (scopeResult is Result<ContentScopeMatch>.Failure scopeFailure)
        {
            return scopeFailure.Error;
        }

        var scope = ((Result<ContentScopeMatch>.Ok)scopeResult).Value;
        return Insert(
            fieldNode,
            insertionParentNodeId,
            (inserted, composition) => AddFieldBinding(
                composition,
                inserted.NodeId,
                scope.ScopeNodeId,
                fieldName,
                bindingTarget));
    }

    /// <summary>
    /// Adds a field output target at an explicit drop location inside a matching content scope.
    /// </summary>
    public Result<HtmlNode> AddContentFieldRelative(
        long contentTypeId,
        string fieldName,
        string fieldType,
        string? fieldLabel,
        long targetNodeId,
        HtmlRelativePlacement placement)
    {
        var fieldValidation = ValidateContentField(contentTypeId, fieldName, fieldType);
        if (fieldValidation is Result<bool>.Failure failure)
        {
            return failure.Error;
        }

        var effectiveTargetNodeId = ResolveContentFieldDropTarget(
            contentTypeId,
            targetNodeId,
            placement);
        var insertionParentNodeId = FindRelativeInsertionParent(effectiveTargetNodeId, placement);
        if (insertionParentNodeId is null)
        {
            return AeroError.NotAllowedError("This field cannot be added at the requested location.");
        }

        var scopeResult = FindCompatibleContentScope(insertionParentNodeId.Value, contentTypeId);
        if (scopeResult is Result<ContentScopeMatch>.Failure scopeFailure)
        {
            return scopeFailure.Error;
        }

        var scope = ((Result<ContentScopeMatch>.Ok)scopeResult).Value;
        var fieldNode = CreateContentFieldNode(fieldName, fieldType, fieldLabel, out var bindingTarget);
        return InsertRelative(
            fieldNode,
            effectiveTargetNodeId,
            placement,
            (inserted, composition) => AddFieldBinding(
                composition,
                inserted.NodeId,
                scope.ScopeNodeId,
                fieldName,
                bindingTarget));
    }

    /// <summary>
    /// Updates one pageable content-list scope without rewriting its HTML template.
    /// </summary>
    public Result<PageContentListScope> UpdateContentListSettings(
        long scopeNodeId,
        PageContentListQuery query,
        PageContentEmptyStateBehavior emptyState)
    {
        ArgumentNullException.ThrowIfNull(query);

        var scope = (Composition.ContentLists ?? [])
            .FirstOrDefault(candidate => candidate.NodeId == scopeNodeId);
        var scopeNode = scope is null
            ? null
            : HtmlTreeOperations.FindById(Content.Root, scope.NodeId);
        if (scope is null
            || scopeNode is null
            || HtmlTreeOperations.FindById(scopeNode, scope.TemplateRootNodeId) is null)
        {
            return AeroError.NotFoundError("The selected content-list scope no longer exists.");
        }

        if (!Enum.IsDefined(emptyState))
        {
            return AeroError.ValidationError(["The selected empty-state behavior is not supported."]);
        }

        var queryResult = NormalizeContentListQuery(query);
        if (queryResult is Result<PageContentListQuery>.Failure queryFailure)
        {
            return queryFailure.Error;
        }

        var normalizedQuery = ((Result<PageContentListQuery>.Ok)queryResult).Value;
        var updatedScope = scope with
        {
            Query = normalizedQuery,
            EmptyState = emptyState
        };
        var updatedLists = (Composition.ContentLists ?? [])
            .Select(candidate => candidate.NodeId == scopeNodeId ? updatedScope : candidate)
            .ToArray();
        var memento = PageEditorDocumentMemento.Capture(Content, Composition);
        Composition = PageCompositionReconciler.RemoveOrphans(
            Content,
            Composition with { ContentLists = updatedLists });
        _history.CaptureBeforeChange(memento);
        return updatedScope;
    }

    /// <summary>
    /// Replaces the authoring source for one rendered fragment as an undoable change.
    /// </summary>
    public Result<PageRenderedFragment> UpdateRenderedFragmentSource(
        long nodeId,
        string source)
    {
        var fragment = (Composition.RenderedFragments ?? [])
            .FirstOrDefault(candidate => candidate.NodeId == nodeId);
        if (fragment is null || HtmlTreeOperations.FindById(Content.Root, nodeId) is null)
        {
            return AeroError.NotFoundError("The selected rendered fragment no longer exists.");
        }

        var validation = ValidateRenderedFragment(fragment.Kind, source);
        if (validation is Result<bool>.Failure failure)
        {
            return failure.Error;
        }

        var updated = fragment with { Source = source };
        var fragments = (Composition.RenderedFragments ?? [])
            .Select(candidate => candidate.NodeId == nodeId ? updated : candidate)
            .ToArray();
        var memento = PageEditorDocumentMemento.Capture(Content, Composition);
        Composition = PageCompositionReconciler.RemoveOrphans(
            Content,
            Composition with { RenderedFragments = fragments });
        _history.CaptureBeforeChange(memento);
        return updated;
    }

    /// <summary>Replaces registered-fragment parameters as one aggregate history command.</summary>
    public Result<PageRegisteredFragment> UpdateRegisteredFragmentParameters(
        long nodeId,
        IReadOnlyDictionary<string, JsonElement> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var fragment = (Composition.RegisteredFragments ?? [])
            .FirstOrDefault(candidate => candidate.NodeId == nodeId);
        if (fragment is null || HtmlTreeOperations.FindById(Content.Root, nodeId) is null)
        {
            return AeroError.NotFoundError("The selected registered fragment no longer exists.");
        }

        var validation = ValidateRegisteredFragment(fragment.Key, parameters);
        if (validation is Result<string>.Failure failure)
        {
            return failure.Error;
        }

        var updated = fragment with
        {
            Parameters = parameters.ToDictionary(
                parameter => parameter.Key,
                parameter => parameter.Value.Clone(),
                StringComparer.Ordinal)
        };
        var fragments = (Composition.RegisteredFragments ?? [])
            .Select(candidate => candidate.NodeId == nodeId ? updated : candidate)
            .ToArray();
        var memento = PageEditorDocumentMemento.Capture(Content, Composition);
        Composition = PageCompositionReconciler.RemoveOrphans(
            Content,
            Composition with { RegisteredFragments = fragments });
        _history.CaptureBeforeChange(memento);
        return updated;
    }

    /// <summary>
    /// Inserts a validated static fragment at the page root as one undoable mutation.
    /// The fragment importer owns parsing and validation; this session owns editor state.
    /// </summary>
    public Result<IReadOnlyList<HtmlNode>> InsertImportedFragment(HtmlPageContent fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment);

        var importedNodes = fragment.Root.Children;
        return ExecuteDocumentMutation(
            () => _treeEditor.InsertChildren(Content.Root.NodeId, importedNodes),
            insertedNodes =>
            {
                SelectedNodeId = insertedNodes[0].NodeId;
                RefreshCompiledStyles();
            });
    }

    public Result<HtmlNode> Move(long nodeId, long destinationParentNodeId, int destinationIndex)
    {
        return ExecuteDocumentMutation(
            () => _treeEditor.Move(nodeId, destinationParentNodeId, destinationIndex),
            _ =>
            {
                SelectedNodeId = nodeId;
                RefreshCompiledStyles();
            });
    }

    public Result<HtmlNode> MoveRelative(
        long nodeId,
        long targetNodeId,
        HtmlRelativePlacement placement)
    {
        return ExecuteDocumentMutation(
            () => _treeEditor.MoveRelative(nodeId, targetNodeId, placement),
            _ =>
            {
                SelectedNodeId = nodeId;
                RefreshCompiledStyles();
            });
    }

    /// <summary>
    /// Moves the selected subtree immediately before its preceding sibling.
    /// </summary>
    /// <returns>The moved node, or a railway error when movement is unavailable.</returns>
    public Result<HtmlNode> MoveSelectedUp() => MoveSelectedSibling(-1);

    /// <summary>
    /// Moves the selected subtree immediately after its following sibling.
    /// </summary>
    /// <returns>The moved node, or a railway error when movement is unavailable.</returns>
    public Result<HtmlNode> MoveSelectedDown() => MoveSelectedSibling(1);

    public Result<HtmlNode> RemoveSelected()
    {
        if (SelectedNodeId is not { } nodeId)
        {
            return AeroError.NotAllowedError("Select an element before removing it.");
        }

        var fallbackSelectionId = FindRemovalFallbackSelection(nodeId);
        return ExecuteDocumentMutation(
            () => _treeEditor.Remove(nodeId),
            _ =>
            {
                SelectedNodeId = fallbackSelectionId;
                RefreshCompiledStyles();
            });
    }

    private long? FindRemovalFallbackSelection(long nodeId)
    {
        var parent = HtmlTreeOperations.FindParentById(Content.Root, nodeId);
        if (parent is null)
        {
            return null;
        }

        var index = parent.Children.FindIndex(child => child.NodeId == nodeId);
        if (index < 0)
        {
            return null;
        }

        if (index + 1 < parent.Children.Count)
        {
            return parent.Children[index + 1].NodeId;
        }

        if (index > 0)
        {
            return parent.Children[index - 1].NodeId;
        }

        return parent.Kind is HtmlNodeKind.Fragment ? null : parent.NodeId;
    }

    private bool CanMoveSelectedSibling(int offset)
    {
        if (SelectedNodeId is not { } nodeId)
        {
            return false;
        }

        var parent = HtmlTreeOperations.FindParentById(Content.Root, nodeId);
        var index = parent?.Children.FindIndex(child => child.NodeId == nodeId) ?? -1;
        var targetIndex = index + offset;
        return parent is not null && index >= 0 && targetIndex >= 0 && targetIndex < parent.Children.Count;
    }

    private Result<HtmlNode> MoveSelectedSibling(int offset)
    {
        if (SelectedNodeId is not { } nodeId)
        {
            return AeroError.NotAllowedError("Select an element before moving it.");
        }

        var parent = HtmlTreeOperations.FindParentById(Content.Root, nodeId);
        var index = parent?.Children.FindIndex(child => child.NodeId == nodeId) ?? -1;
        var targetIndex = index + offset;
        if (parent is null || index < 0 || targetIndex < 0 || targetIndex >= parent.Children.Count)
        {
            return AeroError.NotAllowedError(offset < 0
                ? "The selected element is already first in its container."
                : "The selected element is already last in its container.");
        }

        var target = parent.Children[targetIndex];
        return MoveRelative(
            nodeId,
            target.NodeId,
            offset < 0 ? HtmlRelativePlacement.Before : HtmlRelativePlacement.After);
    }

    public Result<HtmlNode> DuplicateSelected()
    {
        if (SelectedNodeId is not { } nodeId)
        {
            return AeroError.NotAllowedError("Select an element before duplicating it.");
        }

        if (Content.Root.NodeId == nodeId)
        {
            return AeroError.NotAllowedError("The page fragment root cannot be duplicated.");
        }

        var source = HtmlTreeOperations.FindById(Content.Root, nodeId);
        var parent = HtmlTreeOperations.FindParentById(Content.Root, nodeId);
        if (source is null || parent is null)
        {
            return AeroError.NotFoundError($"The node {nodeId} was not found.");
        }

        var sourceIndex = parent.Children.FindIndex(child => child.NodeId == nodeId);
        var nodeMap = new Dictionary<long, HtmlNode>();
        var duplicate = CloneWithFreshNodeIds(source, nodeMap);
        return ExecuteDocumentMutation(
            () => _treeEditor.InsertChild(parent.NodeId, duplicate, sourceIndex + 1),
            _ =>
            {
                SelectedNodeId = duplicate.NodeId;
                RefreshCompiledStyles();
            },
            (_, composition) => DuplicateCompositionFragments(composition, source, nodeMap));
    }

    public Result<HtmlNode> UpdateSelectedProperties(HtmlNodeProperties properties)
    {
        if (SelectedNodeId is not { } nodeId)
        {
            return AeroError.NotAllowedError("Select an element before editing its properties.");
        }

        return ExecuteDocumentMutation(
            () => _treeEditor.UpdateProperties(nodeId, properties, ValidateCandidate),
            _ => RefreshCompiledStyles());
    }

    public Result<HtmlNode> UpdateSelectedChildren(IReadOnlyList<HtmlNode> children)
    {
        if (SelectedNodeId is not { } nodeId)
        {
            return AeroError.NotAllowedError("Select an element before editing its content.");
        }

        return ExecuteDocumentMutation(
            () => _treeEditor.UpdateChildren(nodeId, children, ValidateCandidate),
            _ => RefreshCompiledStyles());
    }

    public Result<HtmlNode> ApplySelectedCollectionAction(HtmlCollectionActionKind action)
    {
        if (SelectedNodeId is not { } nodeId)
        {
            return AeroError.NotAllowedError("Select an element before editing its structure.");
        }

        return action switch
        {
            HtmlCollectionActionKind.AddListItem => AddListItem(nodeId),
            HtmlCollectionActionKind.AddTableRow => AddTableRow(nodeId),
            HtmlCollectionActionKind.AddTableColumn => AddTableColumn(nodeId),
            HtmlCollectionActionKind.AddMediaSource => AddMediaSource(nodeId),
            HtmlCollectionActionKind.AddMediaTrack => AddMediaTrack(nodeId),
            HtmlCollectionActionKind.AddFormInput => AddFormControl(nodeId, "input"),
            HtmlCollectionActionKind.AddFormTextArea => AddFormControl(nodeId, "textarea"),
            HtmlCollectionActionKind.AddFormSelect => AddFormControl(nodeId, "select"),
            HtmlCollectionActionKind.AddSelectOption => AddSelectOption(nodeId),
            HtmlCollectionActionKind.AddOptionGroup => AddOptionGroup(nodeId),
            _ => AeroError.ValidationError(["The requested structure action is not supported."])
        };
    }

    public Result<HtmlPageContent> Undo()
    {
        var result = _history.Undo(Content, Composition);
        if (result is Result<PageEditorDocumentState>.Ok restored)
        {
            Restore(restored.Value);
            ClearMissingSelection();
            RefreshCompiledStyles();
            return Content;
        }

        return ((Result<PageEditorDocumentState>.Failure)result).Error;
    }

    public Result<HtmlPageContent> Redo()
    {
        var result = _history.Redo(Content, Composition);
        if (result is Result<PageEditorDocumentState>.Ok restored)
        {
            Restore(restored.Value);
            ClearMissingSelection();
            RefreshCompiledStyles();
            return Content;
        }

        return ((Result<PageEditorDocumentState>.Failure)result).Error;
    }

    private Result<T> ExecuteDocumentMutation<T>(
        Func<Result<T>> mutation,
        Action<T>? onSuccess = null,
        Func<T, PageCompositionDocument, PageCompositionDocument>? updateComposition = null)
    {
        ArgumentNullException.ThrowIfNull(mutation);

        var memento = PageEditorDocumentMemento.Capture(Content, Composition);
        var result = mutation();
        if (result is Result<T>.Ok ok)
        {
            var candidateComposition = updateComposition?.Invoke(ok.Value, Composition) ?? Composition;
            Composition = PageCompositionReconciler.RemoveOrphans(Content, candidateComposition);
            _history.CaptureBeforeChange(memento);
            onSuccess?.Invoke(ok.Value);
        }

        return result;
    }

    private void Restore(PageEditorDocumentState state)
    {
        _treeEditor = new HtmlTreeEditor(
            state.Content,
            _contentPolicy,
            validateCandidate: ValidateCandidate);
        Composition = state.Composition;
    }

    private Result<HtmlNode> Insert(
        HtmlNode node,
        long? requestedParentNodeId,
        Func<HtmlNode, PageCompositionDocument, PageCompositionDocument>? updateComposition = null)
    {
        var parentNodeId = FindInsertionParent(node, requestedParentNodeId);
        if (parentNodeId is null)
        {
            return AeroError.NotAllowedError($"<{node.TagName}> cannot be added at the selected location.");
        }

        return ExecuteDocumentMutation(
            () => _treeEditor.InsertChild(parentNodeId.Value, node),
            _ =>
            {
                SelectedNodeId = node.NodeId;
                RefreshCompiledStyles();
            },
            updateComposition);
    }

    private Result<HtmlNode> InsertRelative(
        HtmlNode node,
        long targetNodeId,
        HtmlRelativePlacement placement,
        Func<HtmlNode, PageCompositionDocument, PageCompositionDocument>? updateComposition = null)
    {
        return ExecuteDocumentMutation(
            () => _treeEditor.InsertRelative(node, targetNodeId, placement),
            _ =>
            {
                SelectedNodeId = node.NodeId;
                RefreshCompiledStyles();
            },
            updateComposition);
    }

    private Result<HtmlNode> AddListItem(long selectedNodeId)
    {
        var list = FindAncestorOrSelf(selectedNodeId, node => node.TagName is "ul" or "ol");
        if (list is null)
        {
            return AeroError.NotAllowedError("Select a bulleted or numbered list before adding an item.");
        }

        var item = _catalog.CreateElement("li");
        item.Children.Add(HtmlNode.CreateText("List item"));
        return InsertGuidedChild(list.NodeId, item);
    }

    private Result<HtmlNode> AddTableRow(long selectedNodeId)
    {
        var selected = HtmlTreeOperations.FindById(Content.Root, selectedNodeId)!;
        HtmlNode? container;
        if (selected.TagName is "tr" or "th" or "td")
        {
            var row = FindAncestorOrSelf(selectedNodeId, node => node.TagName == "tr");
            container = row is null
                ? null
                : HtmlTreeOperations.FindParentById(Content.Root, row.NodeId);
        }
        else
        {
            container = FindAncestorOrSelf(
                selectedNodeId,
                node => node.TagName is "table" or "thead" or "tbody" or "tfoot");
        }

        if (container is null)
        {
            return AeroError.NotAllowedError("Select a table or table row before adding a row.");
        }

        if (container.TagName == "table")
        {
            container = container.Children.FirstOrDefault(child => child.TagName == "tbody") ?? container;
        }

        var columnCount = DescendantsAndSelf(container)
            .Where(node => node.TagName == "tr")
            .Select(row => row.Children.Count(child => child.TagName is "th" or "td"))
            .DefaultIfEmpty(2)
            .Max();
        columnCount = Math.Max(columnCount, 1);

        var rowTag = container.TagName == "thead" ? "th" : "td";
        var newRow = _catalog.CreateElement("tr");
        for (var column = 1; column <= columnCount; column++)
        {
            newRow.Children.Add(CreateTableCell(
                rowTag,
                rowTag == "th" ? $"Header {column}" : $"Cell {column}",
                rowTag == "th" ? "col" : null));
        }

        return InsertGuidedChild(container.NodeId, newRow);
    }

    private Result<HtmlNode> AddTableColumn(long selectedNodeId)
    {
        var table = FindAncestorOrSelf(selectedNodeId, node => node.TagName == "table");
        if (table is null)
        {
            return AeroError.NotAllowedError("Select a table before adding a column.");
        }

        if (!DescendantsAndSelf(table).Any(node => node.TagName == "tr"))
        {
            return AeroError.NotAllowedError("Add a table row before adding a column.");
        }

        return ExecuteDocumentMutation(
            () => _treeEditor.UpdateStructure(
                table.NodeId,
                candidate => AppendTableColumn(candidate, insideTableHead: false),
                ValidateCandidate),
            _ =>
            {
                SelectedNodeId = table.NodeId;
                RefreshCompiledStyles();
            });
    }

    private Result<HtmlNode> AddMediaSource(long selectedNodeId)
    {
        var media = FindAncestorOrSelf(
            selectedNodeId,
            node => node.TagName is "picture" or "audio" or "video");
        if (media is null)
        {
            return AeroError.NotAllowedError("Select a picture, audio, or video element before adding a source.");
        }

        var source = _catalog.CreateElement("source");
        switch (media.TagName)
        {
            case "picture":
                source.Attributes["media"] = "(min-width: 48rem)";
                source.Attributes["srcset"] = "/media/image-large.jpg 1280w";
                break;
            case "audio":
                source.Attributes["src"] = "/media/audio.mp3";
                source.Attributes["type"] = "audio/mpeg";
                break;
            default:
                source.Attributes["src"] = "/media/video.mp4";
                source.Attributes["type"] = "video/mp4";
                break;
        }

        var insertionIndex = media.Children.FindIndex(child =>
            media.TagName == "picture" ? child.TagName == "img" : child.TagName == "track");
        return InsertGuidedChild(
            media.NodeId,
            source,
            insertionIndex < 0 ? null : insertionIndex);
    }

    private Result<HtmlNode> AddMediaTrack(long selectedNodeId)
    {
        var media = FindAncestorOrSelf(
            selectedNodeId,
            node => node.TagName is "audio" or "video");
        if (media is null)
        {
            return AeroError.NotAllowedError("Select an audio or video element before adding captions.");
        }

        var track = _catalog.CreateElement("track");
        track.Attributes["kind"] = "captions";
        track.Attributes["src"] = "/media/captions.vtt";
        track.Attributes["srclang"] = "en";
        track.Attributes["label"] = "English";
        return InsertGuidedChild(media.NodeId, track);
    }

    private Result<HtmlNode> AddFormControl(long selectedNodeId, string controlTag)
    {
        var form = FindAncestorOrSelf(selectedNodeId, node => node.TagName == "form");
        if (form is null)
        {
            return AeroError.NotAllowedError("Select a form before adding a field.");
        }

        var control = _catalog.CreateElement(controlTag);
        var controlId = $"field-{control.NodeId}";
        control.Attributes["id"] = controlId;
        control.Attributes["name"] = controlTag switch
        {
            "textarea" => "message",
            "select" => "selection",
            _ => "field"
        };

        switch (controlTag)
        {
            case "input":
                control.Attributes["type"] = "text";
                control.Attributes["placeholder"] = "Enter a value";
                break;
            case "textarea":
                control.Attributes["rows"] = "5";
                control.Attributes["placeholder"] = "Enter your message";
                break;
            case "select":
                control.Children.Add(CreateOption("option-1", "Option 1"));
                break;
        }

        var label = _catalog.CreateElement("label");
        label.Attributes["for"] = controlId;
        label.Children.Add(HtmlNode.CreateText(controlTag switch
        {
            "textarea" => "Message",
            "select" => "Choose an option",
            _ => "Field label"
        }));

        var field = _catalog.CreateElement("div");
        field.Style = new HtmlStyle
        {
            Display = CssDisplay.Flex,
            FlexDirection = CssFlexDirection.Column,
            Gap = CssLength.Rem(0.5m)
        };
        field.Children.Add(label);
        field.Children.Add(control);

        var result = ExecuteDocumentMutation(
            () => _treeEditor.InsertChild(form.NodeId, field),
            _ =>
            {
                SelectedNodeId = control.NodeId;
                RefreshCompiledStyles();
            });
        if (result is Result<HtmlNode>.Ok)
        {
            return control;
        }

        return result;
    }

    private Result<HtmlNode> AddSelectOption(long selectedNodeId)
    {
        var optionContainer = FindAncestorOrSelf(
            selectedNodeId,
            node => node.TagName is "select" or "optgroup" or "datalist");
        if (optionContainer is null)
        {
            return AeroError.NotAllowedError("Select a choice list, option group, or suggested-values list before adding an option.");
        }

        var number = optionContainer.Children.Count(child => child.TagName == "option") + 1;
        return InsertGuidedChild(
            optionContainer.NodeId,
            CreateOption($"option-{number}", $"Option {number}"));
    }

    private Result<HtmlNode> AddOptionGroup(long selectedNodeId)
    {
        var select = FindAncestorOrSelf(selectedNodeId, node => node.TagName == "select");
        if (select is null)
        {
            return AeroError.NotAllowedError("Select a choice list before adding an option group.");
        }

        var number = select.Children.Count(child => child.TagName == "optgroup") + 1;
        var optionGroup = _catalog.CreateElement("optgroup");
        optionGroup.Attributes["label"] = $"Option group {number}";
        optionGroup.Children.Add(CreateOption($"group-{number}-option-1", "Option 1"));
        return InsertGuidedChild(select.NodeId, optionGroup);
    }

    private Result<HtmlNode> InsertGuidedChild(long parentNodeId, HtmlNode child, int? index = null)
    {
        return ExecuteDocumentMutation(
            () => _treeEditor.InsertChild(parentNodeId, child, index),
            _ =>
            {
                SelectedNodeId = child.NodeId;
                RefreshCompiledStyles();
            });
    }

    private HtmlNode? FindAncestorOrSelf(long nodeId, Func<HtmlNode, bool> predicate)
    {
        var current = HtmlTreeOperations.FindById(Content.Root, nodeId);
        while (current is not null)
        {
            if (predicate(current))
            {
                return current;
            }

            current = HtmlTreeOperations.FindParentById(Content.Root, current.NodeId);
        }

        return null;
    }

    private void AppendTableColumn(HtmlNode node, bool insideTableHead)
    {
        var isInsideTableHead = insideTableHead || node.TagName == "thead";
        if (node.TagName == "colgroup")
        {
            node.Children.Add(_catalog.CreateElement("col"));
        }

        if (node.TagName == "tr")
        {
            var cellCount = node.Children.Count(child => child.TagName is "th" or "td") + 1;
            var isHeaderRow = isInsideTableHead
                || (node.Children.Count > 0 && node.Children.All(child => child.TagName == "th"));
            node.Children.Add(CreateTableCell(
                isHeaderRow ? "th" : "td",
                isHeaderRow ? $"Header {cellCount}" : $"Cell {cellCount}",
                isHeaderRow ? "col" : null));
        }

        foreach (var child in node.Children)
        {
            AppendTableColumn(child, isInsideTableHead);
        }
    }

    private static IEnumerable<HtmlNode> DescendantsAndSelf(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var descendant in DescendantsAndSelf(child))
            {
                yield return descendant;
            }
        }
    }

    private static Result<bool> ValidateContentTypeIdentity(
        long contentTypeId,
        string contentTypeAlias)
    {
        var errors = new List<string>();
        if (contentTypeId <= 0)
        {
            errors.Add("Select a persisted content type before adding a content scope.");
        }

        if (string.IsNullOrWhiteSpace(contentTypeAlias))
        {
            errors.Add("The selected content type does not have a stable alias.");
        }

        return errors.Count == 0
            ? true
            : AeroError.ValidationError(errors);
    }

    private static Result<bool> ValidateContentItemIdentity(
        long contentTypeId,
        string contentTypeAlias,
        long contentItemId)
    {
        var contentTypeValidation = ValidateContentTypeIdentity(contentTypeId, contentTypeAlias);
        if (contentTypeValidation is Result<bool>.Failure failure)
        {
            return failure.Error;
        }

        return contentItemId > 0
            ? true
            : AeroError.ValidationError(["Select a persisted content item before adding an item scope."]);
    }

    private static Result<bool> ValidateContentField(
        long contentTypeId,
        string fieldName,
        string fieldType)
    {
        var errors = new List<string>();
        if (contentTypeId <= 0)
        {
            errors.Add("Select a persisted content type before adding a field binding.");
        }

        if (string.IsNullOrWhiteSpace(fieldName))
        {
            errors.Add("The selected content field does not have a stable name.");
        }

        if (string.IsNullOrWhiteSpace(fieldType))
        {
            errors.Add("The selected content field does not have a field type.");
        }

        return errors.Count == 0
            ? true
            : AeroError.ValidationError(errors);
    }

    private static Result<PageContentListQuery> NormalizeContentListQuery(
        PageContentListQuery query)
    {
        var errors = new List<string>();
        if (query.PageSize is < PageContentListQuery.MinimumPageSize
            or > PageContentListQuery.MaximumPageSize)
        {
            errors.Add(
                $"Page size must be between {PageContentListQuery.MinimumPageSize} " +
                $"and {PageContentListQuery.MaximumPageSize}.");
        }

        if (!Enum.IsDefined(query.SortDirection))
        {
            errors.Add("The selected sort direction is not supported.");
        }

        var filters = query.Filters ?? [];
        if (filters.Count > PageContentListQuery.MaximumFilterCount)
        {
            errors.Add($"A content list can contain at most {PageContentListQuery.MaximumFilterCount} filters.");
        }

        var normalizedFilters = new List<PageContentFilter>(filters.Count);
        foreach (var filter in filters)
        {
            if (filter is null || string.IsNullOrWhiteSpace(filter.FieldName))
            {
                errors.Add("Every content filter must select a field.");
                continue;
            }

            if (!Enum.IsDefined(filter.Operator))
            {
                errors.Add($"The filter on '{filter.FieldName}' has an unsupported operator.");
                continue;
            }

            var requiresValue = filter.Operator is not PageContentFilterOperator.IsEmpty
                and not PageContentFilterOperator.IsNotEmpty;
            if (requiresValue && string.IsNullOrWhiteSpace(filter.Value))
            {
                errors.Add($"The filter on '{filter.FieldName}' requires a comparison value.");
                continue;
            }

            normalizedFilters.Add(filter with
            {
                FieldName = filter.FieldName.Trim(),
                Value = requiresValue ? filter.Value!.Trim() : null
            });
        }

        if (errors.Count > 0)
        {
            return AeroError.ValidationError(errors);
        }

        return query with
        {
            SortField = string.IsNullOrWhiteSpace(query.SortField) ? null : query.SortField.Trim(),
            Filters = normalizedFilters.ToArray()
        };
    }

    private HtmlNode CreateContentListScopeNode(out long templateRootNodeId)
    {
        var scope = _catalog.CreateElement("section");
        var template = _catalog.CreateElement("article");
        scope.Children.Add(template);
        templateRootNodeId = template.NodeId;
        return scope;
    }

    private HtmlNode CreateContentItemScopeNode() => _catalog.CreateElement("section");

    private HtmlNode CreateRenderedFragmentNode(PageRenderedFragmentKind kind)
    {
        var container = _catalog.CreateElement("section");
        var placeholder = _catalog.CreateElement("p");
        placeholder.Children.Add(HtmlNode.CreateText(kind switch
        {
            PageRenderedFragmentKind.Markdown => "Markdown block — double-click to edit",
            PageRenderedFragmentKind.CustomHtml => "Custom HTML block — double-click to edit",
            PageRenderedFragmentKind.Scriban => "Scriban block — double-click to edit",
            PageRenderedFragmentKind.SharpTs => "TS block — double-click to edit",
            PageRenderedFragmentKind.Htmx => "HTMX block — double-click to edit",
            _ => "Rendered block — double-click to edit"
        }));
        container.Children.Add(placeholder);
        return container;
    }

    private HtmlNode CreateRegisteredFragmentNode(string key)
    {
        var container = _catalog.CreateElement("section");
        var placeholder = _catalog.CreateElement("p");
        placeholder.Children.Add(HtmlNode.CreateText(
            $"{key} application fragment — double-click to edit"));
        container.Children.Add(placeholder);
        return container;
    }

    private HtmlNode CreateContentFieldNode(
        string fieldName,
        string fieldType,
        string? fieldLabel,
        out PageFieldBindingTarget bindingTarget)
    {
        var label = string.IsNullOrWhiteSpace(fieldLabel) ? fieldName : fieldLabel;
        switch (fieldType.Trim().ToLowerInvariant())
        {
            case "image":
            case "media":
            {
                var image = _catalog.CreateElement("img");
                image.Attributes["src"] = "/media/image.jpg";
                image.Attributes["alt"] = label;
                bindingTarget = PageFieldBindingTarget.Source;
                return image;
            }
            case "url":
            {
                var link = _catalog.CreateElement("a");
                link.Attributes["href"] = "#";
                link.Children.Add(HtmlNode.CreateText(label));
                bindingTarget = PageFieldBindingTarget.Hyperlink;
                return link;
            }
            case "richtext":
            {
                var container = _catalog.CreateElement("div");
                container.Children.Add(HtmlNode.CreateText(label));
                bindingTarget = PageFieldBindingTarget.TextContent;
                return container;
            }
            default:
            {
                var paragraph = _catalog.CreateElement("p");
                paragraph.Children.Add(HtmlNode.CreateText(label));
                bindingTarget = PageFieldBindingTarget.TextContent;
                return paragraph;
            }
        }
    }

    private static PageCompositionDocument AddContentListScope(
        PageCompositionDocument composition,
        long scopeNodeId,
        long templateRootNodeId,
        long contentTypeId,
        string contentTypeAlias) => composition with
    {
        ContentLists =
        [
            .. (composition.ContentLists ?? []),
            new PageContentListScope
            {
                NodeId = scopeNodeId,
                TemplateRootNodeId = templateRootNodeId,
                ContentTypeId = contentTypeId,
                ContentTypeAlias = contentTypeAlias,
                Query = new PageContentListQuery { PageSize = 10 },
                EmptyState = PageContentEmptyStateBehavior.RenderNothing
            }
        ]
    };

    private static PageCompositionDocument AddRenderedFragmentEntry(
        PageCompositionDocument composition,
        long nodeId,
        PageRenderedFragmentKind kind,
        string source) => composition with
    {
        RenderedFragments =
        [
            .. (composition.RenderedFragments ?? []),
            new PageRenderedFragment
            {
                NodeId = nodeId,
                Kind = kind,
                Source = source
            }
        ]
    };

    private static Result<bool> ValidateRenderedFragment(
        PageRenderedFragmentKind kind,
        string source)
    {
        if (!Enum.IsDefined(kind))
        {
            return AeroError.ValidationError(["The rendered-fragment type is not supported."]);
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            return AeroError.ValidationError(["Rendered-fragment source cannot be empty."]);
        }

        if (source.Length > PageRenderedFragment.MaximumSourceLength)
        {
            return AeroError.ValidationError(
                [$"Rendered-fragment source cannot exceed {PageRenderedFragment.MaximumSourceLength} characters."]);
        }

        return true;
    }

    private Result<string> ValidateRegisteredFragment(
        string key,
        IReadOnlyDictionary<string, JsonElement>? parameters)
    {
        var normalizedKey = PageRegisteredFragment.NormalizeKey(key);
        if (!PageRegisteredFragment.IsValidKey(normalizedKey))
        {
            return AeroError.ValidationError(["The registered-fragment key is invalid."]);
        }

        var values = parameters ?? new Dictionary<string, JsonElement>();
        int parameterSize;
        try
        {
            parameterSize = JsonSerializer.SerializeToUtf8Bytes(values).Length;
        }
        catch (Exception)
        {
            parameterSize = int.MaxValue;
        }

        if (values.Count > PageRegisteredFragment.MaximumParameterCount
            || values.Keys.Any(name => string.IsNullOrWhiteSpace(name)
                || name.Length > PageRegisteredFragment.MaximumParameterNameLength)
            || parameterSize > PageRegisteredFragment.MaximumParametersUtf8Bytes)
        {
            return AeroError.ValidationError(["The registered-fragment parameters exceed their bounds."]);
        }

        return normalizedKey;
    }

    private static PageCompositionDocument AddRegisteredFragmentEntry(
        PageCompositionDocument composition,
        long nodeId,
        string key,
        IReadOnlyDictionary<string, JsonElement>? parameters) => composition with
    {
        RegisteredFragments =
        [
            .. (composition.RegisteredFragments ?? []),
            new PageRegisteredFragment
            {
                NodeId = nodeId,
                Key = key,
                Parameters = (parameters ?? new Dictionary<string, JsonElement>())
                    .ToDictionary(
                        parameter => parameter.Key,
                        parameter => parameter.Value.Clone(),
                        StringComparer.Ordinal)
            }
        ]
    };

    private static PageCompositionDocument DuplicateCompositionFragments(
        PageCompositionDocument composition,
        HtmlNode source,
        IReadOnlyDictionary<long, HtmlNode> nodeMap)
    {
        composition = DuplicateRenderedFragments(composition, source, nodeMap);
        var duplicated = (composition.RegisteredFragments ?? [])
            .Where(fragment => HtmlTreeOperations.FindById(source, fragment.NodeId) is not null)
            .Where(fragment => nodeMap.ContainsKey(fragment.NodeId))
            .Select(fragment => fragment.CreateSnapshot() with
            {
                NodeId = nodeMap[fragment.NodeId].NodeId
            })
            .ToArray();
        return duplicated.Length == 0
            ? composition
            : composition with
            {
                RegisteredFragments = [.. (composition.RegisteredFragments ?? []), .. duplicated]
            };
    }

    private static PageCompositionDocument DuplicateRenderedFragments(
        PageCompositionDocument composition,
        HtmlNode source,
        IReadOnlyDictionary<long, HtmlNode> nodeMap)
    {
        var duplicated = (composition.RenderedFragments ?? [])
            .Where(fragment => HtmlTreeOperations.FindById(source, fragment.NodeId) is not null)
            .Where(fragment => nodeMap.ContainsKey(fragment.NodeId))
            .Select(fragment => fragment with { NodeId = nodeMap[fragment.NodeId].NodeId })
            .ToArray();
        return duplicated.Length == 0
            ? composition
            : composition with
            {
                RenderedFragments = [.. (composition.RenderedFragments ?? []), .. duplicated]
            };
    }

    private static HtmlNode CloneWithFreshNodeIds(
        HtmlNode source,
        IDictionary<long, HtmlNode> nodeMap)
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
        clone.Children = source.Children
            .Select(child => CloneWithFreshNodeIds(child, nodeMap))
            .ToList();
        return clone;
    }

    private static PageCompositionDocument AddContentItemScope(
        PageCompositionDocument composition,
        long scopeNodeId,
        long contentTypeId,
        string contentTypeAlias,
        long contentItemId,
        string? contentItemSlug) => composition with
    {
        ContentItems =
        [
            .. (composition.ContentItems ?? []),
            new PageContentItemScope
            {
                NodeId = scopeNodeId,
                ContentTypeId = contentTypeId,
                ContentTypeAlias = contentTypeAlias,
                LookupMode = PageContentItemLookupMode.StableId,
                ContentItemId = contentItemId,
                Slug = contentItemSlug
            }
        ]
    };

    private static PageCompositionDocument AddFieldBinding(
        PageCompositionDocument composition,
        long nodeId,
        long scopeNodeId,
        string fieldName,
        PageFieldBindingTarget target) => composition with
    {
        FieldBindings =
        [
            .. (composition.FieldBindings ?? []),
            new PageFieldBinding
            {
                NodeId = nodeId,
                ScopeNodeId = scopeNodeId,
                FieldName = fieldName,
                Target = target
            }
        ]
    };

    private long? FindRelativeInsertionParent(
        long targetNodeId,
        HtmlRelativePlacement placement)
    {
        var target = HtmlTreeOperations.FindById(Content.Root, targetNodeId);
        if (target is null)
        {
            return null;
        }

        return placement == HtmlRelativePlacement.Inside
            ? target.NodeId
            : HtmlTreeOperations.FindParentById(Content.Root, targetNodeId)?.NodeId;
    }

    private long? ResolveContentFieldParent(long contentTypeId, long? requestedParentNodeId)
    {
        if (requestedParentNodeId is not { } parentNodeId)
        {
            return null;
        }

        var selectedList = (Composition.ContentLists ?? []).FirstOrDefault(scope =>
            scope.NodeId == parentNodeId
            && scope.ContentTypeId == contentTypeId);
        return selectedList?.TemplateRootNodeId ?? parentNodeId;
    }

    private long ResolveContentFieldDropTarget(
        long contentTypeId,
        long targetNodeId,
        HtmlRelativePlacement placement)
    {
        if (placement != HtmlRelativePlacement.Inside)
        {
            return targetNodeId;
        }

        var targetList = (Composition.ContentLists ?? []).FirstOrDefault(scope =>
            scope.NodeId == targetNodeId
            && scope.ContentTypeId == contentTypeId);
        return targetList?.TemplateRootNodeId ?? targetNodeId;
    }

    private Result<ContentScopeMatch> FindCompatibleContentScope(
        long insertionParentNodeId,
        long contentTypeId)
    {
        var current = HtmlTreeOperations.FindById(Content.Root, insertionParentNodeId);
        while (current is not null)
        {
            var listScope = (Composition.ContentLists ?? [])
                .FirstOrDefault(scope => scope.NodeId == current.NodeId);
            if (listScope is not null)
            {
                if (listScope.ContentTypeId != contentTypeId)
                {
                    return AeroError.NotAllowedError(
                        "This field belongs to a different content type than the nearest content scope.");
                }

                var templateRoot = HtmlTreeOperations.FindById(current, listScope.TemplateRootNodeId);
                if (templateRoot is null
                    || HtmlTreeOperations.FindById(templateRoot, insertionParentNodeId) is null)
                {
                    return AeroError.NotAllowedError(
                        "List fields must be placed inside the repeatable template.");
                }

                return new ContentScopeMatch(listScope.NodeId);
            }

            var itemScope = (Composition.ContentItems ?? [])
                .FirstOrDefault(scope => scope.NodeId == current.NodeId);
            if (itemScope is not null)
            {
                return itemScope.ContentTypeId == contentTypeId
                    ? new ContentScopeMatch(itemScope.NodeId)
                    : AeroError.NotAllowedError(
                        "This field belongs to a different content type than the nearest content scope.");
            }

            current = HtmlTreeOperations.FindParentById(Content.Root, current.NodeId);
        }

        return AeroError.NotAllowedError(
            "Drag this field inside a content list template or content item of the same type.");
    }

    private long? FindInsertionParent(HtmlNode child, long? requestedParentNodeId)
    {
        var candidateId = requestedParentNodeId ?? SelectedNodeId;
        if (candidateId is { } selectedId)
        {
            var selected = HtmlTreeOperations.FindById(Content.Root, selectedId);
            if (selected is not null && _contentPolicy.CanContain(selected, child).IsAllowed)
            {
                return selected.NodeId;
            }

            var parent = HtmlTreeOperations.FindParentById(Content.Root, selectedId);
            if (parent is not null && _contentPolicy.CanContain(parent, child).IsAllowed)
            {
                return parent.NodeId;
            }

            if (requestedParentNodeId is not null)
            {
                return null;
            }
        }

        return _contentPolicy.CanContain(Content.Root, child).IsAllowed
            ? Content.Root.NodeId
            : null;
    }

    private HtmlNode CreatePaletteNode(HtmlElementDefinition definition)
    {
        var node = _catalog.CreateElement(definition.Tag);
        if (definition.IsVoid)
        {
            if (definition.Tag.Equals("img", StringComparison.OrdinalIgnoreCase))
            {
                node.Attributes["alt"] = string.Empty;
            }

            if (definition.Tag.Equals("input", StringComparison.OrdinalIgnoreCase))
            {
                node.Attributes["type"] = "text";
                node.Attributes["name"] = "field";
            }

            return node;
        }

        var defaultText = definition.Tag.ToLowerInvariant() switch
        {
            "h1" => "Page heading",
            "h2" or "h3" or "h4" or "h5" or "h6" => "Section heading",
            "p" => "Start writing here...",
            "span" => "Text",
            "strong" => "Strong text",
            "em" => "Emphasized text",
            "a" => "Link text",
            "button" => "Button",
            "label" => "Field label",
            "legend" => "Field group",
            "output" => "Calculated result",
            "pre" => "Preformatted text",
            "code" => "code",
            "small" => "Small print",
            "s" => "No longer accurate",
            "sub" => "subscript",
            "sup" => "superscript",
            "mark" => "Highlighted text",
            "abbr" => "Abbreviation",
            "cite" => "Citation",
            "q" => "Inline quotation",
            "time" => "January 1, 2026",
            "data" => "42",
            "kbd" => "Ctrl + K",
            "samp" => "Command completed.",
            "var" => "x",
            "del" => "Removed text",
            "ins" => "Inserted text",
            "progress" => "50%",
            "meter" => "70%",
            _ => null
        };

        if (defaultText is not null)
        {
            node.Children.Add(HtmlNode.CreateText(defaultText));
        }

        if (definition.Tag.Equals("a", StringComparison.OrdinalIgnoreCase))
        {
            node.Attributes["href"] = "#";
        }

        if (definition.Tag is "ul" or "ol")
        {
            var item = _catalog.CreateElement("li");
            item.Children.Add(HtmlNode.CreateText("List item"));
            node.Children.Add(item);
        }

        if (definition.Tag.Equals("blockquote", StringComparison.OrdinalIgnoreCase))
        {
            var paragraph = _catalog.CreateElement("p");
            paragraph.Children.Add(HtmlNode.CreateText("Add a memorable quotation."));
            node.Children.Add(paragraph);
        }

        if (definition.Tag.Equals("address", StringComparison.OrdinalIgnoreCase))
        {
            var paragraph = _catalog.CreateElement("p");
            paragraph.Children.Add(HtmlNode.CreateText("123 Main Street, City, State"));
            node.Children.Add(paragraph);
        }

        if (definition.Tag.Equals("time", StringComparison.OrdinalIgnoreCase))
        {
            node.Attributes["datetime"] = "2026-01-01";
        }

        if (definition.Tag.Equals("data", StringComparison.OrdinalIgnoreCase))
        {
            node.Attributes["value"] = "42";
        }

        if (definition.Tag.Equals("progress", StringComparison.OrdinalIgnoreCase))
        {
            node.Attributes["value"] = "0.5";
            node.Attributes["max"] = "1";
        }

        if (definition.Tag.Equals("meter", StringComparison.OrdinalIgnoreCase))
        {
            node.Attributes["value"] = "0.7";
            node.Attributes["min"] = "0";
            node.Attributes["max"] = "1";
            node.Attributes["low"] = "0.3";
            node.Attributes["high"] = "0.8";
            node.Attributes["optimum"] = "1";
        }

        if (definition.Tag.Equals("dl", StringComparison.OrdinalIgnoreCase))
        {
            var term = _catalog.CreateElement("dt");
            term.Children.Add(HtmlNode.CreateText("Term"));
            var description = _catalog.CreateElement("dd");
            description.Children.Add(HtmlNode.CreateText("Description"));
            node.Children.Add(term);
            node.Children.Add(description);
        }

        if (definition.Tag.Equals("details", StringComparison.OrdinalIgnoreCase))
        {
            var summary = _catalog.CreateElement("summary");
            summary.Children.Add(HtmlNode.CreateText("Show more"));
            var paragraph = _catalog.CreateElement("p");
            paragraph.Children.Add(HtmlNode.CreateText("Add the additional details here."));
            node.Children.Add(summary);
            node.Children.Add(paragraph);
        }

        if (definition.Tag.Equals("dialog", StringComparison.OrdinalIgnoreCase))
        {
            node.Attributes["open"] = string.Empty;
            var heading = _catalog.CreateElement("h2");
            heading.Children.Add(HtmlNode.CreateText("Dialog heading"));
            var paragraph = _catalog.CreateElement("p");
            paragraph.Children.Add(HtmlNode.CreateText("Add dialog content here."));
            node.Children.Add(heading);
            node.Children.Add(paragraph);
        }

        if (definition.Tag.Equals("picture", StringComparison.OrdinalIgnoreCase))
        {
            var source = _catalog.CreateElement("source");
            source.Attributes["media"] = "(min-width: 48rem)";
            source.Attributes["srcset"] = "/media/image-large.jpg 1280w";
            var image = _catalog.CreateElement("img");
            image.Attributes["src"] = "/media/image.jpg";
            image.Attributes["alt"] = "Describe this image";
            image.Attributes["loading"] = "lazy";
            node.Children.Add(source);
            node.Children.Add(image);
        }

        if (definition.Tag is "audio" or "video")
        {
            node.Attributes["controls"] = string.Empty;
            node.Attributes["preload"] = "metadata";
            var source = _catalog.CreateElement("source");
            source.Attributes["src"] = definition.Tag.Equals("audio", StringComparison.OrdinalIgnoreCase)
                ? "/media/audio.mp3"
                : "/media/video.mp4";
            source.Attributes["type"] = definition.Tag.Equals("audio", StringComparison.OrdinalIgnoreCase)
                ? "audio/mpeg"
                : "video/mp4";
            node.Children.Add(source);
        }

        if (definition.Tag.Equals("table", StringComparison.OrdinalIgnoreCase))
        {
            AddDefaultTableContent(node);
        }

        if (definition.Tag.Equals("form", StringComparison.OrdinalIgnoreCase))
        {
            AddDefaultFormContent(node);
        }

        if (definition.Tag.Equals("select", StringComparison.OrdinalIgnoreCase))
        {
            node.Children.Add(CreateOption("option-1", "Option 1"));
        }

        if (definition.Tag.Equals("fieldset", StringComparison.OrdinalIgnoreCase))
        {
            AddDefaultFieldsetContent(node);
        }

        if (definition.Tag.Equals("datalist", StringComparison.OrdinalIgnoreCase))
        {
            node.Children.Add(CreateOption("suggestion-1", "Suggestion 1"));
            node.Children.Add(CreateOption("suggestion-2", "Suggestion 2"));
        }

        if (definition.Tag.Equals("output", StringComparison.OrdinalIgnoreCase))
        {
            node.Attributes["name"] = "result";
        }

        return node;
    }

    private void AddDefaultTableContent(HtmlNode table)
    {
        var caption = _catalog.CreateElement("caption");
        caption.Children.Add(HtmlNode.CreateText("Table caption"));

        var columnGroup = _catalog.CreateElement("colgroup");
        columnGroup.Children.Add(_catalog.CreateElement("col"));
        columnGroup.Children.Add(_catalog.CreateElement("col"));

        var head = _catalog.CreateElement("thead");
        var headRow = _catalog.CreateElement("tr");
        headRow.Children.Add(CreateTableCell("th", "Header 1", "col"));
        headRow.Children.Add(CreateTableCell("th", "Header 2", "col"));
        head.Children.Add(headRow);

        var body = _catalog.CreateElement("tbody");
        var bodyRow = _catalog.CreateElement("tr");
        bodyRow.Children.Add(CreateTableCell("td", "Cell 1"));
        bodyRow.Children.Add(CreateTableCell("td", "Cell 2"));
        body.Children.Add(bodyRow);

        var foot = _catalog.CreateElement("tfoot");
        var footRow = _catalog.CreateElement("tr");
        footRow.Children.Add(CreateTableCell("td", "Total"));
        footRow.Children.Add(CreateTableCell("td", "0"));
        foot.Children.Add(footRow);

        table.Children.Add(caption);
        table.Children.Add(columnGroup);
        table.Children.Add(head);
        table.Children.Add(body);
        table.Children.Add(foot);
    }

    private HtmlNode CreateTableCell(string tag, string text, string? scope = null)
    {
        var cell = _catalog.CreateElement(tag);
        if (scope is not null)
        {
            cell.Attributes["scope"] = scope;
        }

        cell.Children.Add(HtmlNode.CreateText(text));
        return cell;
    }

    private void AddDefaultFormContent(HtmlNode form)
    {
        var input = _catalog.CreateElement("input");
        var inputId = $"field-{input.NodeId}";
        input.Attributes["id"] = inputId;
        input.Attributes["type"] = "text";
        input.Attributes["name"] = "field";
        input.Attributes["placeholder"] = "Enter a value";

        var label = _catalog.CreateElement("label");
        label.Attributes["for"] = inputId;
        label.Children.Add(HtmlNode.CreateText("Field label"));

        var button = _catalog.CreateElement("button");
        button.Attributes["type"] = "submit";
        button.Children.Add(HtmlNode.CreateText("Submit"));

        form.Children.Add(label);
        form.Children.Add(input);
        form.Children.Add(button);
    }

    private void AddDefaultFieldsetContent(HtmlNode fieldset)
    {
        var legend = _catalog.CreateElement("legend");
        legend.Children.Add(HtmlNode.CreateText("Field group"));

        var input = _catalog.CreateElement("input");
        var inputId = $"field-{input.NodeId}";
        input.Attributes["id"] = inputId;
        input.Attributes["type"] = "text";
        input.Attributes["name"] = "field";

        var label = _catalog.CreateElement("label");
        label.Attributes["for"] = inputId;
        label.Children.Add(HtmlNode.CreateText("Field label"));

        fieldset.Children.Add(legend);
        fieldset.Children.Add(label);
        fieldset.Children.Add(input);
    }

    private HtmlNode CreateOption(string value, string text)
    {
        var option = _catalog.CreateElement("option");
        option.Attributes["value"] = value;
        option.Children.Add(HtmlNode.CreateText(text));
        return option;
    }

    private void ClearMissingSelection()
    {
        if (SelectedNodeId is { } nodeId
            && HtmlTreeOperations.FindById(Content.Root, nodeId) is null)
        {
            SelectedNodeId = null;
        }
    }

    private void RefreshCompiledStyles()
    {
        var result = _styleCompiler.Compile(Content, _styleProfile);
        switch (result)
        {
            case Result<CompiledPageStyles>.Ok ok:
                CompiledStyles = ok.Value;
                StyleCompilationError = null;
                break;
            case Result<CompiledPageStyles>.Failure failure:
                CompiledStyles = null;
                StyleCompilationError = failure.Error;
                break;
        }
    }

    private Result<bool> ValidateCandidate(HtmlPageContent content)
    {
        var contentValidation = _contentValidator.Validate(content);
        if (contentValidation is Result<bool>.Failure contentFailure)
        {
            return contentFailure;
        }

        var styleCompilation = _styleCompiler.Compile(content, _styleProfile);
        return styleCompilation switch
        {
            Result<CompiledPageStyles>.Ok => new Result<bool>.Ok(true),
            Result<CompiledPageStyles>.Failure styleFailure => styleFailure.Error,
            _ => AeroError.CreateError("Style compilation returned an unknown result state.")
        };
    }

    private sealed record ContentScopeMatch(long ScopeNodeId);
}
