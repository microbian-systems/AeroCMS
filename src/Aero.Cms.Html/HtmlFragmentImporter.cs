using Aero.Core;
using Aero.Core.Railway;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Aero.Cms.Html;

/// <summary>
/// Strict, fail-closed conversion of static HTML fragments into fresh catalog-backed page content.
/// </summary>
public sealed class HtmlFragmentImporter : IHtmlFragmentImporter
{
    private readonly HtmlElementCatalog _catalog;
    private readonly IHtmlAttributePolicy _attributePolicy;
    private readonly IHtmlContentModelPolicy _contentPolicy;
    private readonly IHtmlContentValidator _contentValidator;
    private readonly HtmlFragmentImportLimits _limits;

    /// <summary>Creates an importer with the same catalog and policies used at save and render boundaries.</summary>
    /// <exception cref="ArgumentNullException">A required dependency is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A supplied import limit is outside the supported range.</exception>
    public HtmlFragmentImporter(
        HtmlElementCatalog catalog,
        IHtmlAttributePolicy attributePolicy,
        IHtmlContentModelPolicy contentPolicy,
        IHtmlContentValidator contentValidator,
        HtmlFragmentImportLimits? limits = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _attributePolicy = attributePolicy ?? throw new ArgumentNullException(nameof(attributePolicy));
        _contentPolicy = contentPolicy ?? throw new ArgumentNullException(nameof(contentPolicy));
        _contentValidator = contentValidator ?? throw new ArgumentNullException(nameof(contentValidator));
        _limits = limits ?? new HtmlFragmentImportLimits();

        if (_limits.MaximumSourceLength < 1) throw new ArgumentOutOfRangeException(nameof(limits), "Maximum source length must be positive.");
        if (_limits.MaximumDepth is < 1 or > 64) throw new ArgumentOutOfRangeException(nameof(limits), "Maximum depth must be between one and 64.");
        if (_limits.MaximumNodeCount is < 1 or > 5_000) throw new ArgumentOutOfRangeException(nameof(limits), "Maximum node count must be between one and 5000.");
    }

    /// <inheritdoc />
    public Result<HtmlPageContent> Import(string fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment);

        if (!HtmlFragmentSyntaxGuard.TryValidate(fragment, _limits, out var sourceElements, out var syntaxError))
        {
            return AeroError.ValidationError([syntaxError]);
        }

