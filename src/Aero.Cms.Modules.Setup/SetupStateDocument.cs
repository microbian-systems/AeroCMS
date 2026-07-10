namespace Aero.Cms.Modules.Setup;

/// <summary>
/// Represents a class for SetupStateDocument.
/// </summary>
public sealed class SetupStateDocument
{
        /// <summary>
    /// FixedId.
    /// </summary>
public const string FixedId = "cms/setup-state";

        /// <summary>
    /// Gets or sets the Id.
    /// </summary>
public string Id { get; set; } = FixedId;
        /// <summary>
    /// Gets or sets the Is Complete.
    /// </summary>
public bool IsComplete { get; set; }
        /// <summary>
    /// Gets or sets the Completed At Utc.
    /// </summary>
public DateTimeOffset? CompletedAtUtc { get; set; }
        /// <summary>
    /// Gets or sets the Database Mode.
    /// </summary>
public string DatabaseMode { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Cache Mode.
    /// </summary>
public string CacheMode { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Secret Provider.
    /// </summary>
public string SecretProvider { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Admin Email.
    /// </summary>
public string AdminEmail { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Site Name.
    /// </summary>
public string SiteName { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Homepage Title.
    /// </summary>
public string HomepageTitle { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Blog Name.
    /// </summary>
public string BlogName { get; set; } = string.Empty;
    
    // Tenant and Site information
        /// <summary>
    /// Gets or sets the Created Tenant Id.
    /// </summary>
public long? CreatedTenantId { get; set; }
        /// <summary>
    /// Gets or sets the Created Site Id.
    /// </summary>
public long? CreatedSiteId { get; set; }
        /// <summary>
    /// Gets or sets the Hostname.
    /// </summary>
public string? Hostname { get; set; }
        /// <summary>
    /// Gets or sets the Default Culture.
    /// </summary>
public string? DefaultCulture { get; set; }
        /// <summary>
    /// Gets or sets the Supported Cultures.
    /// </summary>
public List<string> SupportedCultures { get; set; } = [];
}
