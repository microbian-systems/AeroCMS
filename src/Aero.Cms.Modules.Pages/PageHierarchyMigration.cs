using AeroDB.Sable;

namespace Aero.Cms.Modules.Pages;

/// <summary>
/// Backfills materialized hierarchy fields on page documents that predate the
/// hierarchy model.
/// </summary>
/// <remarks>
/// Migration writes are performed through one Sable session. If any page already
/// has non-default hierarchy data, <see cref="MigrateAsync"/> skips the entire set.
/// </remarks>
public sealed class PageHierarchyMigration
{
    private readonly IDocumentStore _store;
    private readonly ILogger<PageHierarchyMigration> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PageHierarchyMigration"/> class.
    /// </summary>
    /// <param name="store">The Sable document store used to inspect page documents.</param>
    /// <param name="logger">The migration logger.</param>
public PageHierarchyMigration(IDocumentStore store, ILogger<PageHierarchyMigration> logger)
    {
        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// Checks whether the hierarchy marker query completes without finding a root page
    /// whose path is <c>/</c>.
    /// </summary>
    /// <param name="ct">The token used while querying the document store.</param>
    /// <returns>
    /// <see langword="true"/> when no root page with the default path is found;
    /// otherwise <see langword="false"/>. Store failures are logged and also return
    /// <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// The return value reflects the current implementation, which is the inverse of
    /// the method name: a matching default-path page produces <see langword="false"/>.
    /// </remarks>
    public async Task<bool> IsMigrationNeededAsync(CancellationToken ct = default)
    {
        try
        {
            await using var session = await _store.LightweightSessionAsync();
            var needsMigration = await session
                .Query<PageDocument>()
                .Where(x => x.Path == "/" && x.ParentId == null).AnyAsync(ct);

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
    /// <param name="ct">The token used for the query and final save.</param>
    /// <returns>A task that completes after the backfill is persisted or skipped.</returns>
    /// <remarks>
    /// Rooted trees are traversed recursively. Pages whose parent cannot be found are
    /// subsequently treated as roots. Cancellation and store failures propagate.
    /// </remarks>
    public async Task MigrateAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Starting page hierarchy migration...");

        await using var session = await _store.LightweightSessionAsync();

        // Determine which pages already have proper paths
        var migratedCount = await session
            .Query<PageDocument>()
            .Where(x => x.Path != "/" || x.Depth > 0 || x.Order > 0).CountAsync(ct);

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

