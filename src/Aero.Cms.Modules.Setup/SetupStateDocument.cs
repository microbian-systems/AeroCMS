namespace Aero.Cms.Modules.Setup;

/// <summary>
/// Stores the durable installation outcome and identifiers created by initial seeding.
/// </summary>
/// <remarks>
/// This document is separate from the file-based bootstrap lifecycle. It must not contain
/// administrator passwords, database credentials, or secret-provider credentials.
/// </remarks>
public sealed class SetupStateDocument
{
    /// <summary>
    /// Identifies the singleton setup-state document.
    /// </summary>
public const string FixedId = "cms/setup-state";

    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
public string Id { get; set; } = FixedId;
    /// <summary>
    /// Gets or sets whether initial setup completed.
    /// </summary>
public bool IsComplete { get; set; }
    /// <summary>
    /// Gets or sets the UTC completion timestamp.
    /// </summary>
public DateTimeOffset? CompletedAtUtc { get; set; }
    /// <summary>
    /// Gets or sets the database mode used for the installation.
    /// </summary>
public string DatabaseMode { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the cache mode used for the installation.
    /// </summary>
public string CacheMode { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the name of the selected secret provider.
    /// </summary>
public string SecretProvider { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the email address of the initial administrator.
    /// </summary>
public string AdminEmail { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the seeded site name.
    /// </summary>
public string SiteName { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the title of the seeded home page.
    /// </summary>
public string HomepageTitle { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the seeded blog name.
    /// </summary>
public string BlogName { get; set; } = string.Empty;
    
    // Tenant and Site information
    /// <summary>
    /// Gets or sets the identifier of the tenant created during setup.
    /// </summary>
public long? CreatedTenantId { get; set; }
    /// <summary>
    /// Gets or sets the identifier of the site created during setup.
    /// </summary>
public long? CreatedSiteId { get; set; }
    /// <summary>
    /// Gets or sets the configured site hostname.
    /// </summary>
public string? Hostname { get; set; }
    /// <summary>
    /// Gets or sets the site's default culture name.
    /// </summary>
public string? DefaultCulture { get; set; }
    /// <summary>
    /// Gets the culture names enabled during setup.
    /// </summary>
public List<string> SupportedCultures { get; set; } = [];
}
