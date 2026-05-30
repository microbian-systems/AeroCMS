using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Modules.Docs.Areas.Docs.Models;
using Aero.Core;
using Wolverine;
using static Aero.Core.Railway.Prelude;

namespace Aero.Cms.Modules.Docs;

public sealed class DocsTreeService(
    IDocumentSession session,
    IMessageBus bus,
    ILogger<DocsTreeService> logger) : IDocsTreeService
{
    public async Task<Result<IReadOnlyList<DocsSidebarNode>, AeroError>> GetSidebarTreeAsync(
        long siteId,
        long activeId = 0,
        bool publishedOnly = true,
        CancellationToken ct = default)
    {
        try
        {
            var docs = await LoadSiteDocsAsync(siteId, publishedOnly, ct);
            var root = docs.FirstOrDefault(doc => string.Equals(doc.Slug, "docs", StringComparison.OrdinalIgnoreCase));
            if (root is null)
                return Ok<IReadOnlyList<DocsSidebarNode>, AeroError>([]);

            var childrenByParent = docs
                .GroupBy(doc => doc.ParentId)
                .ToDictionary(group => group.Key ?? 0, group => group.OrderBy(doc => doc.Order).ThenBy(doc => doc.Title).ToList());

            return Ok<IReadOnlyList<DocsSidebarNode>, AeroError>(BuildNodes(root.Id, 0, activeId, childrenByParent));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build docs sidebar tree for site {SiteId}", siteId);
            return Fail<IReadOnlyList<DocsSidebarNode>, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<DocsPage>, AeroError>> GetBreadcrumbsAsync(
        long siteId,
        long docId,
        bool publishedOnly = true,
        CancellationToken ct = default)
    {
        try
        {
            var docs = await LoadSiteDocsAsync(siteId, publishedOnly, ct);
            var current = docs.FirstOrDefault(doc => doc.Id == docId);
            if (current is null)
                return Ok<IReadOnlyList<DocsPage>, AeroError>([]);

            var byId = docs.ToDictionary(doc => doc.Id);
            var breadcrumbs = new List<DocsPage>();
            var node = current;

            while (true)
            {
                if (!string.Equals(node.Slug, "docs", StringComparison.OrdinalIgnoreCase))
                    breadcrumbs.Add(node);

                if (node.ParentId is not { } parentId || !byId.TryGetValue(parentId, out node!))
                    break;
            }

            breadcrumbs.Reverse();
            return Ok<IReadOnlyList<DocsPage>, AeroError>(breadcrumbs);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build docs breadcrumbs for doc {DocId}", docId);
            return Fail<IReadOnlyList<DocsPage>, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public IReadOnlyList<HeadingItem> ExtractHeadings(string? markdown)
        => HeadingExtractor.Extract(markdown);

    public async Task<Result<DocsPage, AeroError>> CreateChildSectionAsync(
        long siteId,
        long spaceId,
        long parentId,
        string title,
        string? summary,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(title))
                return Fail<DocsPage, AeroError>(AeroError.ValidationError(["Title is required"]));

            var docs = await LoadSiteDocsAsync(siteId, publishedOnly: false, ct);
            var parent = docs.FirstOrDefault(doc => doc.Id == parentId);
            if (parent is null || !IsWithinSpace(parent, spaceId, docs))
                return Fail<DocsPage, AeroError>(AeroError.NotFoundError("Parent section not found in this docs space"));

            var order = docs
                .Where(doc => doc.ParentId == parentId)
                .Select(doc => doc.Order)
                .DefaultIfEmpty(-1)
                .Max() + 1;

            var page = new DocsPage
            {
                Id = Snowflake.NewId(),
                SiteId = siteId,
                TranslationSetId = null,
                Culture = parent.Culture,
                Title = title.Trim(),
                Summary = summary,
                Slug = GenerateUniqueChildSlug(parent.Slug, title, docs),
                ParentId = parentId,
                Order = order,
                MarkdownContent = string.Empty,
                ModifiedOn = DateTimeOffset.UtcNow,
                ModifiedBy = "system"
            };

            page.TranslationSetId = page.Id;

            session.Store(page);
            await session.SaveChangesAsync(ct);

            await bus.PublishAsync(new DocViewModelCreated(page.ToViewModel(), $"Doc created: {page.Slug}"));
            await bus.PublishAsync(new DocsPageContentUpdatedEvent(page.Id, page.SiteId, page.Slug, null));

            logger.LogInformation("Created docs child section {DocId} under parent {ParentId}", page.Id, parentId);
            return Ok<DocsPage, AeroError>(page);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create docs child section under parent {ParentId}", parentId);
            return Fail<DocsPage, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<DocsPage, AeroError>> MoveSectionAsync(
        long siteId,
        long spaceId,
        long sectionId,
        long newParentId,
        int? order,
        bool rewriteSlug,
        CancellationToken ct = default)
    {
        try
        {
            if (sectionId == spaceId)
                return Fail<DocsPage, AeroError>(AeroError.ValidationError(["A space root cannot be moved from inside the space editor"]));

            var docs = await LoadSiteDocsAsync(siteId, publishedOnly: false, ct);
            var section = docs.FirstOrDefault(doc => doc.Id == sectionId);
            var newParent = docs.FirstOrDefault(doc => doc.Id == newParentId);

            if (section is null || !IsWithinSpace(section, spaceId, docs))
                return Fail<DocsPage, AeroError>(AeroError.NotFoundError("Section not found in this docs space"));

            if (newParent is null || !IsWithinSpace(newParent, spaceId, docs))
                return Fail<DocsPage, AeroError>(AeroError.NotFoundError("Target parent not found in this docs space"));

            if (sectionId == newParentId || IsDescendantOf(newParent, sectionId, docs))
                return Fail<DocsPage, AeroError>(AeroError.ValidationError(["A section cannot be moved under itself or one of its descendants"]));

            var changed = new List<(DocsPage Page, string? OldSlug)> { (section, section.Slug) };
            var oldSlug = section.Slug;

            section.ParentId = newParentId;
            section.Order = order ?? NextOrder(newParentId, docs, sectionId);
            section.ModifiedOn = DateTimeOffset.UtcNow;
            section.ModifiedBy = "system";

            if (rewriteSlug)
            {
                var newSlug = GenerateUniqueChildSlug(newParent.Slug, SlugLeaf(section.Slug), docs, section.Id);
                section.Slug = newSlug;
                RewriteDescendantSlugs(oldSlug, newSlug, section, docs, changed);
            }

            foreach (var (page, _) in changed)
                session.Store(page);

            await session.SaveChangesAsync(ct);

            foreach (var (page, previousSlug) in changed)
            {
                await bus.PublishAsync(new DocViewModelUpdated(page.ToViewModel(), $"Doc moved: {page.Slug}"));
                await bus.PublishAsync(new DocsPageContentUpdatedEvent(page.Id, page.SiteId, page.Slug, previousSlug));
            }

            logger.LogInformation("Moved docs section {DocId} under parent {ParentId}", sectionId, newParentId);
            return Ok<DocsPage, AeroError>(section);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to move docs section {DocId}", sectionId);
            return Fail<DocsPage, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<bool, AeroError>> ReorderSiblingsAsync(
        long siteId,
        long spaceId,
        long parentId,
        IReadOnlyList<long> orderedIds,
        CancellationToken ct = default)
    {
        try
        {
            if (orderedIds.Count == 0)
                return Ok<bool, AeroError>(true);

            var docs = await LoadSiteDocsAsync(siteId, publishedOnly: false, ct);
            var parent = docs.FirstOrDefault(doc => doc.Id == parentId);
            if (parent is null || !IsWithinSpace(parent, spaceId, docs))
                return Fail<bool, AeroError>(AeroError.NotFoundError("Parent section not found in this docs space"));

            var siblings = docs
                .Where(doc => doc.ParentId == parentId)
                .ToDictionary(doc => doc.Id);

            if (orderedIds.Any(id => !siblings.ContainsKey(id)))
                return Fail<bool, AeroError>(AeroError.ValidationError(["Reorder request contains a section outside the selected parent"]));

            for (var index = 0; index < orderedIds.Count; index++)
            {
                var page = siblings[orderedIds[index]];
                page.Order = index;
                page.ModifiedOn = DateTimeOffset.UtcNow;
                page.ModifiedBy = "system";
                session.Store(page);
            }

            await session.SaveChangesAsync(ct);

            foreach (var id in orderedIds)
            {
                var page = siblings[id];
                await bus.PublishAsync(new DocViewModelUpdated(page.ToViewModel(), $"Doc reordered: {page.Slug}"));
                await bus.PublishAsync(new DocsPageContentUpdatedEvent(page.Id, page.SiteId, page.Slug, page.Slug));
            }

            logger.LogInformation("Reordered {Count} docs sections under parent {ParentId}", orderedIds.Count, parentId);
            return Ok<bool, AeroError>(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reorder docs sections under parent {ParentId}", parentId);
            return Fail<bool, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    private Task<IReadOnlyList<DocsPage>> LoadSiteDocsAsync(long siteId, bool publishedOnly, CancellationToken ct)
    {
        var query = session.Query<DocsPage>()
            .Where(doc => doc.SiteId == siteId);

        if (publishedOnly)
            query = query.Where(doc => doc.PublicationState == ContentPublicationState.Published);

        return query
            .OrderBy(doc => doc.Order)
            .ThenBy(doc => doc.Title)
            .ToListAsync(ct);
    }

    private static List<DocsSidebarNode> BuildNodes(
        long parentId,
        int depth,
        long activeId,
        IReadOnlyDictionary<long, List<DocsPage>> childrenByParent)
    {
        if (!childrenByParent.TryGetValue(parentId, out var children))
            return [];

        var nodes = new List<DocsSidebarNode>();
        foreach (var page in children)
        {
            var childNodes = BuildNodes(page.Id, depth + 1, activeId, childrenByParent);
            var isActive = page.Id == activeId;
            var isAncestor = childNodes.Any(node => node.IsActive || node.IsExpanded);

            nodes.Add(new DocsSidebarNode
            {
                Id = page.Id,
                Title = page.Title,
                Slug = page.Slug,
                Order = page.Order,
                Depth = depth,
                IsActive = isActive,
                IsExpanded = isAncestor || isActive,
                Children = childNodes
            });
        }

        return nodes;
    }

    private static bool IsWithinSpace(DocsPage page, long spaceId, IReadOnlyList<DocsPage> docs)
    {
        if (page.Id == spaceId)
            return true;

        var parentId = page.ParentId;
        while (parentId is { } id)
        {
            if (id == spaceId)
                return true;

            parentId = docs.FirstOrDefault(doc => doc.Id == id)?.ParentId;
        }

        return false;
    }

    private static bool IsDescendantOf(DocsPage page, long ancestorId, IReadOnlyList<DocsPage> docs)
    {
        var parentId = page.ParentId;
        while (parentId is { } id)
        {
            if (id == ancestorId)
                return true;

            parentId = docs.FirstOrDefault(doc => doc.Id == id)?.ParentId;
        }

        return false;
    }

    private static int NextOrder(long parentId, IReadOnlyList<DocsPage> docs, long excludeId)
        => docs
            .Where(doc => doc.ParentId == parentId && doc.Id != excludeId)
            .Select(doc => doc.Order)
            .DefaultIfEmpty(-1)
            .Max() + 1;

    private static void RewriteDescendantSlugs(
        string oldParentSlug,
        string newParentSlug,
        DocsPage page,
        IReadOnlyList<DocsPage> docs,
        List<(DocsPage Page, string? OldSlug)> changed)
    {
        foreach (var child in docs.Where(doc => doc.ParentId == page.Id))
        {
            var previous = child.Slug;
            if (previous.StartsWith($"{oldParentSlug}/", StringComparison.OrdinalIgnoreCase))
            {
                child.Slug = $"{newParentSlug}/{previous[(oldParentSlug.Length + 1)..]}";
                child.ModifiedOn = DateTimeOffset.UtcNow;
                child.ModifiedBy = "system";
                changed.Add((child, previous));
            }

            RewriteDescendantSlugs(oldParentSlug, newParentSlug, child, docs, changed);
        }
    }

    private static string GenerateUniqueChildSlug(string parentSlug, string title, IReadOnlyList<DocsPage> docs, long? excludeId = null)
    {
        var baseSlug = $"{NormalizeSlug(parentSlug)}/{GenerateSlug(title)}".Trim('/');
        var candidate = baseSlug;
        var suffix = 2;

        while (docs.Any(doc => doc.Id != excludeId && string.Equals(doc.Slug, candidate, StringComparison.OrdinalIgnoreCase)))
            candidate = $"{baseSlug}-{suffix++}";

        return candidate;
    }

    private static string NormalizeSlug(string value)
        => string.Join('/', value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(GenerateSlug)
            .Where(part => !string.IsNullOrWhiteSpace(part)));

    private static string SlugLeaf(string value)
    {
        var parts = value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? value : parts[^1];
    }

    private static string GenerateSlug(string value)
    {
        var slug = new string(value
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray());

        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);

        return slug.Trim('-');
    }
}
