using System.Text.RegularExpressions;
using Aero.Cms.Html;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>
/// Projects an HTML node into editable attribute and style fields, then returns a detached
/// <see cref="HtmlNodeProperties"/> update to the owning editor.
/// </summary>
/// <remarks>
/// The panel exposes controls only for attributes and style capabilities allowed by the
/// selected element definition. It does not mutate <see cref="Node"/> directly; the owner
/// validates and applies the emitted property model.
/// Password inputs are static HTML controls: their default value is read from and written
/// to the element's persisted <c>value</c> attribute. This inspector is not secret storage,
/// and callers must not place credentials or other secrets in that field.
/// </remarks>
public partial class HtmlElementPropertyPanel
{
    /// <summary>
    /// Gets the HTML input-type tokens offered by the inspector.
    /// </summary>
    /// <remarks>
    /// The <c>password</c> token changes browser presentation only. Its default value remains
    /// persisted and rendered HTML content rather than a protected secret.
    /// </remarks>
    protected static readonly string[] InputTypes =
    [
        "text", "email", "tel", "url", "number", "password", "checkbox", "radio",
        "date", "time", "datetime-local", "month", "week", "color", "range", "hidden"
    ];

    private readonly TiptapInlineContentConverter _richTextConverter = new();
    private HtmlNode? _sourceNode;

    /// <summary>
    /// Gets or sets the selected node whose attributes and styles are being inspected.
    /// </summary>
    [Parameter, EditorRequired]
    public HtmlNode Node { get; set; } = null!;

    /// <summary>
    /// Gets or sets the catalog definition that constrains the controls exposed for
    /// <see cref="Node"/>.
    /// </summary>
    [Parameter, EditorRequired]
    public HtmlElementDefinition Definition { get; set; } = null!;

    /// <summary>
    /// Gets or sets an owner-provided validation error to display without discarding form state.
    /// </summary>
    [Parameter]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the callback that receives a detached property update assembled from the
    /// current form.
    /// </summary>
    [Parameter]
    public EventCallback<HtmlNodeProperties> PropertiesChanged { get; set; }

    /// <summary>
    /// Gets or sets the callback that requests clearing the current editor selection.
    /// </summary>
    [Parameter]
    public EventCallback SelectionCleared { get; set; }

    /// <summary>
    /// Gets or sets the callback that requests the constrained rich-text workflow.
    /// </summary>
    [Parameter]
    public EventCallback RichTextRequested { get; set; }

    /// <summary>
    /// Gets or sets the callback that requests a guided collection mutation.
    /// </summary>
    [Parameter]
    public EventCallback<HtmlCollectionActionKind> CollectionActionRequested { get; set; }

    /// <summary>
    /// Gets or sets the callback that requests duplication of the selected subtree.
    /// </summary>
    [Parameter]
    public EventCallback DuplicateRequested { get; set; }

    /// <summary>
    /// Gets or sets the callback that requests removal of the selected subtree.
    /// </summary>
    [Parameter]
    public EventCallback RemoveRequested { get; set; }

    /// <summary>
    /// Gets or sets the callback that requests media selection for a specific node property.
    /// </summary>
    [Parameter]
    public EventCallback<HtmlMediaTargetKind> MediaRequested { get; set; }

    /// <summary>
    /// Gets the mutable inspector form loaded from the current node.
    /// </summary>
    protected InspectorForm Form { get; private set; } = new();

