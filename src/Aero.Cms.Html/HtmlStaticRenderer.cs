using System.Net;
using System.Text;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Html;

/// <summary>
/// Produces encoded static HTML from the page-content tree for public rendering.
/// </summary>
public sealed class HtmlStaticRenderer(
    HtmlElementCatalog catalog,
    IHtmlContentModelPolicy contentPolicy,
    IHtmlAttributePolicy attributePolicy,
    IHtmlContentValidator contentValidator)
{
    /// <summary>
    /// Renders a page-content tree when every node, nesting relationship, and attribute is valid.
    /// </summary>
    public Result<string> Render(HtmlPageContent content) => RenderMarkup(content, compiledStyles: null);

    /// <summary>
    /// Renders page markup with precompiled classes and returns its separately hosted stylesheet.
    /// </summary>
    public Result<RenderedHtmlPage> RenderPage(HtmlPageContent content, CompiledPageStyles compiledStyles)
    {
        ArgumentNullException.ThrowIfNull(compiledStyles);

        var markup = RenderMarkup(content, compiledStyles);
        return markup switch
        {
            Result<string>.Ok(var value) => new Result<RenderedHtmlPage>.Ok(new RenderedHtmlPage
            {
                Markup = value,
                CssText = compiledStyles.CssText,
                StyleContentHash = compiledStyles.ContentHash
            }),
            Result<string>.Failure(var error) => new Result<RenderedHtmlPage>.Failure(error),
            _ => new Result<RenderedHtmlPage>.Failure(AeroError.CreateError("Unknown HTML rendering result state."))
        };
    }

    private Result<string> RenderMarkup(HtmlPageContent content, CompiledPageStyles? compiledStyles)
    {
        ArgumentNullException.ThrowIfNull(content);

        var validation = contentValidator.Validate(content);
        if (validation is Result<bool>.Failure validationFailure)
        {
            return validationFailure.Error;
        }

        if (content.Root.Kind is not HtmlNodeKind.Fragment)
        {
            return AeroError.ValidationError(["Page content must begin with a fragment root."]);
        }

        var writer = new StringBuilder();
        var result = RenderNode(content.Root, parent: null, writer, compiledStyles);
        if (result is Result<bool>.Failure failure)
        {
            return failure.Error;
        }

        return new Result<string>.Ok(writer.ToString());
    }

    private Result<bool> RenderNode(
        HtmlNode node,
        HtmlNode? parent,
        StringBuilder writer,
        CompiledPageStyles? compiledStyles)
    {
        if (parent is not null)
        {
            var nesting = contentPolicy.CanContain(parent, node);
            if (!nesting.IsAllowed)
            {
                return AeroError.ValidationError([nesting.Reason ?? "The HTML tree contains an invalid parent/child relationship."]);
            }
        }

        if (node.Kind == HtmlNodeKind.Fragment)
        {
            foreach (var child in node.Children)
            {
                var childResult = RenderNode(child, node, writer, compiledStyles);
                if (childResult is Result<bool>.Failure failure)
                {
                    return failure.Error;
                }
            }

            return true;
        }

        if (node.Kind == HtmlNodeKind.Text)
        {
            if (node.Children.Count > 0)
            {
                return AeroError.ValidationError(["Text nodes cannot have children."]);
            }

            writer.Append(WebUtility.HtmlEncode(node.Text ?? string.Empty));
            return true;
        }

        if (node.Kind is not HtmlNodeKind.Element || !catalog.TryGet(node.TagName, out var definition) || definition is null)
        {
            return AeroError.ValidationError(["The HTML tree contains an unsupported element."]);
        }

        if (definition.IsVoid && node.Children.Count > 0)
        {
            return AeroError.ValidationError([$"<{definition.Tag}> is a void element and cannot have children."]);
        }

        writer.Append('<').Append(definition.Tag);
        var attributesResult = WriteAttributes(node, definition, writer, compiledStyles);
        if (attributesResult is Result<bool>.Failure attributeFailure)
        {
            return attributeFailure.Error;
        }

        writer.Append('>');

        if (!definition.IsVoid)
        {
            foreach (var child in node.Children)
            {
                var childResult = RenderNode(child, node, writer, compiledStyles);
                if (childResult is Result<bool>.Failure failure)
                {
                    return failure.Error;
                }
            }

            writer.Append("</").Append(definition.Tag).Append('>');
        }

        return true;
    }

    private Result<bool> WriteAttributes(
        HtmlNode node,
        HtmlElementDefinition definition,
        StringBuilder writer,
        CompiledPageStyles? compiledStyles)
    {
        var attributes = new SortedDictionary<string, string>(node.Attributes, StringComparer.OrdinalIgnoreCase);
        attributes.Remove("class");

        foreach (var (name, value) in attributes)
        {
            var decision = attributePolicy.CanRender(definition, name, value);
            if (!decision.IsAllowed)
            {
                return AeroError.ValidationError([decision.Reason ?? $"The {name} attribute is invalid."]);
            }

            writer.Append(' ')
                .Append(name.ToLowerInvariant())
                .Append("=\"")
                .Append(WebUtility.HtmlEncode(value))
                .Append('"');
        }

        IEnumerable<string> classNames = node.ThemeClasses;
        if (node.Attributes.TryGetValue("class", out var explicitClasses))
        {
            classNames = new[] { explicitClasses }.Concat(classNames);
        }

        if (compiledStyles is not null)
        {
            classNames = classNames.Concat(compiledStyles.ClassesFor(node.NodeId));
        }

        var renderedClasses = string.Join(' ', classNames
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal));

        if (!string.IsNullOrWhiteSpace(renderedClasses))
        {
            writer.Append(" class=\"")
                .Append(WebUtility.HtmlEncode(renderedClasses))
                .Append('"');
        }

        return true;
    }
}
