using Wolverine.Attributes;

namespace Aero.Cms.Modules.Pages.Migration;

/// <summary>
/// Wolverine handler for block content migration commands.
/// Wraps migration in Wolverine's automatic Marten transaction for safety.
/// </summary>
[WolverineHandler]
public sealed class BlockContentMigrationHandler(IBlockContentMigrationService migrationService)
{
    public Task<BlockMigrationResult> Handle(
        MigratePageBlockContent command,
        CancellationToken ct)
    {
        return migrationService.MigratePageAsync(command.PageId, ct);
    }

    public Task<BlockMigrationResult> Handle(
        MigrateSiteBlockContent command,
        CancellationToken ct)
    {
        return migrationService.MigrateSiteAsync(command.SiteId, ct);
    }
}
