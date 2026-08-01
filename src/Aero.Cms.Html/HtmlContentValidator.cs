using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Html;

/// <summary>
/// Manifest-backed validation for the single recursive HTML page tree.
/// </summary>
public sealed class HtmlContentValidator : IHtmlContentValidator
{
    private readonly HtmlElementCatalog _catalog;
    private readonly IHtmlContentModelPolicy _contentPolicy;
    private readonly IHtmlAttributePolicy _attributePolicy;
    private readonly HtmlContentValidationLimits _limits;

    /// <summary>Creates a validator from the catalog, policy strategies, and optional resource limits.</summary>
    /// <exception cref="ArgumentNullException">A required dependency is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A configured resource limit is not positive.</exception>
    public HtmlContentValidator(
        HtmlElementCatalog catalog,
        IHtmlContentModelPolicy contentPolicy,
        IHtmlAttributePolicy attributePolicy,
        HtmlContentValidationLimits? limits = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _contentPolicy = contentPolicy ?? throw new ArgumentNullException(nameof(contentPolicy));
        _attributePolicy = attributePolicy ?? throw new ArgumentNullException(nameof(attributePolicy));
        _limits = limits ?? new HtmlContentValidationLimits();

        if (_limits.MaximumDepth < 1) throw new ArgumentOutOfRangeException(nameof(limits), "Maximum depth must be positive.");
        if (_limits.MaximumNodeCount < 1) throw new ArgumentOutOfRangeException(nameof(limits), "Maximum node count must be positive.");
    }

    /// <inheritdoc />
    public Result<bool> Validate(HtmlPageContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.Root is null)
        {
            return AeroError.ValidationError(["Page content must have a root fragment."]);
        }

        var state = new ValidationState();
        if (content.Root.Kind is not HtmlNodeKind.Fragment)
        {
            state.Errors.Add("Page content must begin with a fragment root.");
        }

        ValidateNode(content.Root, parent: null, depth: 0, insideForm: false, state);