    /// <summary>
    /// Gets whether link-specific attributes should be exposed.
    /// </summary>
    protected bool IsLink => Definition.Tag.Equals("a", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether image-specific attributes should be exposed.
    /// </summary>
    protected bool IsImage => Definition.Tag.Equals("img", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether shared audio and video attributes should be exposed.
    /// </summary>
    protected bool IsMediaElement => Definition.Tag is "audio" or "video";

    /// <summary>
    /// Gets whether video-only attributes should be exposed.
    /// </summary>
    protected bool IsVideo => Definition.Tag.Equals("video", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether responsive media-source attributes should be exposed.
    /// </summary>
    protected bool IsSource => Definition.Tag.Equals("source", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether timed-text track attributes should be exposed.
    /// </summary>
    protected bool IsTrack => Definition.Tag.Equals("track", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether button behavior attributes should be exposed.
    /// </summary>
    protected bool IsButton => Definition.Tag.Equals("button", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether table-cell span attributes should be exposed.
    /// </summary>
    protected bool IsTableCell => Definition.Tag is "th" or "td";

    /// <summary>
    /// Gets whether the table column <c>span</c> attribute should be exposed.
    /// </summary>
    protected bool IsTableColumnDefinition => Definition.Tag is "col" or "colgroup";

    /// <summary>
    /// Gets whether header-cell scope can be edited.
    /// </summary>
    protected bool IsHeaderCell => Definition.Tag.Equals("th", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether form submission attributes should be exposed.
    /// </summary>
    protected bool IsForm => Definition.Tag.Equals("form", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether fieldset name and disabled state should be exposed.
    /// </summary>
    protected bool IsFieldset => Definition.Tag.Equals("fieldset", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether label-to-control association should be exposed.
    /// </summary>
    protected bool IsLabel => Definition.Tag.Equals("label", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether input-control attributes should be exposed.
    /// </summary>
    protected bool IsInput => Definition.Tag.Equals("input", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether textarea attributes and literal text should be exposed.
    /// </summary>
    protected bool IsTextArea => Definition.Tag.Equals("textarea", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether select-control attributes should be exposed.
    /// </summary>
    protected bool IsSelect => Definition.Tag.Equals("select", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether option-group attributes should be exposed.
    /// </summary>
    protected bool IsOptionGroup => Definition.Tag.Equals("optgroup", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether option attributes and literal text should be exposed.
    /// </summary>
    protected bool IsOption => Definition.Tag.Equals("option", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether output association attributes should be exposed.
    /// </summary>
    protected bool IsOutput => Definition.Tag.Equals("output", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether the element definition permits a citation URL.
    /// </summary>
    protected bool HasCitationUrl => Definition.AllowedAttributes.Contains("cite", StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether the element definition permits an <c>open</c> boolean attribute.
    /// </summary>
    protected bool HasOpenAttribute =>
        Definition.AllowedAttributes.Contains("open", StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the context-appropriate label for the <c>open</c> boolean attribute.
    /// </summary>
    protected string OpenAttributeLabel => Definition.Tag.Equals("dialog", StringComparison.OrdinalIgnoreCase)
        ? "Open by default"
        : "Expanded by default";

    /// <summary>
    /// Gets whether the element definition permits a machine-readable date or time.
    /// </summary>
    protected bool HasDateTimeAttribute => Definition.AllowedAttributes.Contains("datetime", StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether a machine-readable data value should be exposed.
    /// </summary>
    protected bool IsData => Definition.Tag.Equals("data", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether progress value and maximum attributes should be exposed.
    /// </summary>
    protected bool IsProgress => Definition.Tag.Equals("progress", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether meter range and optimum attributes should be exposed.
    /// </summary>
    protected bool IsMeter => Definition.Tag.Equals("meter", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether guided list-item actions apply to the current selection.
    /// </summary>
    protected bool SupportsListActions => Definition.Tag is "ul" or "ol" or "li";

    /// <summary>
    /// Gets whether guided row or column actions apply to the current table context.
    /// </summary>
    protected bool SupportsTableActions => Definition.Tag is "table" or "caption" or "colgroup" or "col"
        or "thead" or "tbody" or "tfoot" or "tr" or "th" or "td";

    /// <summary>
    /// Gets whether a guided media-source action applies to the current selection.
    /// </summary>
    protected bool SupportsMediaSourceAction => Definition.Tag is "picture" or "audio" or "video" or "source";

    /// <summary>
    /// Gets whether a guided timed-track action applies to the current selection.
    /// </summary>
    protected bool SupportsMediaTrackAction => Definition.Tag is "audio" or "video" or "track";

    /// <summary>
    /// Gets whether a guided form-control action applies to the current form context.
    /// </summary>
    protected bool SupportsFormActions => Definition.Tag is "form" or "fieldset" or "legend" or "label"
        or "input" or "textarea" or "select" or "optgroup" or "option" or "button" or "output";

    /// <summary>
    /// Gets whether a guided option action applies to the current selection.
    /// </summary>
    protected bool SupportsSelectOptionAction => Definition.Tag is "select" or "optgroup" or "datalist" or "option";

    /// <summary>
    /// Gets whether a guided option-group action applies to the current selection.
    /// </summary>
    protected bool SupportsOptionGroupAction => Definition.Tag is "select" or "optgroup";

    /// <summary>
    /// Gets whether at least one guided collection action applies to the current selection.
    /// </summary>
    protected bool SupportsCollectionActions => SupportsListActions
        || SupportsTableActions
        || SupportsMediaSourceAction
        || SupportsMediaTrackAction
        || SupportsFormActions
        || SupportsSelectOptionAction
        || SupportsOptionGroupAction;

    /// <summary>
    /// Gets whether the current form display mode supports flex-direction controls.
    /// </summary>
    protected bool IsFlexDisplay => Form.Display is CssDisplay.Flex or CssDisplay.InlineFlex;

    /// <summary>
    /// Gets whether the current form display mode supports grid-column controls.
    /// </summary>
    protected bool IsGridDisplay => Form.Display is CssDisplay.Grid or CssDisplay.InlineGrid;

    /// <summary>
    /// Gets whether the current phrasing-content node can round-trip through the constrained
    /// rich-text converter.
    /// </summary>
    protected bool SupportsRichText => Definition.ChildModel is HtmlChildModel.Phrasing
        && _richTextConverter.CanEdit(Node);

    /// <summary>
    /// Reloads form state when the selected node instance changes.
    /// </summary>
    /// <remarks>
    /// Owner-provided validation errors do not automatically reset in-progress form edits for the
    /// same node instance.
    /// </remarks>
    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(Node, _sourceNode))
        {
            LoadFromNode();
        }
    }

    /// <summary>
    /// Determines whether the element definition allows a named style capability.
    /// </summary>
    /// <param name="capability">The case-insensitive capability token to test.</param>
    /// <returns><see langword="true"/> when the inspector may expose that style group.</returns>
    protected bool HasCapability(string capability) =>
        Definition.StyleCapabilities.Contains(capability, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Converts a PascalCase enum name into a label suitable for inspector controls.
    /// </summary>
    /// <typeparam name="T">The enum type being labeled.</typeparam>
    /// <param name="value">The enum value to label.</param>
    /// <returns>The enum name with word boundaries separated by spaces.</returns>
    protected static string Friendly<T>(T value) where T : struct, Enum =>
        Regex.Replace(value.ToString(), "(?<=[a-z0-9])([A-Z])", " $1");

    /// <summary>
    /// Requests that the owning editor clear the selected node.
    /// </summary>
    /// <returns>A task that completes when the callback has finished.</returns>
    protected Task ClearSelectionAsync() => SelectionCleared.InvokeAsync();

    /// <summary>
    /// Requests the constrained rich-text workflow for the selected node.
    /// </summary>
    /// <returns>A task that completes when the callback has finished.</returns>
    protected Task RequestRichTextAsync() => RichTextRequested.InvokeAsync();

    /// <summary>
    /// Requests a guided mutation for the selected list, table, media, or form context.
    /// </summary>
    /// <param name="action">The collection action to request.</param>
    /// <returns>A task that completes when the callback has finished.</returns>
    protected Task RequestCollectionActionAsync(HtmlCollectionActionKind action) =>
        CollectionActionRequested.InvokeAsync(action);

    /// <summary>
    /// Requests duplication of the selected subtree.
    /// </summary>
    /// <returns>A task that completes when the callback has finished.</returns>
    protected Task RequestDuplicateAsync() => DuplicateRequested.InvokeAsync();

    /// <summary>
    /// Requests removal of the selected subtree.
    /// </summary>
    /// <returns>A task that completes when the callback has finished.</returns>
    protected Task RequestRemoveAsync() => RemoveRequested.InvokeAsync();

    /// <summary>
    /// Requests media selection for a specific editable media property.
    /// </summary>
    /// <param name="target">The node property that will receive the selected media URL.</param>
    /// <returns>A task that completes when the callback has finished.</returns>
    protected Task RequestMediaAsync(HtmlMediaTargetKind target) => MediaRequested.InvokeAsync(target);

    /// <summary>
    /// Builds a detached property model from the form and forwards it to the owning editor.
    /// </summary>
    /// <returns>A task that completes when the callback has finished.</returns>
    protected Task ApplyAsync() => PropertiesChanged.InvokeAsync(BuildProperties());

    /// <summary>
    /// Discards in-progress form edits by reloading values from <see cref="Node"/>.
    /// </summary>
    protected void Reset() => LoadFromNode();

    /// <summary>
    /// Projects the selected node's allowed attributes, literal text, and structured styles into
    /// editable form state.
    /// </summary>
    /// <remarks>
    /// Boolean HTML attributes are represented by key presence. Missing CSS values remain
    /// nullable, while user-facing defaults are supplied only where the form requires them.
    /// </remarks>
    private void LoadFromNode()
    {
        _sourceNode = Node;
        var style = Node.Style;
        Form = new InspectorForm
        {
            Id = Attribute("id"),
            Title = Attribute("title"),
            Href = Attribute("href"),
            Target = Attribute("target"),
            Rel = Attribute("rel"),
            Source = Attribute("src"),
            SourceSet = Attribute("srcset"),
            Sizes = Attribute("sizes"),
            MediaQuery = Attribute("media"),
            MediaType = Attribute("type"),
            Poster = Attribute("poster"),
            Preload = Attribute("preload"),
            AutoPlay = Node.Attributes.ContainsKey("autoplay"),
            Controls = Node.Attributes.ContainsKey("controls"),
            Loop = Node.Attributes.ContainsKey("loop"),
            Muted = Node.Attributes.ContainsKey("muted"),
            PlaysInline = Node.Attributes.ContainsKey("playsinline"),
            TrackKind = Attribute("kind"),
            SourceLanguage = Attribute("srclang"),
            TrackLabel = Attribute("label"),
            DefaultTrack = Node.Attributes.ContainsKey("default"),
            AlternativeText = Attribute("alt"),
            Width = Attribute("width"),
            Height = Attribute("height"),
            Loading = Attribute("loading"),
            ButtonType = Attribute("type") ?? "button",
            Disabled = Node.Attributes.ContainsKey("disabled"),
            ColumnSpan = Attribute(IsTableColumnDefinition ? "span" : "colspan"),
            RowSpan = Attribute("rowspan"),
            Scope = Attribute("scope"),
            Action = Attribute("action"),
            Method = Attribute("method") ?? "get",
            AutoComplete = Attribute("autocomplete"),
            LabelFor = Attribute("for"),
            InputType = Attribute("type") ?? "text",
            ControlName = Attribute("name"),
            ControlValue = Attribute("value"),
            Placeholder = Attribute("placeholder"),
            Required = Node.Attributes.ContainsKey("required"),
            ReadOnly = Node.Attributes.ContainsKey("readonly"),
            Checked = Node.Attributes.ContainsKey("checked"),
            Multiple = Node.Attributes.ContainsKey("multiple"),
            Selected = Node.Attributes.ContainsKey("selected"),
            Minimum = Attribute("min"),
            Maximum = Attribute("max"),
            Step = Attribute("step"),
            Rows = Attribute("rows"),
            Columns = Attribute("cols"),
            MaximumLength = Attribute("maxlength"),
            OptionLabel = Attribute("label"),
            CitationUrl = Attribute("cite"),
            Open = Node.Attributes.ContainsKey("open"),
            DateTimeValue = Attribute("datetime"),
            MachineValue = Attribute("value"),
            NumericValue = Attribute("value"),
            NumericMinimum = Attribute("min"),
            NumericMaximum = Attribute("max"),
            NumericLow = Attribute("low"),
            NumericHigh = Attribute("high"),
            NumericOptimum = Attribute("optimum"),
            LiteralText = IsTextArea || IsOption
                ? string.Concat(Node.Children.Where(child => child.Kind is HtmlNodeKind.Text).Select(child => child.Text))
                : null,
            Display = style?.Display,
            FlexDirection = style?.FlexDirection,
            GridColumns = style?.GridColumns,
            StackOnSmallScreens = style?.StackOnSmallScreens ?? false,
            Gap = LengthField.From(style?.Gap),
            AlignItems = style?.AlignItems,
            JustifyContent = style?.JustifyContent,
            Padding = SpacingField.From(style?.Padding),
            Margin = SpacingField.From(style?.Margin),
            MinimumHeight = LengthField.From(style?.MinimumHeight),
            BackgroundColor = ColorValue(style?.Surface?.BackgroundColor),
            BackgroundImageUrl = style?.Surface?.BackgroundImageUrl,
            OverlayColor = ColorValue(style?.Surface?.OverlayColor),
            OverlayOpacity = style?.Surface?.OverlayOpacity ?? 0.4m,
            BackgroundFit = style?.Surface?.BackgroundFit,
            BackgroundPosition = style?.Surface?.BackgroundPosition,
            BackgroundRepeat = style?.Surface?.BackgroundRepeat,
            BorderRadius = LengthField.From(style?.Surface?.BorderRadius),
            TextColor = ColorValue(style?.Typography?.Color),
            FontSize = LengthField.From(style?.Typography?.FontSize),
            FontWeight = style?.Typography?.FontWeight,
            LineHeight = style?.Typography?.LineHeight,
            LetterSpacing = LengthField.From(style?.Typography?.LetterSpacing),
            TextAlignment = style?.Typography?.Alignment,
            UseTextGradient = style?.Typography?.Gradient is not null,
            GradientStartColor = ColorValue(style?.Typography?.Gradient?.StartColor) ?? "#2563eb",
            GradientEndColor = ColorValue(style?.Typography?.Gradient?.EndColor) ?? "#9333ea",
            GradientAngleDegrees = style?.Typography?.Gradient?.AngleDegrees ?? 90m
        };
    }

    /// <summary>
    /// Creates a detached update while preserving attributes and style groups that this inspector
    /// is not authorized to edit.
    /// </summary>
    /// <returns>
    /// A property model containing normalized form values. Empty editable style groups are
    /// collapsed to <see langword="null"/>.
    /// </returns>
    /// <remarks>
    /// Textarea and option text is represented as literal replacement content so the owning tree
    /// editor can replace their children atomically.
    /// </remarks>
    private HtmlNodeProperties BuildProperties()
    {
        var properties = HtmlNodeProperties.From(Node);
        SetOrRemove(properties.Attributes, "id", Form.Id);
        SetOrRemove(properties.Attributes, "title", Form.Title);

        if (IsLink)
        {
            SetOrRemove(properties.Attributes, "href", Form.Href);
            SetOrRemove(properties.Attributes, "target", Form.Target);
            SetOrRemove(properties.Attributes, "rel", Form.Rel);
        }

        if (IsImage)
        {
            SetOrRemove(properties.Attributes, "src", Form.Source);
            SetOrRemove(properties.Attributes, "alt", Form.AlternativeText, preserveEmpty: true);
            SetOrRemove(properties.Attributes, "width", Form.Width);
            SetOrRemove(properties.Attributes, "height", Form.Height);
            SetOrRemove(properties.Attributes, "loading", Form.Loading);
        }

        if (IsMediaElement)
        {
            SetOrRemove(properties.Attributes, "src", Form.Source);
            SetOrRemove(properties.Attributes, "preload", Form.Preload);
            SetBoolean(properties.Attributes, "autoplay", Form.AutoPlay);
            SetBoolean(properties.Attributes, "controls", Form.Controls);
            SetBoolean(properties.Attributes, "loop", Form.Loop);
            SetBoolean(properties.Attributes, "muted", Form.Muted);
            if (IsVideo)
            {
                SetOrRemove(properties.Attributes, "poster", Form.Poster);
                SetOrRemove(properties.Attributes, "width", Form.Width);
                SetOrRemove(properties.Attributes, "height", Form.Height);
                SetBoolean(properties.Attributes, "playsinline", Form.PlaysInline);
            }
        }

        if (IsSource)
        {
            SetOrRemove(properties.Attributes, "src", Form.Source);
            SetOrRemove(properties.Attributes, "srcset", Form.SourceSet);
            SetOrRemove(properties.Attributes, "sizes", Form.Sizes);
            SetOrRemove(properties.Attributes, "media", Form.MediaQuery);
            SetOrRemove(properties.Attributes, "type", Form.MediaType);
        }

        if (IsTrack)
        {
            SetOrRemove(properties.Attributes, "kind", Form.TrackKind);
            SetOrRemove(properties.Attributes, "src", Form.Source);
            SetOrRemove(properties.Attributes, "srclang", Form.SourceLanguage);
            SetOrRemove(properties.Attributes, "label", Form.TrackLabel);
            SetBoolean(properties.Attributes, "default", Form.DefaultTrack);
        }

        if (IsButton)
        {
            SetOrRemove(properties.Attributes, "type", Form.ButtonType);
            if (Form.Disabled) properties.Attributes["disabled"] = string.Empty;
            else properties.Attributes.Remove("disabled");
        }

        if (IsTableCell)
        {
            SetOrRemove(properties.Attributes, "colspan", Form.ColumnSpan);
            SetOrRemove(properties.Attributes, "rowspan", Form.RowSpan);
            if (IsHeaderCell)
            {
                SetOrRemove(properties.Attributes, "scope", Form.Scope);
            }
        }

        if (IsTableColumnDefinition)
        {
            SetOrRemove(properties.Attributes, "span", Form.ColumnSpan);
        }

        if (IsForm)
        {
            SetOrRemove(properties.Attributes, "action", Form.Action);
            SetOrRemove(properties.Attributes, "method", Form.Method);
            SetOrRemove(properties.Attributes, "target", Form.Target);
            SetOrRemove(properties.Attributes, "autocomplete", Form.AutoComplete);
        }

        if (IsFieldset)
        {
            SetOrRemove(properties.Attributes, "name", Form.ControlName);
            SetBoolean(properties.Attributes, "disabled", Form.Disabled);
        }

        if (IsLabel)
        {
            SetOrRemove(properties.Attributes, "for", Form.LabelFor);
        }

        if (IsInput)
        {
            SetOrRemove(properties.Attributes, "type", Form.InputType);
            SetOrRemove(properties.Attributes, "name", Form.ControlName);
            SetOrRemove(properties.Attributes, "value", Form.ControlValue, preserveEmpty: true);
            SetOrRemove(properties.Attributes, "placeholder", Form.Placeholder);
            SetOrRemove(properties.Attributes, "min", Form.Minimum);
            SetOrRemove(properties.Attributes, "max", Form.Maximum);
            SetOrRemove(properties.Attributes, "step", Form.Step);
            SetOrRemove(properties.Attributes, "autocomplete", Form.AutoComplete);
            SetBoolean(properties.Attributes, "required", Form.Required);
            SetBoolean(properties.Attributes, "disabled", Form.Disabled);
            SetBoolean(properties.Attributes, "readonly", Form.ReadOnly);
            SetBoolean(properties.Attributes, "checked", Form.Checked);
        }

        if (IsTextArea)
        {
            SetOrRemove(properties.Attributes, "name", Form.ControlName);
            SetOrRemove(properties.Attributes, "placeholder", Form.Placeholder);
            SetOrRemove(properties.Attributes, "rows", Form.Rows);
            SetOrRemove(properties.Attributes, "cols", Form.Columns);
            SetOrRemove(properties.Attributes, "maxlength", Form.MaximumLength);
            SetOrRemove(properties.Attributes, "autocomplete", Form.AutoComplete);
            SetBoolean(properties.Attributes, "required", Form.Required);
            SetBoolean(properties.Attributes, "disabled", Form.Disabled);
            SetBoolean(properties.Attributes, "readonly", Form.ReadOnly);
            properties.ReplaceChildrenWithLiteralText = true;
            properties.LiteralText = Form.LiteralText;
        }

        if (IsSelect)
        {
            SetOrRemove(properties.Attributes, "name", Form.ControlName);
            SetOrRemove(properties.Attributes, "autocomplete", Form.AutoComplete);
            SetBoolean(properties.Attributes, "multiple", Form.Multiple);
            SetBoolean(properties.Attributes, "required", Form.Required);
            SetBoolean(properties.Attributes, "disabled", Form.Disabled);
        }

        if (IsOptionGroup)
        {
            SetOrRemove(properties.Attributes, "label", Form.OptionLabel);
            SetBoolean(properties.Attributes, "disabled", Form.Disabled);
        }

        if (IsOption)
        {
            SetOrRemove(properties.Attributes, "value", Form.ControlValue, preserveEmpty: true);
            SetOrRemove(properties.Attributes, "label", Form.OptionLabel);
            SetBoolean(properties.Attributes, "selected", Form.Selected);
            SetBoolean(properties.Attributes, "disabled", Form.Disabled);
            properties.ReplaceChildrenWithLiteralText = true;
            properties.LiteralText = Form.LiteralText;
        }

        if (IsOutput)
        {
            SetOrRemove(properties.Attributes, "name", Form.ControlName);
            SetOrRemove(properties.Attributes, "for", Form.LabelFor);
        }

        if (HasCitationUrl)
        {
            SetOrRemove(properties.Attributes, "cite", Form.CitationUrl);
        }

        if (HasOpenAttribute)
        {
            SetBoolean(properties.Attributes, "open", Form.Open);
        }

        if (HasDateTimeAttribute)
        {
            SetOrRemove(properties.Attributes, "datetime", Form.DateTimeValue);
        }

        if (IsData)
        {
            SetOrRemove(properties.Attributes, "value", Form.MachineValue);
        }

        if (IsProgress)
        {
            SetOrRemove(properties.Attributes, "value", Form.NumericValue);
            SetOrRemove(properties.Attributes, "max", Form.NumericMaximum);
        }

        if (IsMeter)
        {
            SetOrRemove(properties.Attributes, "value", Form.NumericValue);
            SetOrRemove(properties.Attributes, "min", Form.NumericMinimum);
            SetOrRemove(properties.Attributes, "max", Form.NumericMaximum);
            SetOrRemove(properties.Attributes, "low", Form.NumericLow);
            SetOrRemove(properties.Attributes, "high", Form.NumericHigh);
            SetOrRemove(properties.Attributes, "optimum", Form.NumericOptimum);
        }

        var style = properties.Style ?? new HtmlStyle();
        if (HasCapability("layout"))
        {
            style.Display = Form.Display;
            style.FlexDirection = IsFlexDisplay ? Form.FlexDirection : null;
            style.GridColumns = IsGridDisplay ? Form.GridColumns : null;
            style.StackOnSmallScreens = (IsFlexDisplay || IsGridDisplay) && Form.StackOnSmallScreens;
            style.Gap = Form.Gap.ToModel();
            style.AlignItems = Form.AlignItems;
            style.JustifyContent = Form.JustifyContent;
            style.MinimumHeight = Form.MinimumHeight.ToModel();
        }

        if (HasCapability("spacing"))
        {
            style.Padding = Form.Padding.ToModel();
            style.Margin = Form.Margin.ToModel();
        }

        if (HasCapability("surface"))
        {
            style.Surface = BuildSurface();
        }

        if (HasCapability("typography"))
        {
            var typography = style.Typography ?? new CssTypographyStyle();
            typography.Color = Form.UseTextGradient ? null : ParseColor(Form.TextColor);
            typography.FontSize = Form.FontSize.ToModel();
            typography.FontWeight = Form.FontWeight;
            typography.LineHeight = Form.LineHeight;
            typography.LetterSpacing = Form.LetterSpacing.ToModel();
            typography.Alignment = Form.TextAlignment;
            typography.Gradient = Form.UseTextGradient
                ? new CssTextGradient
                {
                    StartColor = ParseColor(Form.GradientStartColor) ?? CssColor.Hex("#2563eb"),
                    EndColor = ParseColor(Form.GradientEndColor) ?? CssColor.Hex("#9333ea"),
                    AngleDegrees = Form.GradientAngleDegrees
                }
                : null;
            style.Typography = IsEmpty(typography) ? null : typography;
        }

        properties.Style = IsEmpty(style) ? null : style;
        return properties;
    }

    /// <summary>
    /// Builds the editable surface style while retaining any unedited surface data from the
    /// selected node.
    /// </summary>
    /// <returns>
    /// The normalized surface style, or <see langword="null"/> when every surface field is empty.
    /// </returns>
    /// <remarks>
    /// Overlay and background-placement values are cleared when no background image is present.
    /// </remarks>
    private CssSurfaceStyle? BuildSurface()
    {
        var surface = HtmlTreeOperations.CloneStyle(Node.Style)?.Surface ?? new CssSurfaceStyle();
        surface.BackgroundColor = ParseColor(Form.BackgroundColor);
        surface.BackgroundImageUrl = NullIfWhiteSpace(Form.BackgroundImageUrl);
        surface.OverlayColor = string.IsNullOrWhiteSpace(surface.BackgroundImageUrl)
            ? null
            : ParseColor(Form.OverlayColor);
        surface.OverlayOpacity = surface.OverlayColor is null ? null : Form.OverlayOpacity;
        surface.BackgroundFit = string.IsNullOrWhiteSpace(surface.BackgroundImageUrl) ? null : Form.BackgroundFit;
        surface.BackgroundPosition = string.IsNullOrWhiteSpace(surface.BackgroundImageUrl) ? null : Form.BackgroundPosition;
        surface.BackgroundRepeat = string.IsNullOrWhiteSpace(surface.BackgroundImageUrl) ? null : Form.BackgroundRepeat;
        surface.BorderRadius = Form.BorderRadius.ToModel();
        return IsEmpty(surface) ? null : surface;
    }

    /// <summary>
    /// Reads an attribute from the selected node without introducing a default.
    /// </summary>
    /// <param name="name">The normalized attribute name.</param>
    /// <returns>The stored value, or <see langword="null"/> when the key is absent.</returns>
    private string? Attribute(string name) => Node.Attributes.TryGetValue(name, out var value) ? value : null;

    /// <summary>
    /// Extracts the serialized token from a structured CSS color.
    /// </summary>
    /// <param name="color">The optional color model.</param>
    /// <returns>The stored color token, or <see langword="null"/>.</returns>
    private static string? ColorValue(CssColor? color) => color?.Value;

    /// <summary>
    /// Parses a non-empty inspector color as a hexadecimal color or named design token.
    /// </summary>
    /// <param name="value">The user-entered color value.</param>
    /// <returns>A structured color, or <see langword="null"/> for blank input.</returns>
    /// <remarks>
    /// Syntax validation is deferred to the owning page validation pipeline; a leading
    /// <c>#</c> selects hexadecimal representation and all other values become tokens.
    /// </remarks>
    private static CssColor? ParseColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.StartsWith('#')
            ? CssColor.Hex(normalized)
            : CssColor.Token(normalized);
    }

    /// <summary>
    /// Trims a non-blank value while representing blank input as absent.
    /// </summary>
    /// <param name="value">The optional form value.</param>
    /// <returns>The trimmed value, or <see langword="null"/> when blank.</returns>
    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Sets a trimmed HTML attribute or removes it when the form value represents absence.
    /// </summary>
    /// <param name="attributes">The detached attribute dictionary to update.</param>
    /// <param name="name">The normalized attribute name.</param>
    /// <param name="value">The optional form value.</param>
    /// <param name="preserveEmpty">
    /// Whether an empty string is a meaningful value that should remain present.
    /// </param>
    private static void SetOrRemove(
        IDictionary<string, string> attributes,
        string name,
        string? value,
        bool preserveEmpty = false)
    {
        if (value is null || (!preserveEmpty && string.IsNullOrWhiteSpace(value)))
        {
            attributes.Remove(name);
            return;
        }

        attributes[name] = value.Trim();
    }

    /// <summary>
    /// Encodes an HTML boolean attribute by key presence rather than textual Boolean values.
    /// </summary>
    /// <param name="attributes">The detached attribute dictionary to update.</param>
    /// <param name="name">The normalized boolean attribute name.</param>
    /// <param name="enabled">Whether the attribute should be present.</param>
    private static void SetBoolean(IDictionary<string, string> attributes, string name, bool enabled)
    {
        if (enabled) attributes[name] = string.Empty;
        else attributes.Remove(name);
    }

    /// <summary>
    /// Determines whether a structured style contains no serializable values.
    /// </summary>
    /// <param name="style">The style to inspect.</param>
    /// <returns><see langword="true"/> when the style can be collapsed to absence.</returns>
    private static bool IsEmpty(HtmlStyle style) =>
        style.Display is null && style.FlexDirection is null && style.GridColumns is null
        && !style.StackOnSmallScreens && style.Gap is null && style.AlignItems is null
        && style.JustifyContent is null && style.Padding is null && style.Margin is null
        && style.MinimumHeight is null && style.Surface is null && style.Typography is null;

    /// <summary>
    /// Determines whether a surface style contains no serializable values.
    /// </summary>
    /// <param name="surface">The surface style to inspect.</param>
    /// <returns><see langword="true"/> when the surface can be collapsed to absence.</returns>
    private static bool IsEmpty(CssSurfaceStyle surface) =>
        surface.BackgroundColor is null && string.IsNullOrWhiteSpace(surface.BackgroundImageUrl)
        && surface.OverlayColor is null && surface.OverlayOpacity is null
        && surface.BackgroundFit is null && surface.BackgroundPosition is null
        && surface.BackgroundRepeat is null && surface.BorderRadius is null;

    /// <summary>
    /// Determines whether a typography style contains no serializable values.
    /// </summary>
    /// <param name="typography">The typography style to inspect.</param>
    /// <returns><see langword="true"/> when the typography can be collapsed to absence.</returns>
    private static bool IsEmpty(CssTypographyStyle typography) =>
        typography.Color is null && typography.FontSize is null && typography.FontWeight is null
        && typography.LineHeight is null && typography.LetterSpacing is null
        && typography.Alignment is null && typography.Gradient is null;

    /// <summary>
    /// Holds the editable projection of attributes and structured styles for the selected node.
    /// </summary>
    /// <remarks>
    /// This mutable model is component state only. It is never persisted or applied to the source
    /// node until <see cref="ApplyAsync"/> emits a detached property update.
    /// </remarks>
    protected sealed class InspectorForm
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Href { get; set; }
        public string? Target { get; set; }
        public string? Rel { get; set; }
        public string? Source { get; set; }
        public string? SourceSet { get; set; }
        public string? Sizes { get; set; }
        public string? MediaQuery { get; set; }
        public string? MediaType { get; set; }
        public string? Poster { get; set; }
        public string? Preload { get; set; }
        public bool AutoPlay { get; set; }
        public bool Controls { get; set; }
        public bool Loop { get; set; }
        public bool Muted { get; set; }
        public bool PlaysInline { get; set; }
        public string? TrackKind { get; set; }
        public string? SourceLanguage { get; set; }
        public string? TrackLabel { get; set; }
        public bool DefaultTrack { get; set; }
        public string? AlternativeText { get; set; }
        public string? Width { get; set; }
        public string? Height { get; set; }
        public string? Loading { get; set; }
        public string? ButtonType { get; set; }
        public bool Disabled { get; set; }
        public string? ColumnSpan { get; set; }
        public string? RowSpan { get; set; }
        public string? Scope { get; set; }
        public string? Action { get; set; }
        public string? Method { get; set; }
        public string? AutoComplete { get; set; }
        public string? LabelFor { get; set; }
        public string? InputType { get; set; }
        public string? ControlName { get; set; }
        public string? ControlValue { get; set; }
        public string? Placeholder { get; set; }
        public bool Required { get; set; }
        public bool ReadOnly { get; set; }
        public bool Checked { get; set; }
        public bool Multiple { get; set; }
        public bool Selected { get; set; }
        public string? Minimum { get; set; }
        public string? Maximum { get; set; }
        public string? Step { get; set; }
        public string? Rows { get; set; }
        public string? Columns { get; set; }
        public string? MaximumLength { get; set; }
        public string? OptionLabel { get; set; }
        public string? CitationUrl { get; set; }
        public bool Open { get; set; }
        public string? DateTimeValue { get; set; }
        public string? MachineValue { get; set; }
        public string? NumericValue { get; set; }
        public string? NumericMinimum { get; set; }
        public string? NumericMaximum { get; set; }
        public string? NumericLow { get; set; }
        public string? NumericHigh { get; set; }
        public string? NumericOptimum { get; set; }
        public string? LiteralText { get; set; }
        public CssDisplay? Display { get; set; }
        public CssFlexDirection? FlexDirection { get; set; }
        public int? GridColumns { get; set; }
        public bool StackOnSmallScreens { get; set; }
        public LengthField Gap { get; set; } = new();
        public CssAlignment? AlignItems { get; set; }
        public CssJustification? JustifyContent { get; set; }
        public SpacingField Padding { get; set; } = new();
        public SpacingField Margin { get; set; } = new();
        public LengthField MinimumHeight { get; set; } = new();
        public string? BackgroundColor { get; set; }
        public string? BackgroundImageUrl { get; set; }
        public string? OverlayColor { get; set; }
        public decimal OverlayOpacity { get; set; } = 0.4m;
        public CssBackgroundFit? BackgroundFit { get; set; }
        public CssBackgroundPosition? BackgroundPosition { get; set; }
        public CssBackgroundRepeat? BackgroundRepeat { get; set; }
        public LengthField BorderRadius { get; set; } = new();
        public string? TextColor { get; set; }
        public LengthField FontSize { get; set; } = new();
        public int? FontWeight { get; set; }
        public decimal? LineHeight { get; set; }
        public LengthField LetterSpacing { get; set; } = new();
        public CssTextAlignment? TextAlignment { get; set; }
        public bool UseTextGradient { get; set; }
        public string? GradientStartColor { get; set; } = "#2563eb";
        public string? GradientEndColor { get; set; } = "#9333ea";
        public decimal GradientAngleDegrees { get; set; } = 90m;
    }

    /// <summary>
    /// Represents an optional CSS length as a numeric value and unit for inspector binding.
    /// </summary>
    /// <remarks>
    /// A missing <see cref="Value"/> represents no CSS declaration; the unit remains available as
    /// the default for the next value entered by the user.
    /// </remarks>
    public sealed class LengthField
    {
        /// <summary>
        /// Gets or sets the optional numeric component of the CSS length.
        /// </summary>
        public decimal? Value { get; set; }

        /// <summary>
        /// Gets or sets the allowlisted CSS unit paired with <see cref="Value"/>.
        /// </summary>
        public CssLengthUnit Unit { get; set; } = CssLengthUnit.Rem;

        /// <summary>
        /// Converts the bound field to its serializable CSS model.
        /// </summary>
        /// <returns>
        /// A CSS length when <see cref="Value"/> is present; otherwise,
        /// <see langword="null"/>.
        /// </returns>
        public CssLength? ToModel() => Value is { } value ? new CssLength { Value = value, Unit = Unit } : null;

        /// <summary>
        /// Creates inspector state from an optional CSS length.
        /// </summary>
        /// <param name="value">The persisted CSS length, or <see langword="null"/>.</param>
        /// <returns>
        /// A new field whose unit defaults to <see cref="CssLengthUnit.Rem"/> when no model is
        /// present.
        /// </returns>
        public static LengthField From(CssLength? value) => new()
        {
            Value = value?.Value,
            Unit = value?.Unit ?? CssLengthUnit.Rem
        };
    }

    /// <summary>
    /// Holds logical block and inline spacing fields for direction-aware CSS serialization.
    /// </summary>
    protected sealed class SpacingField
    {
        /// <summary>
        /// Gets or sets spacing before the content in the block flow direction.
        /// </summary>
        public LengthField BlockStart { get; set; } = new();

        /// <summary>
        /// Gets or sets spacing after the content in the inline flow direction.
        /// </summary>
        public LengthField InlineEnd { get; set; } = new();

        /// <summary>
        /// Gets or sets spacing after the content in the block flow direction.
        /// </summary>
        public LengthField BlockEnd { get; set; } = new();

        /// <summary>
        /// Gets or sets spacing before the content in the inline flow direction.
        /// </summary>
        public LengthField InlineStart { get; set; } = new();

        /// <summary>
        /// Converts the four optional logical fields to their serializable spacing model.
        /// </summary>
        /// <returns>
        /// A logical spacing model when any side has a value; otherwise,
        /// <see langword="null"/>.
        /// </returns>
        public CssLogicalSpacing? ToModel()
        {
            var spacing = new CssLogicalSpacing
            {
                BlockStart = BlockStart.ToModel(),
                InlineEnd = InlineEnd.ToModel(),
                BlockEnd = BlockEnd.ToModel(),
                InlineStart = InlineStart.ToModel()
            };
            return spacing.BlockStart is null && spacing.InlineEnd is null
                && spacing.BlockEnd is null && spacing.InlineStart is null ? null : spacing;
        }

        /// <summary>
        /// Creates inspector state from optional logical CSS spacing.
        /// </summary>
        /// <param name="value">The persisted spacing model, or <see langword="null"/>.</param>
        /// <returns>A new mutable field with one optional length per logical side.</returns>
        public static SpacingField From(CssLogicalSpacing? value) => new()
        {
            BlockStart = LengthField.From(value?.BlockStart),
            InlineEnd = LengthField.From(value?.InlineEnd),
            BlockEnd = LengthField.From(value?.BlockEnd),
            InlineStart = LengthField.From(value?.InlineStart)
        };
    }
}
