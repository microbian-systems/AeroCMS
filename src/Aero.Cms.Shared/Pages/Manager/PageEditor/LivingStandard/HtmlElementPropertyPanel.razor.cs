using System.Text.RegularExpressions;
using Aero.Cms.Html;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

public partial class HtmlElementPropertyPanel
{
    protected static readonly string[] InputTypes =
    [
        "text", "email", "tel", "url", "number", "password", "checkbox", "radio",
        "date", "time", "datetime-local", "month", "week", "color", "range", "hidden"
    ];

    private readonly TiptapInlineContentConverter _richTextConverter = new();
    private HtmlNode? _sourceNode;

    [Parameter, EditorRequired]
    public HtmlNode Node { get; set; } = null!;

    [Parameter, EditorRequired]
    public HtmlElementDefinition Definition { get; set; } = null!;

    [Parameter]
    public string? ErrorMessage { get; set; }

    [Parameter]
    public EventCallback<HtmlNodeProperties> PropertiesChanged { get; set; }

    [Parameter]
    public EventCallback SelectionCleared { get; set; }

    [Parameter]
    public EventCallback RichTextRequested { get; set; }

    [Parameter]
    public EventCallback<HtmlCollectionActionKind> CollectionActionRequested { get; set; }

    [Parameter]
    public EventCallback DuplicateRequested { get; set; }

    [Parameter]
    public EventCallback RemoveRequested { get; set; }

    [Parameter]
    public EventCallback<HtmlMediaTargetKind> MediaRequested { get; set; }

    protected InspectorForm Form { get; private set; } = new();

