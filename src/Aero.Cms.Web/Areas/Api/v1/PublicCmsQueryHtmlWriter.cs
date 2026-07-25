using System.Text;
using System.Text.Encodings.Web;
using Aero.Cms.Abstractions.Content;

namespace Aero.Cms.Web.Areas.Api.V1;

/// <summary>
/// Produces fixed, encoded semantic fragments for HTMX callers.
/// Stored markup and caller-supplied templates are intentionally unsupported.
/// </summary>
internal static class PublicCmsQueryHtmlWriter
{
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Default;

    public static string Pages(PublicQueryPage<PublicPageQueryItem> page)
        => WriteFlatList(
            "pages",
            page.Items.Select(item => (
                item.Id,
                item.Title,
                item.Summary,
                item.Path)));

    public static string Posts(PublicQueryPage<PublicPostQueryItem> page)
        => WriteFlatList(
            "posts",
            page.Items.Select(item => (
                item.Id,
                item.Title,
                item.Excerpt,
                $"/blog/{item.Slug.Trim('/')}")));

    public static string Docs(PublicQueryPage<PublicDocsQueryItem> page)
        => WriteFlatList(
            "docs",
            page.Items.Select(item => (
                item.Id,
                item.Title,
                item.Summary,
                $"/docs/{item.Slug.Trim('/')}")));

    public static string Content(ContentQueryResult result)
    {
        var output = new StringBuilder();
        output.Append("<div class=\"aero-query-fragment\" data-aero-query=\"")
            .Append(Encode(result.Name))
            .Append("\"><ul class=\"aero-query-tree\">");
        WriteNodes(output, result.Roots);
        output.Append("</ul></div>");
        return output.ToString();
    }

    private static string WriteFlatList(
        string queryName,
        IEnumerable<(string Id, string Title, string? Summary, string Path)> items)
    {
        var output = new StringBuilder();
        output.Append("<div class=\"aero-query-fragment\" data-aero-query=\"")
            .Append(Encode(queryName))
            .Append("\"><ul class=\"aero-query-list\">");
        foreach (var item in items)
        {
            output.Append("<li data-aero-id=\"")
                .Append(Encode(item.Id))
                .Append("\"><a href=\"")
                .Append(Encode(item.Path))
                .Append("\"><strong>")
                .Append(Encode(item.Title))
                .Append("</strong></a>");
            if (!string.IsNullOrWhiteSpace(item.Summary))
            {
                output.Append("<p>")
                    .Append(Encode(item.Summary))
                    .Append("</p>");
            }

            output.Append("</li>");
        }

        output.Append("</ul></div>");
        return output.ToString();
    }

    private static void WriteNodes(StringBuilder output, IEnumerable<ContentNode> nodes)
    {
        foreach (var node in nodes)
        {
            output.Append("<li data-aero-id=\"")
                .Append(Encode(node.Id))
                .Append("\" data-content-type=\"")
                .Append(Encode(node.ContentType))
                .Append("\"><strong>")
                .Append(Encode(node.Title))
                .Append("</strong>");
            if (node.Children.Length > 0)
            {
                output.Append("<ul>");
                WriteNodes(output, node.Children);
                output.Append("</ul>");
            }

            output.Append("</li>");
        }
    }

    private static string Encode(string value)
        => Encoder.Encode(value);
}
