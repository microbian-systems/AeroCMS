namespace Aero.Cms.Modules.Pages.Migration;

/// <summary>
/// Migrates legacy block content (BlockBase documents referenced by PageEditorState.BlockIdMap)
/// into NeoCompositionBlock documents and rebuilds the editor state mappings.
/// </summary>
public interface IBlockContentMigrationService
{
    /// <summary>The current Neo block schema version. Pages at or above this version are skipped.</summary>
    int CurrentSchemaVersion { get; }

    /// <summary>Migrate block content for a single page.</summary>
    Task<BlockMigrationResult> MigratePageAsync(long pageId, CancellationToken ct = default);

    /// <summary>Migrate block content for all pages in a site.</summary>
    Task<BlockMigrationResult> MigrateSiteAsync(long siteId, CancellationToken ct = default);
}
