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

        ValidateNode(content.Root, parent: null, depth: 0, state);

        return state.Errors.Count == 0
            ? new Result<bool>.Ok(true)
            : AeroError.ValidationError(state.Errors);
    }

    private void ValidateNode(HtmlNode node, HtmlNode? parent, int depth, ValidationState state)
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

        foreach (var child in node.Children)
        {
            if (child is null)
            {
                state.Errors.Add($"Node {node.NodeId} contains a null child.");
                continue;
            }

            ValidateNode(child, node, depth + 1, state);
        }
    }

    private static void ValidateFragment(HtmlNode node, HtmlNode? parent, ICollection<string> errors)
    {
        if (parent is not null) errors.Add("Fragment nodes are allowed only at the page root.");
        if (node.TagName is not null || node.Text is not null || node.Attributes.Count > 0
            || node.ThemeClasses.Count > 0 || node.Style is not null)
        {
            errors.Add("The page fragment cannot carry a tag, text, attributes, classes, or style intent.");
        }
    }

    private static void ValidateText(HtmlNode node, ICollection<string> errors)
    {
        if (node.TagName is not null || node.Attributes.Count > 0 || node.ThemeClasses.Count > 0
            || node.Style is not null || node.Children.Count > 0)
        {
            errors.Add($"Text node {node.NodeId} can contain literal text only.");
        }
    }

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
    }

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

    private static bool HasLayoutIntent(HtmlStyle style) =>
        style.Display is not null || style.FlexDirection is not null || style.GridColumns is not null
        || style.StackOnSmallScreens || style.Gap is not null || style.AlignItems is not null
        || style.JustifyContent is not null || style.MinimumHeight is not null;

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

    private sealed class ValidationState
    {
        public List<string> Errors { get; } = [];
        public HashSet<long> NodeIds { get; } = [];
        public HashSet<HtmlNode> References { get; } = new(ReferenceEqualityComparer.Instance);
        public int NodeCount { get; set; }
        public bool NodeLimitReported { get; set; }
    }
}