        return state.Errors.Count == 0
            ? new Result<bool>.Ok(true)
            : AeroError.ValidationError(state.Errors);
    }

    /// <summary>
    /// Traverses one node while enforcing depth, count, identity uniqueness, reference uniqueness, and form nesting.
    /// </summary>
    private void ValidateNode(
        HtmlNode node,
        HtmlNode? parent,
        int depth,
        bool insideForm,
        ValidationState state)
    {
        if (depth > _limits.MaximumDepth)
        {
            state.Errors.Add($"Page content exceeds the maximum depth of {_limits.MaximumDepth}.");
            return;
        }

        state.NodeCount++;
        if (state.NodeCount > _limits.MaximumNodeCount)
        {
            if (!state.NodeLimitReported)
            {
                state.Errors.Add($"Page content exceeds the maximum node count of {_limits.MaximumNodeCount}.");
                state.NodeLimitReported = true;
            }

            return;
        }

        if (!state.References.Add(node))
        {
            state.Errors.Add($"Node {node.NodeId} is referenced more than once; page structure must be a tree.");
            return;
        }

        if (node.NodeId <= 0 || !state.NodeIds.Add(node.NodeId))
        {
            state.Errors.Add(node.NodeId <= 0
                ? "Every page node must have a positive editor identity."
                : $"Page node identity {node.NodeId} is duplicated.");
        }

        if (node.Attributes is null || node.ThemeClasses is null || node.Children is null)
        {
            state.Errors.Add($"Node {node.NodeId} has a null collection.");
            return;
        }

        if (parent is not null)
        {
            var containment = _contentPolicy.CanContain(parent, node);
            if (!containment.IsAllowed)
            {
                state.Errors.Add(containment.Reason ?? "The page contains an invalid parent/child relationship.");
            }
        }

        switch (node.Kind)
        {
            case HtmlNodeKind.Fragment:
                ValidateFragment(node, parent, state.Errors);
                break;
            case HtmlNodeKind.Text:
                ValidateText(node, state.Errors);
                break;
            case HtmlNodeKind.Element:
                ValidateElement(node, state.Errors);
                break;
            default:
                state.Errors.Add($"Node {node.NodeId} has an unsupported node kind.");
                return;
        }

        var isForm = node.Kind is HtmlNodeKind.Element
            && node.TagName?.Equals("form", StringComparison.OrdinalIgnoreCase) == true;
        if (isForm && insideForm)
        {
            state.Errors.Add("Forms cannot be nested inside forms.");
        }

        foreach (var child in node.Children)
        {
            if (child is null)
            {
                state.Errors.Add($"Node {node.NodeId} contains a null child.");
                continue;
            }

            ValidateNode(child, node, depth + 1, insideForm || isForm, state);
        }
    }

    /// <summary>Enforces the non-rendered, root-only fragment invariant.</summary>
    private static void ValidateFragment(HtmlNode node, HtmlNode? parent, ICollection<string> errors)
    {
        if (parent is not null) errors.Add("Fragment nodes are allowed only at the page root.");
        if (node.TagName is not null || node.Text is not null || node.Attributes.Count > 0
            || node.ThemeClasses.Count > 0 || node.Style is not null)
        {
            errors.Add("The page fragment cannot carry a tag, text, attributes, classes, or style intent.");
        }
    }

    /// <summary>Enforces that text nodes carry literal text and no element-only state.</summary>
    private static void ValidateText(HtmlNode node, ICollection<string> errors)
    {
        if (node.TagName is not null || node.Attributes.Count > 0 || node.ThemeClasses.Count > 0
            || node.Style is not null || node.Children.Count > 0)
        {
            errors.Add($"Text node {node.NodeId} can contain literal text only.");
        }
    }

    /// <summary>Validates catalog membership, canonical casing, attributes, styles, and element-specific structure.</summary>
    private void ValidateElement(HtmlNode node, ICollection<string> errors)
    {
        if (!_catalog.TryGet(node.TagName, out var definition) || definition is null)
        {
            errors.Add($"Node {node.NodeId} uses an unsupported HTML element.");
            return;
        }

        if (!string.Equals(node.TagName, definition.Tag, StringComparison.Ordinal))
        {
            errors.Add($"Element tags must use the canonical lower-case form <{definition.Tag}>.");
        }

        if (node.Text is not null)
        {
            errors.Add($"Element <{definition.Tag}> must store literal content in child text nodes.");
        }

        if (definition.IsVoid && node.Children.Count > 0)
        {
            errors.Add($"<{definition.Tag}> is a void element and cannot have children.");
        }

        foreach (var (name, value) in node.Attributes)
        {
            if (value is null)
            {
                errors.Add($"The {name} attribute on <{definition.Tag}> has a null value.");
                continue;
            }

            var decision = _attributePolicy.CanRender(definition, name, value);
            if (!decision.IsAllowed)
            {
                errors.Add(decision.Reason ?? $"The {name} attribute is invalid on <{definition.Tag}>.");
            }
        }

        ValidateStyleCapabilities(node.Style, definition, errors);
        ValidateElementStructure(node, definition, errors);
    }

    /// <summary>Applies structure rules not expressible by the broad catalog child model.</summary>
    private static void ValidateElementStructure(
        HtmlNode node,
        HtmlElementDefinition definition,
        ICollection<string> errors)
    {
        if (!definition.Tag.Equals("details", StringComparison.OrdinalIgnoreCase))
        {
            ValidateNumericElement(node, definition, errors);
            return;
        }

        var summaries = node.Children
            .Where(child => child.Kind is HtmlNodeKind.Element
                && child.TagName?.Equals("summary", StringComparison.OrdinalIgnoreCase) == true)
            .ToArray();
        if (summaries.Length > 1)
        {
            errors.Add("<details> can contain at most one <summary> element.");
        }

        if (summaries.Length == 1 && !ReferenceEquals(node.Children.FirstOrDefault(), summaries[0]))
        {
            errors.Add("The <summary> element must be the first child of <details>.");
        }


        ValidateNumericElement(node, definition, errors);
    }

    /// <summary>Validates relational numeric constraints for progress and meter elements.</summary>
    private static void ValidateNumericElement(
        HtmlNode node,
        HtmlElementDefinition definition,
        ICollection<string> errors)
    {
        if (definition.Tag.Equals("progress", StringComparison.OrdinalIgnoreCase))
        {
            var maximum = DecimalAttribute(node, "max") ?? 1m;
            var value = DecimalAttribute(node, "value");
            if (value is < 0 || value > maximum)
            {
                errors.Add("The <progress> value must be between zero and max.");
            }

            return;
        }

        if (!definition.Tag.Equals("meter", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var minimum = DecimalAttribute(node, "min") ?? 0m;
        var maximumMeter = DecimalAttribute(node, "max") ?? 1m;
        var meterValue = DecimalAttribute(node, "value");
        var low = DecimalAttribute(node, "low");
        var high = DecimalAttribute(node, "high");
        var optimum = DecimalAttribute(node, "optimum");

        if (maximumMeter <= minimum)
        {
            errors.Add("The <meter> max value must be greater than min.");
        }

        if (meterValue is null || meterValue < minimum || meterValue > maximumMeter)
        {
            errors.Add("The <meter> value must be present and fall between min and max.");
        }

        if (low is not null && (low < minimum || low > maximumMeter)
            || high is not null && (high < minimum || high > maximumMeter)
            || optimum is not null && (optimum < minimum || optimum > maximumMeter))
        {
            errors.Add("The <meter> low, high, and optimum values must fall between min and max.");
        }

        if (low is not null && high is not null && low > high)
        {
            errors.Add("The <meter> low value cannot be greater than high.");
        }
    }

    /// <summary>Reads an invariant decimal attribute, returning <see langword="null"/> when absent or malformed.</summary>
    private static decimal? DecimalAttribute(HtmlNode node, string name) =>
        node.Attributes.TryGetValue(name, out var value)
        && decimal.TryParse(
            value,
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;

    /// <summary>Rejects semantic style groups not enabled by the element manifest.</summary>
    private static void ValidateStyleCapabilities(
        HtmlStyle? style,
        HtmlElementDefinition definition,
        ICollection<string> errors)
    {
        if (style is null) return;

        var capabilities = definition.StyleCapabilities;
        RequireCapability(HasLayoutIntent(style), "layout", definition.Tag, capabilities, errors);
        RequireCapability(style.Padding is not null || style.Margin is not null, "spacing", definition.Tag, capabilities, errors);
        RequireCapability(style.Surface is not null, "surface", definition.Tag, capabilities, errors);
        RequireCapability(style.Typography is not null, "typography", definition.Tag, capabilities, errors);
    }

    /// <summary>Determines whether any layout or sizing control is populated.</summary>
    private static bool HasLayoutIntent(HtmlStyle style) =>
        style.Display is not null || style.FlexDirection is not null || style.GridColumns is not null
        || style.StackOnSmallScreens || style.Gap is not null || style.AlignItems is not null
        || style.JustifyContent is not null || style.MinimumHeight is not null;

    /// <summary>Adds an error when populated style intent is absent from the element's capability allowlist.</summary>
    private static void RequireCapability(
        bool isUsed,
        string capability,
        string tag,
        IReadOnlyCollection<string> capabilities,
        ICollection<string> errors)
    {
        if (isUsed && !capabilities.Contains(capability, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add($"<{tag}> does not support {capability} style intent in the current catalog.");
        }
    }

    /// <summary>Holds traversal-wide identity, resource, and error state for one validation pass.</summary>
    private sealed class ValidationState
    {
        /// <summary>Gets accumulated validation diagnostics in traversal order.</summary>
        public List<string> Errors { get; } = [];
        /// <summary>Gets stable identities encountered during traversal.</summary>
        public HashSet<long> NodeIds { get; } = [];
        /// <summary>Gets object references encountered so shared subtrees and cycles fail closed.</summary>
        public HashSet<HtmlNode> References { get; } = new(ReferenceEqualityComparer.Instance);
        /// <summary>Gets or sets the number of nodes visited so far.</summary>
        public int NodeCount { get; set; }
        /// <summary>Gets or sets whether the node-limit error has already been emitted.</summary>
        public bool NodeLimitReported { get; set; }
    }
}
