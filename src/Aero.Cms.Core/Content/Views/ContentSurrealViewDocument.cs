using AeroDB.Sable;

namespace Aero.Cms.Core.Content.Views;

/// <summary>Append-only, tenant-and-site-scoped persisted view revision.</summary>
public sealed class ContentSurrealViewDocument : SableDocument
{
    public long TenantId { get; set; }
    public long SiteId { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string ShapeAlias { get; set; } = string.Empty;
    public string ShapeFingerprint { get; set; } = string.Empty;
    public string SelectStatement { get; set; } = string.Empty;
    public string IdentityField { get; set; } = string.Empty;
    public string? TitleField { get; set; }
    public string? EntrySelectStatement { get; set; }
    public string? SearchSelectStatement { get; set; }
    public long Version { get; set; }
    public bool IsPublished { get; set; }
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public string? CreatedBy { get; set; }
    public bool CacheEnabled { get; set; } = true;
    public long CacheDurationSeconds { get; set; } = 300;
    public long CacheGeneration { get; set; }
    public long? RelationshipId { get; set; }
    public string? RelationshipSchemaFingerprint { get; set; }
    public bool PublicExecutionEligible { get; set; }
    public string? PublicExecutionIneligibilityReason { get; set; }
    public string? PublicPlanAlias { get; set; }
    public string? PublicPlanFingerprint { get; set; }
    public string? PublicPlanDialectFingerprint { get; set; }
}
