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

    private HtmlNode ConvertElement(IElement element, HtmlNode parent, int depth, ref int nodeCount)
    {
        if (!string.Equals(element.NamespaceUri, "http://www.w3.org/1999/xhtml", StringComparison.Ordinal)
            || !IsCanonicalLowerCase(element.LocalName)
            || !_catalog.TryGet(element.LocalName, out var definition)
            || definition is null)
        {
            throw new HtmlFragmentImportException("The HTML fragment contains an unsupported or non-HTML element.");
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

    private static bool MatchesSourceShape(INodeList nodes, IReadOnlyList<HtmlFragmentSourceElement> sourceElements)
    {
        var parsedElements = new List<HtmlFragmentSourceElement>();
        CollectElements(nodes, parentTagName: null, parsedElements);
        return parsedElements.Count == sourceElements.Count
            && parsedElements.Zip(sourceElements).All(pair =>
                string.Equals(pair.First.TagName, pair.Second.TagName, StringComparison.Ordinal)
                && string.Equals(pair.First.ParentTagName, pair.Second.ParentTagName, StringComparison.Ordinal));
    }

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

    private static bool AcceptsMeaningfulWhitespace(HtmlNode parent) =>
        parent.Kind is HtmlNodeKind.Element
        && parent.TagName is "pre" or "textarea";

    private static bool IsCanonicalLowerCase(string value) => value.All(character => !char.IsUpper(character));

    private sealed class HtmlFragmentImportException(string message) : Exception(message);
}
