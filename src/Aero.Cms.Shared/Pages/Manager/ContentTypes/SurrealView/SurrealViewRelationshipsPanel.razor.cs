using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Abstractions.Http.Clients;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.ContentTypes.SurrealView;

/// <summary>Renders discovered and CMS-managed relationship ownership and DDL lifecycle states.</summary>
public partial class SurrealViewRelationshipsPanel
{
    [Parameter] public IReadOnlyList<ContentRelationshipSummary> Relationships { get; set; } = [];
    [Parameter] public IReadOnlyList<ContentViewShapeOption> Shapes { get; set; } = [];
    [Parameter] public string CurrentShapeAlias { get; set; } = string.Empty;
    [Parameter] public RelationshipDdlPreviewResponse? Preview { get; set; }
    [Parameter] public bool IsConfigured { get; set; }
    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public bool IsMutating { get; set; }
    [Parameter] public string? ErrorMessage { get; set; }
    [Parameter] public EventCallback<SaveContentRelationshipDraftCommand> SaveDraftRequested { get; set; }
    [Parameter] public EventCallback<long> PreviewDdlRequested { get; set; }
    [Parameter] public EventCallback<long> ApplyDdlRequested { get; set; }

    private bool _isEditing;
    private bool _editingExisting;
    private string _alias = string.Empty;
    private string? _sourceShapeAlias;
    private string? _targetShapeAlias;
    private string _sourceTable = string.Empty;
    private string _targetTable = string.Empty;
    private string? _sourceField;
    private string? _targetField;
    private string? _edgeTable;
    private ContentRelationshipKind _kind = ContentRelationshipKind.RecordLink;
    private ContentRelationshipCardinality _cardinality = ContentRelationshipCardinality.ManyToOne;

    private bool CanSubmitDraft => !IsMutating
        && !string.IsNullOrWhiteSpace(_alias)
        && !string.IsNullOrWhiteSpace(_sourceTable)
        && !string.IsNullOrWhiteSpace(_targetTable)
        && (_kind switch
        {
            ContentRelationshipKind.FieldJoin => !string.IsNullOrWhiteSpace(_sourceField) && !string.IsNullOrWhiteSpace(_targetField),
            ContentRelationshipKind.RecordLink or ContentRelationshipKind.SelfHierarchy => !string.IsNullOrWhiteSpace(_sourceField),
            ContentRelationshipKind.GraphEdge => true,
            _ => false
        });

    private void BeginCreate()
    {
        _alias = string.Empty;
        _sourceShapeAlias = CurrentShapeAlias;
        _targetShapeAlias = null;
        _sourceTable = string.Empty;
        _targetTable = string.Empty;
        _sourceField = null;
        _targetField = null;
        _edgeTable = null;
        _kind = ContentRelationshipKind.RecordLink;
        _cardinality = ContentRelationshipCardinality.ManyToOne;
        _editingExisting = false;
        _isEditing = true;
    }

    private void BeginEdit(ContentRelationshipSummary relationship)
    {
        if (relationship.OwnershipState != ContentRelationshipOwnershipState.CmsDraft) return;
        _alias = relationship.Alias;
        _sourceShapeAlias = relationship.SourceShapeAlias;
        _targetShapeAlias = relationship.TargetShapeAlias;
        _sourceTable = relationship.SourceTable;
        _targetTable = relationship.TargetTable;
        _sourceField = relationship.SourceField;
        _targetField = relationship.TargetField;
        _edgeTable = relationship.EdgeTable;
        _kind = relationship.Kind;
        _cardinality = relationship.Cardinality;
        _editingExisting = true;
        _isEditing = true;
    }

    private Task SubmitDraftAsync()
    {
        if (!CanSubmitDraft) return Task.CompletedTask;
        _isEditing = false;
        return SaveDraftRequested.InvokeAsync(new SaveContentRelationshipDraftCommand(
            _alias.Trim(),
            new SaveContentRelationshipDraftRequest(
                Normalize(_sourceShapeAlias),
                Normalize(_targetShapeAlias),
                _sourceTable.Trim(),
                _targetTable.Trim(),
                Normalize(_sourceField),
                Normalize(_targetField),
                Normalize(_edgeTable),
                _kind,
                _cardinality)));
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string FormatKind(ContentRelationshipKind kind) => kind switch
    {
        ContentRelationshipKind.FieldJoin => "Field join",
        ContentRelationshipKind.RecordLink => "Record link",
        ContentRelationshipKind.GraphEdge => "Graph edge",
        ContentRelationshipKind.SelfHierarchy => "Self hierarchy",
        _ => kind.ToString()
    };

    private static string FormatOwnership(ContentRelationshipOwnershipState state) => state switch
    {
        ContentRelationshipOwnershipState.ExternalDiscovered => "Database-owned",
        ContentRelationshipOwnershipState.CmsDraft => "CMS draft",
        ContentRelationshipOwnershipState.Applied => "Applied · locked",
        ContentRelationshipOwnershipState.Derived => "Query-derived",
        ContentRelationshipOwnershipState.Drifted => "Drifted · locked",
        _ => state.ToString()
    };

    private static string OwnershipClass(ContentRelationshipOwnershipState state) => state switch
    {
        ContentRelationshipOwnershipState.Drifted => "is-drifted",
        ContentRelationshipOwnershipState.CmsDraft => "is-draft",
        ContentRelationshipOwnershipState.Applied => "is-applied",
        _ => "is-read-only"
    };

    private static string EditingState(ContentRelationshipSummary relationship) => relationship.OwnershipState switch
    {
        ContentRelationshipOwnershipState.ExternalDiscovered => "Read-only because this relationship already exists in SurrealDB.",
        ContentRelationshipOwnershipState.Derived => "Read-only because the query defines this relationship.",
        ContentRelationshipOwnershipState.Applied => "Locked after DDL application.",
        ContentRelationshipOwnershipState.Drifted => "Locked because the live schema no longer matches the recorded fingerprint.",
        _ when relationship.CanApplyDdl => "Editable until reviewed DDL is applied.",
        _ when relationship.CanPreviewDdl => "Editable; DDL preview is available, but schema application is disabled in this host.",
        _ => "DDL lifecycle is unavailable in this host."
    };

    private static string FormatSchemaTarget(ContentRelationshipSummary relationship)
    {
        if (!string.IsNullOrWhiteSpace(relationship.EdgeTable)) return $"Edge table: {relationship.EdgeTable}";
        if (!string.IsNullOrWhiteSpace(relationship.SourceField) && !string.IsNullOrWhiteSpace(relationship.TargetField))
            return $"{relationship.SourceField} → {relationship.TargetField}";
        return relationship.SourceField ?? relationship.TargetField ?? "—";
    }
}
