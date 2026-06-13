namespace Aero.Cms.Modules.Pages;

/// <summary>
/// One-time migration to populate hierarchy fields (Path, Depth, Order, Deleted)
/// on existing PageDocument records. Run during module startup if needed.
/// </summary>
public sealed class PageHierarchyMigration
{
    private readonly IDocumentStore _store;
    private readonly ILogger<PageHierarchyMigration> _logger;

    public PageHierarchyMigration(IDocumentStore store, ILogger<PageHierarchyMigration> logger)
    {
        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// Returns true if migration is needed (any page has default Path).
    /// </summary>
    public async Task<bool> IsMigrationNeededAsync(CancellationToken ct = default)
    {
        try
        {
            await using var session = _store.LightweightSession();
            var needsMigration = await session
                .Query<PageDocument>()
                .AnyAsync(x => x.Path == "/" && x.ParentId == null, ct);

            return !needsMigration;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check if page hierarchy migration is needed");
            return false;
        }
    }

    /// <summary>
    /// Migrates all pages: sets Path, Depth, Order=0, and ensures Deleted=false.
    /// Pages are processed in dependency order (roots first, then by depth).
    /// </summary>
    public async Task MigrateAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Starting page hierarchy migration...");

        await using var session = _store.LightweightSession();

        // Determine which pages already have proper paths
        var migratedCount = await session
            .Query<PageDocument>()
            .CountAsync(x => x.Path != "/" || x.Depth > 0 || x.Order > 0, ct);

        if (migratedCount > 0)
        {
            _logger.LogInformation(
                "{Count} pages already have hierarchy data — skipping migration", migratedCount);
            return;
        }

        // Load all pages
        var allPages = await session
            .Query<PageDocument>()
            .ToListAsync(ct);

        _logger.LogInformation("Migrating {Count} pages", allPages.Count);

        // Build parent lookup: for each page, find its children
        var childrenByParent = new Dictionary<long?, List<PageDocument>>();
        foreach (var page in allPages)
        {
            if (!childrenByParent.ContainsKey(page.ParentId))
                childrenByParent[page.ParentId] = [];

            childrenByParent[page.ParentId].Add(page);
        }

        var count = 0;

        // Process roots first, then recurse
        if (childrenByParent.TryGetValue(null, out var roots))
        {
            foreach (var root in roots)
            {
                MigrateTree(root, null, 0, session, childrenByParent, ref count);
            }
        }

        // Handle orphaned pages (parentId set but parent not found)
        var processed = 0;
        foreach (var page in allPages.Where(p => p.Path == "/"))
        {
            page.Path = "/" + page.Slug;
            page.Depth = 0;
            page.Order = processed++;
            page.Deleted = false;
            session.Update(page);
            count++;
        }

        await session.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Page hierarchy migration complete. Updated {Count} pages.", count);
    }

    private void MigrateTree(
        PageDocument page,
        string? parentPath,
        int depth,
        IDocumentSession session,
        Dictionary<long?, List<PageDocument>> childrenByParent,
        ref int count)
    {
        var path = parentPath is null
            ? "/" + page.Slug
            : parentPath.TrimEnd('/') + "/" + page.Slug;

        page.Path = path;
        page.Depth = depth;
        page.Order = 0;
        page.Deleted = false;
        session.Update(page);
        count++;

        // Recurse into children
        if (childrenByParent.TryGetValue(page.Id, out var children))
        {
            // Order children by their existing order or title
            var ordered = children.OrderBy(c => c.Order).ThenBy(c => c.Title).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                ordered[i].Order = i;
                MigrateTree(ordered[i], path, depth + 1, session, childrenByParent, ref count);
            }
        }
    }
}
