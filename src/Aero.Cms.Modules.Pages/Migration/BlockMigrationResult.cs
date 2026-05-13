namespace Aero.Cms.Modules.Pages.Migration;

/// <summary>
/// Result of a block content migration run.
/// </summary>
/// <param name="Migrated">Pages where legacy blocks were successfully migrated.</param>
/// <param name="Skipped">Pages already at current schema or with no legacy blocks.</param>
/// <param name="Failed">Pages where migration encountered an error.</param>
/// <param name="AffectedPageIds">All page IDs that were processed (migrated or skipped).</param>
/// <param name="Errors">Error messages for failed pages.</param>
public sealed record BlockMigrationResult(
    int Migrated,
    int Skipped,
    int Failed,
    List<long> AffectedPageIds,
    List<string> Errors);
