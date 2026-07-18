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
    private readonly IHtmlContentValidator _contentValidator;
    private readonly IHtmlComponentTemplateFactory _componentFactory;
    private readonly IHtmlLayoutStarterFactory _layoutFactory;
    private readonly IStyleCompiler _styleCompiler;
    private readonly IStyleProfile _styleProfile;
    private readonly HtmlTreeEditor _treeEditor;

    public HtmlPageEditorSession(
        HtmlPageContent content,
        HtmlElementCatalog catalog,
        IHtmlContentModelPolicy contentPolicy,
        IHtmlContentValidator contentValidator,
        IHtmlLayoutStarterFactory layoutFactory,
        IHtmlComponentTemplateFactory componentFactory,
        IStyleCompiler styleCompiler,
        IStyleProfile styleProfile)
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
    /// Inserts a validated static fragment at the page root as one undoable mutation.
    /// The fragment importer owns parsing and validation; this session owns editor state.
    /// </summary>
    public Result<IReadOnlyList<HtmlNode>> InsertImportedFragment(HtmlPageContent fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment);

        var importedNodes = fragment.Root.Children;
        var result = _treeEditor.InsertChildren(Content.Root.NodeId, importedNodes);
        if (result is Result<IReadOnlyList<HtmlNode>>.Ok ok)
        {
            SelectedNodeId = ok.Value[0].NodeId;
            RefreshCompiledStyles();
        }

        return result;
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

    public Result<HtmlNode> MoveRelative(
        long nodeId,
        long targetNodeId,
        HtmlRelativePlacement placement)
    {
        var result = _treeEditor.MoveRelative(nodeId, targetNodeId, placement);
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

        var fallbackSelectionId = FindRemovalFallbackSelection(nodeId);
        var result = _treeEditor.Remove(nodeId);
        if (result is Result<HtmlNode>.Ok)
        {
            SelectedNodeId = fallbackSelectionId;
            RefreshCompiledStyles();
        }

        return result;
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
        var duplicate = HtmlTreeOperations.CloneWithFreshNodeIds(source);
        var result = _treeEditor.InsertChild(parent.NodeId, duplicate, sourceIndex + 1);
        if (result is Result<HtmlNode>.Ok)
        {
            SelectedNodeId = duplicate.NodeId;
            RefreshCompiledStyles();
        }

        return result;
    }

    public Result<HtmlNode> UpdateSelectedProperties(HtmlNodeProperties properties)
    {
        if (SelectedNodeId is not { } nodeId)
        {
            return AeroError.NotAllowedError("Select an element before editing its properties.");
        }

        var result = _treeEditor.UpdateProperties(nodeId, properties, ValidateCandidate);
        if (result is Result<HtmlNode>.Ok)
        {
            RefreshCompiledStyles();
        }

        return result;
    }

    public Result<HtmlNode> UpdateSelectedChildren(IReadOnlyList<HtmlNode> children)
    {
        if (SelectedNodeId is not { } nodeId)
        {
            return AeroError.NotAllowedError("Select an element before editing its content.");
        }

        var result = _treeEditor.UpdateChildren(nodeId, children, ValidateCandidate);
        if (result is Result<HtmlNode>.Ok)
        {
            RefreshCompiledStyles();
        }

        return result;
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

    private Result<HtmlNode> InsertRelative(
        HtmlNode node,
        long targetNodeId,
        HtmlRelativePlacement placement)
    {
        var result = _treeEditor.InsertRelative(node, targetNodeId, placement);
        if (result is Result<HtmlNode>.Ok)
        {
            SelectedNodeId = node.NodeId;
            RefreshCompiledStyles();
        }

        return result;
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

        var result = _treeEditor.UpdateStructure(
            table.NodeId,
            candidate => AppendTableColumn(candidate, insideTableHead: false),
            ValidateCandidate);
        if (result is Result<HtmlNode>.Ok)
        {
            SelectedNodeId = table.NodeId;
            RefreshCompiledStyles();
        }

        return result;
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

        var result = _treeEditor.InsertChild(form.NodeId, field);
        if (result is Result<HtmlNode>.Ok)
        {
            SelectedNodeId = control.NodeId;
            RefreshCompiledStyles();
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
        var result = _treeEditor.InsertChild(parentNodeId, child, index);
        if (result is Result<HtmlNode>.Ok)
        {
            SelectedNodeId = child.NodeId;
            RefreshCompiledStyles();
        }

        return result;
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
}
