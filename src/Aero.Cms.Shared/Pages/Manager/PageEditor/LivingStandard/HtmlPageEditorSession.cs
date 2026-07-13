using Aero.Cms.Html;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>
/// Owns the editable HTML document, selection, derived styles, and Memento history
/// for one page-editor session.
/// </summary>
public sealed class HtmlPageEditorSession
{
    private readonly HtmlElementCatalog _catalog;
    private readonly IHtmlContentModelPolicy _contentPolicy;
    private readonly IHtmlLayoutStarterFactory _layoutFactory;
    private readonly IStyleCompiler _styleCompiler;
    private readonly IStyleProfile _styleProfile;
    private readonly HtmlTreeEditor _treeEditor;

    public HtmlPageEditorSession(
        HtmlPageContent content,
        HtmlElementCatalog catalog,
        IHtmlContentModelPolicy contentPolicy,
        IHtmlLayoutStarterFactory layoutFactory,
        IStyleCompiler styleCompiler,
        IStyleProfile styleProfile)
    {
        ArgumentNullException.ThrowIfNull(content);
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _contentPolicy = contentPolicy ?? throw new ArgumentNullException(nameof(contentPolicy));
        _layoutFactory = layoutFactory ?? throw new ArgumentNullException(nameof(layoutFactory));
        _styleCompiler = styleCompiler ?? throw new ArgumentNullException(nameof(styleCompiler));
        _styleProfile = styleProfile ?? throw new ArgumentNullException(nameof(styleProfile));
        _treeEditor = new HtmlTreeEditor(content, contentPolicy);

        RefreshCompiledStyles();
    }

    public HtmlPageContent Content => _treeEditor.Content;

    public long? SelectedNodeId { get; private set; }

    public HtmlNode? SelectedNode => SelectedNodeId is { } nodeId
        ? HtmlTreeOperations.FindById(Content.Root, nodeId)
        : null;

    public CompiledPageStyles? CompiledStyles { get; private set; }

    public AeroError? StyleCompilationError { get; private set; }

    public bool CanUndo => _treeEditor.History.CanUndo;

    public bool CanRedo => _treeEditor.History.CanRedo;

    public void Select(long? nodeId)
    {
        SelectedNodeId = nodeId is { } value
            && HtmlTreeOperations.FindById(Content.Root, value) is not null
                ? value
                : null;
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

    public Result<HtmlNode> Move(long nodeId, long destinationParentNodeId, int destinationIndex)
    {
        var result = _treeEditor.Move(nodeId, destinationParentNodeId, destinationIndex);
        if (result is Result<HtmlNode>.Ok)
        {
            SelectedNodeId = nodeId;
            RefreshCompiledStyles();
        }

        return result;
    }

    public Result<HtmlNode> RemoveSelected()
    {
        if (SelectedNodeId is not { } nodeId)
        {
            return AeroError.NotAllowedError("Select an element before removing it.");
        }

        var result = _treeEditor.Remove(nodeId);
        if (result is Result<HtmlNode>.Ok)
        {
            SelectedNodeId = null;
            RefreshCompiledStyles();
        }

        return result;
    }

    public Result<HtmlPageContent> Undo()
    {
        var result = _treeEditor.Undo();
        if (result is Result<HtmlPageContent>.Ok)
        {
            ClearMissingSelection();
            RefreshCompiledStyles();
        }

        return result;
    }

    public Result<HtmlPageContent> Redo()
    {
        var result = _treeEditor.Redo();
        if (result is Result<HtmlPageContent>.Ok)
        {
            ClearMissingSelection();
            RefreshCompiledStyles();
        }

        return result;
    }

    private Result<HtmlNode> Insert(HtmlNode node, long? requestedParentNodeId)
    {
        var parentNodeId = FindInsertionParent(node, requestedParentNodeId);
        if (parentNodeId is null)
        {
            return AeroError.NotAllowedError($"<{node.TagName}> cannot be added at the selected location.");
        }

        var result = _treeEditor.InsertChild(parentNodeId.Value, node);
        if (result is Result<HtmlNode>.Ok)
        {
            SelectedNodeId = node.NodeId;
            RefreshCompiledStyles();
        }

        return result;
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

        return node;
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
}
