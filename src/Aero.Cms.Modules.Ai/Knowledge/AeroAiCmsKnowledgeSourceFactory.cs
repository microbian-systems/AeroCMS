using System.Text;
using Aero.Cms.Abstractions.Ai.Knowledge;
using Aero.Cms.Abstractions.Ai.Pipeline;
using Aero.Cms.Core.Entities;
using Aero.Cms.Html;

namespace Aero.Cms.Modules.Ai.Knowledge;

/// <summary>
/// Creates normalized AI knowledge sources from CMS documents without exposing executable source.
/// </summary>
internal static class AeroAiCmsKnowledgeSourceFactory
{
    private static readonly HashSet<string> NonContentElements = new(
        ["script", "style", "template"],
        StringComparer.OrdinalIgnoreCase);

    public static AeroAiKnowledgeSource Create(PageDocument page)
    {
        ArgumentNullException.ThrowIfNull(page);

        var publicSections = CreateMetadataSections(
            page.Title,
            page.Summary,
            page.SeoTitle,
            page.SeoDescription);
        AddSection(
            publicSections,
            "Body",
            ExtractText(page.PublishedContent));

        var managerSections = CreateMetadataSections(
            page.Title,
            page.Summary,
            page.SeoTitle,
            page.SeoDescription);
        AddSection(
            managerSections,
            "Body",
            ExtractText(page.DraftContent));

        return new AeroAiKnowledgeSource(
            TenantId: 0,
            SiteId: page.SiteId,
            SourceKind: AeroAiKnowledgeSourceKinds.Page,
            SourceId: page.Id,
            SourceUri: NormalizePagePath(page.Path, page.Slug),
            Culture: page.Culture,
            SourceRevision: page.ContentRevision,
            IsPublished: page.IsPubliclyVisible,
            IncludeInSearch: page.IncludeInSearch,
            IncludeInPublicAi: page.IncludeInPublicAi,
            Title: page.Title,
            PublicSections: publicSections,
            ManagerSections: managerSections);
    }

    public static AeroAiKnowledgeSource Create(PostDocument post)
    {
        ArgumentNullException.ThrowIfNull(post);

        var sections = CreateMetadataSections(
            post.Title,
            post.Excerpt,
            post.SeoTitle,
            post.SeoDescription);
        AddSection(sections, "Article", post.MarkdownContent);

        return new AeroAiKnowledgeSource(
            TenantId: 0,
            SiteId: post.SiteId,
            SourceKind: AeroAiKnowledgeSourceKinds.Post,
            SourceId: post.Id,
            SourceUri: $"/blog/{post.Slug.Trim('/')}",
            Culture: post.Culture,
            SourceRevision: RevisionFrom(post.ModifiedOn, post.CreatedOn),
            IsPublished: post.IsPubliclyVisible,
            IncludeInSearch: post.IncludeInSearch,
            IncludeInPublicAi: post.IncludeInPublicAi,
            Title: post.Title,
            PublicSections: sections,
            ManagerSections: sections);
    }

    public static AeroAiKnowledgeSource Create(DocsPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        var sections = CreateMetadataSections(
            page.Title,
            page.Summary,
            page.SeoTitle,
            page.SeoDescription);
        AddSection(sections, "Documentation", page.MarkdownContent);

        return new AeroAiKnowledgeSource(
            TenantId: 0,
            SiteId: page.SiteId,
            SourceKind: AeroAiKnowledgeSourceKinds.Docs,
            SourceId: page.Id,
            SourceUri: $"/docs/{page.Slug.Trim('/')}",
            Culture: page.Culture,
            SourceRevision: page.PublishedVersion > 0
                ? page.PublishedVersion
                : RevisionFrom(page.ModifiedOn, page.CreatedOn),
            IsPublished: page.IsPubliclyVisible,
            IncludeInSearch: page.IncludeInSearch,
            IncludeInPublicAi: page.IncludeInPublicAi,
            Title: page.Title,
            PublicSections: sections,
            ManagerSections: sections);
    }

    private static List<AeroAiKnowledgeSection> CreateMetadataSections(
        string title,
        string? summary,
        string? seoTitle,
        string? seoDescription)
    {
        var sections = new List<AeroAiKnowledgeSection>();
        AddSection(sections, "Entry", title);
        AddSection(sections, "Summary", summary);
        AddSection(sections, "SEO title", seoTitle);
        AddSection(sections, "SEO description", seoDescription);
        return sections;
    }

    private static void AddSection(
        ICollection<AeroAiKnowledgeSection> sections,
        string name,
        string? content)
    {
        if (!string.IsNullOrWhiteSpace(content))
        {
            sections.Add(new AeroAiKnowledgeSection(
                name,
                content.Trim(),
                AeroAiFieldExposure.Public));
        }
    }

    private static string ExtractText(HtmlPageContent? content)
    {
        if (content?.Root is null)
            return string.Empty;

        var text = new StringBuilder();
        AppendText(content.Root, text);
        return CollapseWhitespace(text);
    }

    private static void AppendText(HtmlNode node, StringBuilder destination)
    {
        if (node.Kind == HtmlNodeKind.Text)
        {
            if (!string.IsNullOrWhiteSpace(node.Text))
                destination.Append(node.Text).Append(' ');
            return;
        }

        if (node.Kind == HtmlNodeKind.Element
            && node.TagName is not null
            && NonContentElements.Contains(node.TagName))
        {
            return;
        }

        foreach (var child in node.Children)
            AppendText(child, destination);
    }

    private static string CollapseWhitespace(StringBuilder source)
    {
        if (source.Length == 0)
            return string.Empty;

        var collapsed = new StringBuilder(source.Length);
        var previousWasWhitespace = true;
        foreach (var character in source.ToString())
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace)
                    collapsed.Append(' ');
                previousWasWhitespace = true;
                continue;
            }

            collapsed.Append(character);
            previousWasWhitespace = false;
        }

        return collapsed.ToString().Trim();
    }

    private static string NormalizePagePath(string? path, string? slug)
    {
        var candidate = string.IsNullOrWhiteSpace(path)
            ? slug
            : path;
        candidate = (candidate ?? string.Empty).Trim();
        if (candidate.Length == 0)
            return "/";
        return candidate.StartsWith("/", StringComparison.Ordinal)
            ? candidate
            : $"/{candidate}";
    }

    private static long RevisionFrom(
        DateTimeOffset? modifiedOn,
        DateTimeOffset createdOn)
        => (modifiedOn ?? createdOn).UtcTicks;
}
