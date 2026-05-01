namespace Aero.Cms.Modules.Setup;

public sealed class SetupStateDocument
{
    public const string FixedId = "cms/setup-state";

    public string Id { get; set; } = FixedId;
    public bool IsComplete { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string DatabaseMode { get; set; } = string.Empty;
    public string CacheMode { get; set; } = string.Empty;
    public string SecretProvider { get; set; } = string.Empty;
    public string AdminEmail { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public string HomepageTitle { get; set; } = string.Empty;
    public string BlogName { get; set; } = string.Empty;
    
    // Tenant and Site information
    public long? CreatedTenantId { get; set; }
    public long? CreatedSiteId { get; set; }
    public string? Hostname { get; set; }
    public string? DefaultCulture { get; set; }
}
