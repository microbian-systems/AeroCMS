using AeroDB.Sable;

namespace Aero.Cms.Modules.Jobs;

internal sealed class ContentImportJobDocument : SableDocument, IVersioned
{
    public string RequestIdentity { get; set; } = string.Empty;
    public long TenantId { get; set; }
    public long SiteId { get; set; }
    public string ImporterKey { get; set; } = string.Empty;
    public string ImporterVersion { get; set; } = string.Empty;
    public string SourceFingerprint { get; set; } = string.Empty;
    public string SelectionFingerprint { get; set; } = string.Empty;
    public string OptionsJson { get; set; } = "{}";
    public string Actor { get; set; } = string.Empty;
    public bool Activate { get; set; }
    public string State { get; set; } = "Pending";
    public int Attempt { get; set; }
    public string? Checkpoint { get; set; }
    public long ProgressCurrent { get; set; }
    public long? ProgressTotal { get; set; }
    public string? LastError { get; set; }
    public string? LeaseToken { get; set; }
    public long FencingVersion { get; set; }
    public DateTimeOffset? LeaseExpiresOn { get; set; }
    public DateTimeOffset? NextAttemptOn { get; set; }
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ModifiedOn { get; set; } = DateTimeOffset.UtcNow;
    public long Version { get; set; }
}
