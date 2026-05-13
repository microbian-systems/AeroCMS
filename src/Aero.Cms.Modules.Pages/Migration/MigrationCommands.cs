namespace Aero.Cms.Modules.Pages.Migration;

/// <summary>Command to migrate block content for a single page.</summary>
public sealed record MigratePageBlockContent(long PageId);

/// <summary>Command to migrate block content for all pages in a site.</summary>
public sealed record MigrateSiteBlockContent(long SiteId);
