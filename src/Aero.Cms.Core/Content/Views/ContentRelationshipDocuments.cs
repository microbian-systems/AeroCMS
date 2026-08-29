using AeroDB.Sable;
using Aero.Cms.Abstractions.Content.Views;

namespace Aero.Cms.Core.Content.Views;

/// <summary>Tenant/site scoped relationship definition. Schema application history is stored separately.</summary>
public sealed class ContentRelationshipDocument : SableDocument
{
    public long TenantId { get; set; }
    public long SiteId { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string SourceShapeAlias { get; set; } = string.Empty;
    public string TargetShapeAlias { get; set; } = string.Empty;
    public string SourceTable { get; set; } = string.Empty;
    public string TargetTable { get; set; } = string.Empty;
    public string? SourceField { get; set; }
    public string? TargetField { get; set; }
    public string? EdgeTable { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Cardinality { get; set; } = string.Empty;
    public string OwnershipState { get; set; } = string.Empty;
    public string SchemaFingerprint { get; set; } = string.Empty;
}

public sealed class ContentRelationshipDdlJournalDocument : SableDocument
{
    public long TenantId { get; set; }
    public long SiteId { get; set; }
    public long RelationshipId { get; set; }
    public string AppliedSchemaFingerprint { get; set; } = string.Empty;
    public DateTimeOffset AppliedOn { get; set; }
    public string? AppliedBy { get; set; }
}