        try
        {
            var parser = new HtmlParser();
            var document = parser.ParseDocument("<html><body></body></html>");
            var context = document.Body;
            if (context is null)
            {
                return AeroError.ValidationError(["The isolated HTML fragment context could not be created."]);
            }

            var domNodes = parser.ParseFragment(fragment, context);
            if (!MatchesSourceShape(domNodes, sourceElements))
            {
                return AeroError.ValidationError(["The HTML fragment requires parser recovery or normalization and cannot be imported."]);
            }

            var content = new HtmlPageContent();
            var nodeCount = 1;
            foreach (var domNode in domNodes)
            {
                var converted = ConvertNode(domNode, parent: content.Root, depth: 1, ref nodeCount);
                if (converted is not null)
                {
                    content.Root.Children.Add(converted);
                }
            }

            var validation = _contentValidator.Validate(content);
            return validation is Result<bool>.Ok
                ? new Result<HtmlPageContent>.Ok(content)
                : validation is Result<bool>.Failure failure
                    ? new Result<HtmlPageContent>.Failure(failure.Error)
                    : AeroError.ValidationError(["The imported HTML fragment could not be validated."]);
        }
        catch (HtmlFragmentImportException exception)
        {
            return AeroError.ValidationError([exception.Message]);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return AeroError.ValidationError(["The HTML fragment could not be parsed safely."]);
        }
    }

    /// <summary>Converts one supported DOM node while enforcing recursive resource limits.</summary>
    private HtmlNode? ConvertNode(INode domNode, HtmlNode parent, int depth, ref int nodeCount)
    {
        if (depth > _limits.MaximumDepth)
        {
            throw new HtmlFragmentImportException($"The HTML fragment exceeds the maximum depth of {_limits.MaximumDepth}.");
        }

        return domNode.NodeType switch
        {
            NodeType.Text => ConvertText(domNode.TextContent, parent),
            NodeType.Element => ConvertElement((IElement)domNode, parent, depth, ref nodeCount),
            _ => throw new HtmlFragmentImportException("The HTML fragment contains an unsupported DOM node.")
        };
    }

    /// <summary>Preserves literal text while discarding formatting whitespace where HTML semantics permit.</summary>
    private HtmlNode? ConvertText(string? text, HtmlNode parent)
    {
        if (text is null)
        {
            throw new HtmlFragmentImportException("The HTML fragment contains an invalid text node.");
        }

        if (string.IsNullOrWhiteSpace(text) && !AcceptsMeaningfulWhitespace(parent))
        {
            return null;
        }

        return HtmlNode.CreateText(text);
    }

    /// <summary>Converts a canonical HTML element only after catalog, nesting, attribute, and URL checks succeed.</summary>
    private HtmlNode ConvertElement(IElement element, HtmlNode parent, int depth, ref int nodeCount)
    {
        if (!string.Equals(element.NamespaceUri, "http://www.w3.org/1999/xhtml", StringComparison.Ordinal)
            || !IsCanonicalLowerCase(element.LocalName))
        {
            throw new HtmlFragmentImportException("The HTML fragment contains an unsupported or non-HTML element.");
        }

        if (!_catalog.TryGet(element.LocalName, out var definition)
            || definition is null)
        {
            throw new HtmlFragmentImportException(
                $"The '<{element.LocalName}>' element is not supported in page fragments.");
        }

        nodeCount++;
        if (nodeCount > _limits.MaximumNodeCount)
        {
            throw new HtmlFragmentImportException($"The HTML fragment exceeds the maximum node count of {_limits.MaximumNodeCount}.");
        }

        var containment = _contentPolicy.CanContain(parent, HtmlNode.CreateElement(definition.Tag));
        if (!containment.IsAllowed)
        {
            throw new HtmlFragmentImportException(containment.Reason ?? "The HTML fragment contains an invalid parent/child relationship.");
        }

        var node = _catalog.CreateElement(definition.Tag);
        foreach (var attribute in element.Attributes)
        {
            if (!string.IsNullOrEmpty(attribute.NamespaceUri)
                || !IsCanonicalLowerCase(attribute.LocalName))
            {
                throw new HtmlFragmentImportException("The HTML fragment contains an unsupported attribute namespace or name.");
            }

            var decision = _attributePolicy.CanRender(definition, attribute.LocalName, attribute.Value);
            if (!decision.IsAllowed)
            {
                throw new HtmlFragmentImportException(decision.Reason ?? "The HTML fragment contains an unsafe or unsupported attribute.");
            }

            if (!node.Attributes.TryAdd(attribute.LocalName, attribute.Value))
            {
                throw new HtmlFragmentImportException($"The HTML fragment contains duplicate '{attribute.Name}' attributes.");
            }
        }

        foreach (var child in element.ChildNodes)
        {
            var converted = ConvertNode(child, node, depth + 1, ref nodeCount);
            if (converted is not null)
            {
                node.Children.Add(converted);
            }
        }

        return node;
    }

    /// <summary>Detects parser recovery or reparenting by comparing source and parsed element ancestry.</summary>
    private static bool MatchesSourceShape(INodeList nodes, IReadOnlyList<HtmlFragmentSourceElement> sourceElements)
    {
        var parsedElements = new List<HtmlFragmentSourceElement>();
        CollectElements(nodes, parentTagName: null, parsedElements);
        return parsedElements.Count == sourceElements.Count
            && parsedElements.Zip(sourceElements).All(pair =>
                string.Equals(pair.First.TagName, pair.Second.TagName, StringComparison.Ordinal)
                && string.Equals(pair.First.ParentTagName, pair.Second.ParentTagName, StringComparison.Ordinal));
    }

    /// <summary>Collects parsed elements and direct-parent tags in depth-first source order.</summary>
    private static void CollectElements(
        INodeList nodes,
        string? parentTagName,
        ICollection<HtmlFragmentSourceElement> elements)
    {
        foreach (var node in nodes)
        {
            if (node is not IElement element)
            {
                continue;
            }

            elements.Add(new HtmlFragmentSourceElement(element.LocalName, parentTagName));
            CollectElements(element.ChildNodes, element.LocalName, elements);
        }
    }

    /// <summary>Preserves whitespace-only text only where it is semantically significant.</summary>
    private static bool AcceptsMeaningfulWhitespace(HtmlNode parent) =>
        parent.Kind is HtmlNodeKind.Element
        && parent.TagName is "pre" or "textarea";

    /// <summary>Rejects parser-normalized casing so imported source remains explicit and canonical.</summary>
    private static bool IsCanonicalLowerCase(string value) => value.All(character => !char.IsUpper(character));

    /// <summary>Represents an expected validation failure during strict DOM conversion.</summary>
    private sealed class HtmlFragmentImportException(string message) : Exception(message);
}