    protected bool IsLink => Definition.Tag.Equals("a", StringComparison.OrdinalIgnoreCase);
    protected bool IsImage => Definition.Tag.Equals("img", StringComparison.OrdinalIgnoreCase);
    protected bool IsMediaElement => Definition.Tag is "audio" or "video";
    protected bool IsVideo => Definition.Tag.Equals("video", StringComparison.OrdinalIgnoreCase);
    protected bool IsSource => Definition.Tag.Equals("source", StringComparison.OrdinalIgnoreCase);
    protected bool IsTrack => Definition.Tag.Equals("track", StringComparison.OrdinalIgnoreCase);
    protected bool IsButton => Definition.Tag.Equals("button", StringComparison.OrdinalIgnoreCase);
    protected bool IsTableCell => Definition.Tag is "th" or "td";
    protected bool IsHeaderCell => Definition.Tag.Equals("th", StringComparison.OrdinalIgnoreCase);
    protected bool IsForm => Definition.Tag.Equals("form", StringComparison.OrdinalIgnoreCase);
    protected bool IsLabel => Definition.Tag.Equals("label", StringComparison.OrdinalIgnoreCase);
    protected bool IsInput => Definition.Tag.Equals("input", StringComparison.OrdinalIgnoreCase);
    protected bool IsTextArea => Definition.Tag.Equals("textarea", StringComparison.OrdinalIgnoreCase);
    protected bool IsSelect => Definition.Tag.Equals("select", StringComparison.OrdinalIgnoreCase);
    protected bool IsOption => Definition.Tag.Equals("option", StringComparison.OrdinalIgnoreCase);
    protected bool HasCitationUrl => Definition.AllowedAttributes.Contains("cite", StringComparer.OrdinalIgnoreCase);
    protected bool IsDetails => Definition.Tag.Equals("details", StringComparison.OrdinalIgnoreCase);
    protected bool HasDateTimeAttribute => Definition.AllowedAttributes.Contains("datetime", StringComparer.OrdinalIgnoreCase);
    protected bool IsData => Definition.Tag.Equals("data", StringComparison.OrdinalIgnoreCase);
    protected bool IsProgress => Definition.Tag.Equals("progress", StringComparison.OrdinalIgnoreCase);
    protected bool IsMeter => Definition.Tag.Equals("meter", StringComparison.OrdinalIgnoreCase);
    protected bool SupportsListActions => Definition.Tag is "ul" or "ol" or "li";
    protected bool SupportsTableActions => Definition.Tag is "table" or "thead" or "tbody" or "tr" or "th" or "td";
    protected bool SupportsMediaSourceAction => Definition.Tag is "picture" or "audio" or "video" or "source";
    protected bool SupportsMediaTrackAction => Definition.Tag is "audio" or "video" or "track";
    protected bool SupportsFormActions => Definition.Tag is "form" or "label" or "input" or "textarea" or "select" or "option" or "button";
    protected bool SupportsSelectOptionAction => Definition.Tag is "select" or "option";
    protected bool SupportsCollectionActions => SupportsListActions
        || SupportsTableActions
        || SupportsMediaSourceAction
        || SupportsMediaTrackAction
        || SupportsFormActions;
    protected bool IsFlexDisplay => Form.Display is CssDisplay.Flex or CssDisplay.InlineFlex;
    protected bool IsGridDisplay => Form.Display is CssDisplay.Grid or CssDisplay.InlineGrid;
    protected bool SupportsRichText => Definition.ChildModel is HtmlChildModel.Phrasing
        && _richTextConverter.CanEdit(Node);

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(Node, _sourceNode))
        {
            LoadFromNode();
        }
    }

    protected bool HasCapability(string capability) =>
        Definition.StyleCapabilities.Contains(capability, StringComparer.OrdinalIgnoreCase);

    protected static string Friendly<T>(T value) where T : struct, Enum =>
        Regex.Replace(value.ToString(), "(?<=[a-z0-9])([A-Z])", " $1");

    protected Task ClearSelectionAsync() => SelectionCleared.InvokeAsync();

    protected Task RequestRichTextAsync() => RichTextRequested.InvokeAsync();

    protected Task RequestCollectionActionAsync(HtmlCollectionActionKind action) =>
        CollectionActionRequested.InvokeAsync(action);

    protected Task RequestDuplicateAsync() => DuplicateRequested.InvokeAsync();

    protected Task RequestRemoveAsync() => RemoveRequested.InvokeAsync();

    protected Task RequestMediaAsync(HtmlMediaTargetKind target) => MediaRequested.InvokeAsync(target);

    protected Task ApplyAsync() => PropertiesChanged.InvokeAsync(BuildProperties());

    protected void Reset() => LoadFromNode();

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
            ColumnSpan = Attribute("colspan"),
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

        if (IsForm)
        {
            SetOrRemove(properties.Attributes, "action", Form.Action);
            SetOrRemove(properties.Attributes, "method", Form.Method);
            SetOrRemove(properties.Attributes, "target", Form.Target);
            SetOrRemove(properties.Attributes, "autocomplete", Form.AutoComplete);
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

        if (IsOption)
        {
            SetOrRemove(properties.Attributes, "value", Form.ControlValue, preserveEmpty: true);
            SetOrRemove(properties.Attributes, "label", Form.OptionLabel);
            SetBoolean(properties.Attributes, "selected", Form.Selected);
            SetBoolean(properties.Attributes, "disabled", Form.Disabled);
            properties.ReplaceChildrenWithLiteralText = true;
            properties.LiteralText = Form.LiteralText;
        }

        if (HasCitationUrl)
        {
            SetOrRemove(properties.Attributes, "cite", Form.CitationUrl);
        }

        if (IsDetails)
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
            typography.Color = ParseColor(Form.TextColor);
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

    private string? Attribute(string name) => Node.Attributes.TryGetValue(name, out var value) ? value : null;

    private static string? ColorValue(CssColor? color) => color?.Value;

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

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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

    private static void SetBoolean(IDictionary<string, string> attributes, string name, bool enabled)
    {
        if (enabled) attributes[name] = string.Empty;
        else attributes.Remove(name);
    }

    private static bool IsEmpty(HtmlStyle style) =>
        style.Display is null && style.FlexDirection is null && style.GridColumns is null
        && !style.StackOnSmallScreens && style.Gap is null && style.AlignItems is null
        && style.JustifyContent is null && style.Padding is null && style.Margin is null
        && style.MinimumHeight is null && style.Surface is null && style.Typography is null;

    private static bool IsEmpty(CssSurfaceStyle surface) =>
        surface.BackgroundColor is null && string.IsNullOrWhiteSpace(surface.BackgroundImageUrl)
        && surface.OverlayColor is null && surface.OverlayOpacity is null
        && surface.BackgroundFit is null && surface.BackgroundPosition is null
        && surface.BackgroundRepeat is null && surface.BorderRadius is null;

    private static bool IsEmpty(CssTypographyStyle typography) =>
        typography.Color is null && typography.FontSize is null && typography.FontWeight is null
        && typography.LineHeight is null && typography.LetterSpacing is null
        && typography.Alignment is null && typography.Gradient is null;

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

    public sealed class LengthField
    {
        public decimal? Value { get; set; }
        public CssLengthUnit Unit { get; set; } = CssLengthUnit.Rem;

        public CssLength? ToModel() => Value is { } value ? new CssLength { Value = value, Unit = Unit } : null;

        public static LengthField From(CssLength? value) => new()
        {
            Value = value?.Value,
            Unit = value?.Unit ?? CssLengthUnit.Rem
        };
    }

    protected sealed class SpacingField
    {
        public LengthField BlockStart { get; set; } = new();
        public LengthField InlineEnd { get; set; } = new();
        public LengthField BlockEnd { get; set; } = new();
        public LengthField InlineStart { get; set; } = new();

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

        public static SpacingField From(CssLogicalSpacing? value) => new()
        {
            BlockStart = LengthField.From(value?.BlockStart),
            InlineEnd = LengthField.From(value?.InlineEnd),
            BlockEnd = LengthField.From(value?.BlockEnd),
            InlineStart = LengthField.From(value?.InlineStart)
        };
    }
}
