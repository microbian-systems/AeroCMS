using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Core.Entities;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Pages.Migration;

/// <summary>
/// Admin API routes for triggering block content migration.
/// All endpoints require admin authorization.
/// </summary>
public static class MigrationApiRoutes
{
    private const string Tag = "Migration";
    private const string Prefix = "/api/v1/admin/migration";

    public static void MapMigrationRoutes(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(Prefix)
            .RequireAuthorization()
            .WithTags(Tag);

        // ── Page-level migration ──────────────────────────────
        group.MapPost("/pages/{pageId:long}", MigratePageAsync)
            .WithName("MigratePageBlockContent")
            .WithDescription("Migrate legacy block content for a single page to Neo blocks.");

        // ── Page-level migration (body — safe from JS Number truncation) ──
        group.MapPost("/pages", MigratePageFromBodyAsync)
            .WithName("MigratePageBlockContentFromBody")
            .WithDescription("Migrate a page via JSON body. Use when Snowflake ID exceeds JS Number precision.");

        // ── Step 3: Document structure migration (PageDocument → PageEditorState) ──
        group.MapPost("/document-migration", RunDocumentMigrationAsync)
            .WithName("RunDocumentMigration")
            .WithDescription("Run Step 3 document-structure migration for a site. Creates PageEditorState from PageDocument blocks.");

        // ── Site-level migration ──────────────────────────────
        group.MapPost("/sites/{siteId:long}", MigrateSiteAsync)
            .WithName("MigrateSiteBlockContent")
            .WithDescription("Migrate legacy block content for all pages in a site to Neo blocks.");

        // ── Site-level migration (body — safe from JS Number truncation) ──
        group.MapPost("/sites", MigrateSiteFromBodyAsync)
            .WithName("MigrateSiteBlockContentFromBody")
            .WithDescription("Migrate a site via JSON body. Use when Snowflake ID exceeds JS Number precision.");

        // ── Status check ──────────────────────────────────────
        group.MapGet("/status", GetMigrationStatusAsync)
            .WithName("GetMigrationStatus")
            .WithDescription("Check current schema version and migration readiness.");

        // ── Dry-run (preview) ─────────────────────────────────
        group.MapGet("/preview/{pageId:long}", PreviewMigrationAsync)
            .WithName("PreviewMigration")
            .WithDescription("Preview what migration would do for a page without changing data.");

        // ── Page listing (for finding valid IDs to test with) ────
        group.MapGet("/pages-list", GetPageIdsAsync)
            .WithName("GetMigrationPageIds")
            .WithDescription("List page IDs available for migration. Useful when testing with large Snowflake IDs.");

        // ── Editor state diagnostic ─────────────────────────────
        group.MapGet("/diagnose/{pageId:long}", DiagnosePageAsync)
            .WithName("DiagnosePageMigration")
            .WithDescription("Diagnose a page: show PageEditorState, block IDs, and what would be migrated.");
    }

    private static async Task<IResult> MigratePageAsync(
        long pageId,
        IBlockContentMigrationService migration,
        CancellationToken ct)
    {
        var result = await migration.MigratePageAsync(pageId, ct);
        return result.Failed > 0
            ? Results.Conflict(result)
            : Results.Ok(result);
    }

    private static async Task<IResult> MigratePageFromBodyAsync(
        [FromBody] MigratePageRequest request,
        IBlockContentMigrationService migration,
        CancellationToken ct)
    {
        if (!long.TryParse(request.PageId, out var pageId))
            return TypedResults.BadRequest(new { error = $"Invalid pageId: '{request.PageId}'" });

        var result = await migration.MigratePageAsync(pageId, ct);
        return result.Failed > 0
            ? Results.Conflict(result)
            : Results.Ok(result);
    }

    private static async Task<IResult> RunDocumentMigrationAsync(
        [FromBody] DocumentMigrationRequest request,
        PageDocumentMigration docMigration,
        CancellationToken ct)
    {
        if (!long.TryParse(request.SiteId, out var siteId))
            return TypedResults.BadRequest(new { error = $"Invalid siteId: '{request.SiteId}'" });

        var result = await docMigration.MigrateAsync(siteId, ct);
        return Results.Ok(result);
    }

    /// <summary>
    /// Request body for page migration. Uses <c>PageId</c> as a string
    /// to avoid JavaScript Number precision truncation with Snowflake IDs (&gt; 2^53).
    /// </summary>
    public sealed record MigratePageRequest(string PageId);

    /// <summary>
    /// Request body for document migration. Uses <c>SiteId</c> as a string.
    /// </summary>
    public sealed record DocumentMigrationRequest(string SiteId);

    private static async Task<IResult> MigrateSiteAsync(
        long siteId,
        IBlockContentMigrationService migration,
        CancellationToken ct)
    {
        var result = await migration.MigrateSiteAsync(siteId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> MigrateSiteFromBodyAsync(
        [FromBody] DocumentMigrationRequest request,
        IBlockContentMigrationService migration,
        CancellationToken ct)
    {
        if (!long.TryParse(request.SiteId, out var siteId))
            return TypedResults.BadRequest(new { error = $"Invalid siteId: '{request.SiteId}'" });

        var result = await migration.MigrateSiteAsync(siteId, ct);
        return Results.Ok(result);
    }

    private static IResult GetMigrationStatusAsync(
        IBlockContentMigrationService migration)
    {
        return Results.Ok(new
        {
            CurrentSchemaVersion = migration.CurrentSchemaVersion,
            Status = "ready",
        });
    }

    private static async Task<IResult> GetPageIdsAsync(
        [FromQuery] long siteId,
        IQuerySession querySession,
        CancellationToken ct)
    {
        var ids = await querySession.Query<PageDocument>()
            .Where(p => p.SiteId == siteId && !p.Deleted)
            .Select(p => p.Id)
            .ToListAsync(ct);

        return Results.Ok(new { siteId, count = ids.Count, pageIds = ids });
    }

    private static async Task<IResult> DiagnosePageAsync(
        long pageId,
        IDocumentSession session,
        ILegacyBlockMapper mapper,
        CancellationToken ct)
    {
        var page = await session.LoadAsync<PageDocument>(pageId, ct);
        var editorState = await session.LoadAsync<PageEditorState>(pageId, ct);

        var placements = editorState?.Blocks ?? [];
        var blockDetails = new List<object>();

        foreach (var p in placements)
        {
            if (p.BlockId is { } blockId)
            {
                var block = await session.LoadAsync<BlockBase>(blockId, ct);
                var nodes = block is not null ? mapper.MapFromBlock(block) : [];
                blockDetails.Add(new
                {
                    clientId = p.ClientId,
                    blockId,
                    blockType = block?.BlockType ?? "null",
                    exists = block is not null,
                    mappedNodes = nodes.Count,
                    nodeCatalogIds = nodes.Select(n => n.CatalogId).ToList(),
                });
            }
            else
            {
                blockDetails.Add(new { clientId = p.ClientId, blockId = "null", blockType = "n/a", exists = false });
            }
        }

        return Results.Ok(new
        {
            pageId,
            schemaVersion = page?.BlockSchemaVersion ?? 0,
            placementCount = placements.Count,
            placements = blockDetails,
        });
    }

    private static async Task<IResult> PreviewMigrationAsync(
        long pageId,
        IBlockContentMigrationService migration,
        IHttpContextAccessor? httpAccessor,
        CancellationToken ct)
    {
        return Results.Ok(new
        {
            PageId = pageId,
            Status = "preview_not_implemented",
            Note = "Run actual migration via POST /api/v1/admin/migration/pages/{pageId}",
        });
    }
}
