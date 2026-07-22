using AeroDB.Sable;

namespace Aero.Cms.Modules.Setup;

/// <summary>
/// Stores the durable installation outcome and identifiers created by initial seeding.
/// </summary>
/// <remarks>
/// This document is separate from the file-based bootstrap lifecycle. It must not contain
/// administrator passwords, database credentials, or secret-provider credentials.
/// </remarks>
public sealed class SetupStateDocument : SableDocumentString, IVersioned
{
    /// <summary>
    /// Identifies the singleton setup-state document.
    /// </summary>
public const string FixedId = "cms/setup-state";

    /// <summary>
    /// Initializes the singleton setup-state document with its fixed identifier.
    /// </summary>
public SetupStateDocument() => Id = FixedId;
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
    /// Gets or sets the canonical authentication provider selected for CMS managers.
    /// </summary>
public string RequestedManagerAuthenticationProvider { get; set; } = "local";
    /// <summary>
    /// Gets or sets the canonical authentication provider selected for storefront members.
    /// </summary>
public string RequestedMemberAuthenticationProvider { get; set; } = "disabled";
    /// <summary>
    /// Gets or sets the email address of the initial administrator.
    /// </summary>
public string AdminEmail { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the exact locally authenticated administrator reserved for manager recovery.
    /// </summary>
    /// <remarks>
    /// This Snowflake identifier is non-secret. The corresponding Identity account remains the
    /// authority for the password hash, lockout state, and administrator role membership.
    /// </remarks>
public long? RecoveryAdministratorUserId { get; set; }
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
    /// <summary>Gets or sets the optimistic-concurrency version.</summary>
public long Version { get; set; }
}
