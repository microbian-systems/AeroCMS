using System.Net;
using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.ContentTypes.SurrealView;

/// <summary>Owns the manager state for a persisted content type's query-backed view.</summary>
public partial class SurrealViewEditor : IAsyncDisposable
{
    [Parameter, EditorRequired] public string ContentTypeAlias { get; set; } = string.Empty;
    [Inject] private IContentViewsHttpClient ViewsApi { get; set; } = default!;

    private CancellationTokenSource? _loadCts;
    private string? _loadedAlias;
    private IReadOnlyList<ContentViewShapeOption> _shapes = [];
    private IReadOnlyList<ContentRelationshipSummary> _relationships = [];
    private ContentViewPreviewResponse? _preview;
    private RelationshipDdlPreviewResponse? _relationshipPreview;
    private string _shapeAlias = string.Empty;
    private string _selectStatement = string.Empty;
    private string _identityField = string.Empty;
    private string? _titleField;
    private string _entrySelectStatement = string.Empty;
    private string _searchSelectStatement = string.Empty;
    private string _editorKey = "new";
    private string? _loadError;
    private string? _previewError;
    private string? _relationshipsError;
    private string? _pageMessage;
    private long _revisionVersion;
    private ContentViewPublicationState _publicationState = ContentViewPublicationState.Draft;
    private bool _cacheEnabled = true;
    private int _cacheDurationSeconds = 300;
    private bool _isConfigured;
    private bool _isDirty;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _isPublishing;
    private bool _isPreviewing;
    private bool _relationshipsLoading;
    private bool _relationshipMutating;
    private bool _pageMessageIsError;
    private bool _publicExecutionEligible;
    private string? _publicExecutionIneligibilityReason;

    private bool _isBusy => _isSaving || _isPublishing || _isPreviewing || _relationshipMutating;
    private bool CanSave => !_isBusy
        && _shapes.Count > 0
        && !string.IsNullOrWhiteSpace(_shapeAlias)
        && !string.IsNullOrWhiteSpace(_selectStatement)
        && !string.IsNullOrWhiteSpace(_identityField)
        && !string.IsNullOrWhiteSpace(_entrySelectStatement)
        && !string.IsNullOrWhiteSpace(_searchSelectStatement);
    private bool CanPreview => !_isBusy && !string.IsNullOrWhiteSpace(_shapeAlias) && !string.IsNullOrWhiteSpace(_selectStatement);
    private bool CanPublish => _isConfigured
        && _publicExecutionEligible
        && !_isDirty
        && !_isBusy
        && _revisionVersion > 0;
    private string PublicationLabel => _publicationState == ContentViewPublicationState.Published ? "Published" : "Draft";
    private string PublishTitle => _isDirty
        ? "Save this draft before publishing"
        : !_publicExecutionEligible
            ? _publicExecutionIneligibilityReason ?? "This saved revision is not eligible for public execution."
            : "Publish this exact saved revision";

    protected override async Task OnParametersSetAsync()
    {
        if (!string.Equals(_loadedAlias, ContentTypeAlias, StringComparison.Ordinal))
        {
            _loadedAlias = ContentTypeAlias;
            await LoadAsync();
        }
    }

    private Task ReloadAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;
        _isLoading = true;
        _loadError = null;
        _pageMessage = null;
        _preview = null;
        _relationshipPreview = null;
        _relationships = [];
        try
        {
            var shapes = await ViewsApi.GetShapesAsync(ct);
            if (shapes is Result<IReadOnlyList<ContentViewShapeOption>, AeroError>.Failure shapeFailure)
            {
                _loadError = FormatError(shapeFailure.Error);
                return;
            }

            _shapes = ((Result<IReadOnlyList<ContentViewShapeOption>, AeroError>.Ok)shapes).Value;
            if (_shapes.Count == 0)
            {
                _loadError = "No code-owned content shapes are registered in this host.";
                return;
            }

            var draft = await ViewsApi.GetAsync(ContentTypeAlias, ct);
            switch (draft)
            {
                case Result<ContentViewEditorSnapshot, AeroError>.Ok ok:
                    ApplySnapshot(ok.Value);
                    await LoadRelationshipsAsync(ct);
                    break;
                case Result<ContentViewEditorSnapshot, AeroError>.Failure failure when IsNotFound(failure.Error):
                    _isConfigured = false;
                    _shapeAlias = _shapes[0].Alias;
                    _selectStatement = string.Empty;
                    _identityField = string.Empty;
                    _titleField = null;
                    _entrySelectStatement = string.Empty;
                    _searchSelectStatement = string.Empty;
                    _cacheEnabled = true;
                    _cacheDurationSeconds = 300;
                    _revisionVersion = 0;
                    _publicationState = ContentViewPublicationState.Draft;
                    _publicExecutionEligible = false;
                    _publicExecutionIneligibilityReason = null;
                    _isDirty = false;
                    _editorKey = $"{ContentTypeAlias}-new";
                    break;
                case Result<ContentViewEditorSnapshot, AeroError>.Failure failure:
                    _loadError = FormatError(failure.Error);
                    break;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        finally
        {
            if (!ct.IsCancellationRequested) _isLoading = false;
        }
    }

    private async Task SaveAsync()
    {
        if (!CanSave) return;
        _isSaving = true;
        ClearPageMessage();
        try
        {
            var result = await ViewsApi.SaveDraftAsync(
                ContentTypeAlias,
                new SaveContentViewDraftRequest(
                    _shapeAlias,
                    _selectStatement,
                    _identityField,
                    _titleField,
                    _entrySelectStatement,
                    _searchSelectStatement,
                    _cacheEnabled,
                    _cacheDurationSeconds));
            switch (result)
            {
                case Result<ContentViewEditorSnapshot, AeroError>.Ok ok:
                    ApplySnapshot(ok.Value);
                    SetPageMessage("Draft saved. Preview and publish this exact revision when it is ready.", false);
                    await LoadRelationshipsAsync();
                    break;
                case Result<ContentViewEditorSnapshot, AeroError>.Failure failure:
                    SetPageMessage(FormatError(failure.Error), true);
                    break;
            }
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task PreviewAsync()
    {
        if (!CanPreview) return;
        _isPreviewing = true;
        _previewError = null;
        _preview = null;
        try
        {
            var result = await ViewsApi.PreviewAsync(
                ContentTypeAlias,
                new PreviewContentViewRequest(_shapeAlias, _selectStatement, 20));
            switch (result)
            {
                case Result<ContentViewPreviewResponse, AeroError>.Ok ok:
                    _preview = ok.Value;
                    break;
                case Result<ContentViewPreviewResponse, AeroError>.Failure failure:
                    _previewError = FormatError(failure.Error);
                    break;
            }
        }
        finally
        {
            _isPreviewing = false;
        }
    }

    private async Task PublishAsync()
    {
        if (!CanPublish) return;
        _isPublishing = true;
        ClearPageMessage();
        try
        {
            var result = await ViewsApi.PublishAsync(ContentTypeAlias, _revisionVersion);
            switch (result)
            {
                case Result<ContentViewEditorSnapshot, AeroError>.Ok ok:
                    ApplySnapshot(ok.Value);
                    SetPageMessage("Published this immutable view revision.", false);
                    break;
                case Result<ContentViewEditorSnapshot, AeroError>.Failure failure:
                    SetPageMessage(FormatError(failure.Error), true);
                    break;
            }
        }
        finally
        {
            _isPublishing = false;
        }
    }

    private async Task InvalidateCacheAsync()
    {
        if (!_isConfigured || _isBusy) return;
        ClearPageMessage();
        var result = await ViewsApi.InvalidateCacheAsync(ContentTypeAlias);
        switch (result)
        {
            case Result<bool, AeroError>.Ok { Value: true }:
                SetPageMessage("The site-scoped content-view cache was invalidated.", false);
                break;
            case Result<bool, AeroError>.Failure failure:
                SetPageMessage(FormatError(failure.Error), true);
                break;
        }
    }

    private async Task LoadRelationshipsAsync(CancellationToken ct = default)
    {
        if (!_isConfigured) return;
        _relationshipsLoading = true;
        _relationshipsError = null;
        try
        {
            var result = await ViewsApi.GetRelationshipsAsync(ContentTypeAlias, ct);
            switch (result)
            {
                case Result<IReadOnlyList<ContentRelationshipSummary>, AeroError>.Ok ok:
                    _relationships = ok.Value;
                    break;
                case Result<IReadOnlyList<ContentRelationshipSummary>, AeroError>.Failure failure:
                    _relationships = [];
                    _relationshipsError = FormatError(failure.Error);
                    break;
            }
        }
        finally
        {
            _relationshipsLoading = false;
        }
    }

    private async Task PreviewRelationshipDdlAsync(long relationshipId)
    {
        _relationshipMutating = true;
        _relationshipsError = null;
        _relationshipPreview = null;
        try
        {
            var result = await ViewsApi.PreviewRelationshipDdlAsync(ContentTypeAlias, relationshipId);
            switch (result)
            {
                case Result<RelationshipDdlPreviewResponse, AeroError>.Ok ok:
                    _relationshipPreview = ok.Value;
                    break;
                case Result<RelationshipDdlPreviewResponse, AeroError>.Failure failure:
                    _relationshipsError = FormatError(failure.Error);
                    break;
            }
        }
        finally
        {
            _relationshipMutating = false;
        }
    }

    private async Task AdoptRelationshipAsync(ContentRelationshipSummary relationship)
    {
        if (!_isConfigured || _relationshipMutating || !relationship.CanAdopt) return;
        _relationshipMutating = true;
        _relationshipsError = null;
        _relationshipPreview = null;
        try
        {
            var result = await ViewsApi.AdoptRelationshipAsync(
                ContentTypeAlias,
                relationship.Alias,
                relationship.SchemaFingerprint);
            switch (result)
            {
                case Result<ContentRelationshipSummary, AeroError>.Ok:
                    SetPageMessage("Relationship adopted from its reviewed schema fingerprint and locked for View use.", false);
                    await LoadRelationshipsAsync();
                    break;
                case Result<ContentRelationshipSummary, AeroError>.Failure failure:
                    _relationshipsError = FormatError(failure.Error);
                    break;
            }
        }
        finally
        {
            _relationshipMutating = false;
        }
    }

    private async Task SaveRelationshipDraftAsync(SaveContentRelationshipDraftCommand command)
    {
        if (!_isConfigured || _relationshipMutating) return;
        _relationshipMutating = true;
        _relationshipsError = null;
        _relationshipPreview = null;
        try
        {
            var result = await ViewsApi.SaveRelationshipDraftAsync(
                ContentTypeAlias,
                command.Alias,
                command.Request);
            switch (result)
            {
                case Result<ContentRelationshipSummary, AeroError>.Ok ok:
                    SetPageMessage("Relationship draft saved. Review its deterministic DDL preview before applying schema changes.", false);
                    await LoadRelationshipsAsync();
                    await PreviewRelationshipDdlAsync(ok.Value.Id);
                    break;
                case Result<ContentRelationshipSummary, AeroError>.Failure failure:
                    _relationshipsError = FormatError(failure.Error);
                    break;
            }
        }
        finally
        {
            _relationshipMutating = false;
        }
    }

    private async Task ApplyRelationshipDdlAsync(long relationshipId)
    {
        if (_relationshipPreview is not { } preview || preview.RelationshipId != relationshipId) return;
        _relationshipMutating = true;
        _relationshipsError = null;
        try
        {
            var result = await ViewsApi.ApplyRelationshipDdlAsync(
                ContentTypeAlias,
                relationshipId,
                preview.ProposedSchemaFingerprint);
            switch (result)
            {
                case Result<RelationshipDdlApplyResponse, AeroError>.Ok:
                    _relationshipPreview = null;
                    SetPageMessage("Relationship DDL applied and journaled. The relationship is now locked.", false);
                    await LoadRelationshipsAsync();
                    break;
                case Result<RelationshipDdlApplyResponse, AeroError>.Failure failure:
                    _relationshipsError = FormatError(failure.Error);
                    break;
            }
        }
        finally
        {
            _relationshipMutating = false;
        }
    }

    private void ApplySnapshot(ContentViewEditorSnapshot snapshot)
    {
        _isConfigured = true;
        _shapeAlias = snapshot.ShapeAlias;
        _selectStatement = snapshot.SelectStatement;
        _identityField = snapshot.IdentityField;
        _titleField = snapshot.TitleField;
        _entrySelectStatement = snapshot.EntrySelectStatement;
        _searchSelectStatement = snapshot.SearchSelectStatement;
        _revisionVersion = snapshot.Version;
        _publicationState = snapshot.PublicationState;
        _cacheEnabled = snapshot.CacheEnabled;
        _cacheDurationSeconds = Math.Clamp(snapshot.CacheDurationSeconds, 1, 86_400);
        _publicExecutionEligible = snapshot.PublicExecutionEligible;
        _publicExecutionIneligibilityReason = snapshot.PublicExecutionIneligibilityReason;
        _isDirty = false;
        _editorKey = $"{ContentTypeAlias}-{snapshot.Version}-{snapshot.PublicationState}";
    }

    private Task SetShapeAlias(string value)
    {
        _shapeAlias = value;
        MarkDirty();
        return Task.CompletedTask;
    }

    private Task SetSelectStatement(string value)
    {
        _selectStatement = value;
        MarkDirty();
        return Task.CompletedTask;
    }

    private Task SetIdentityField(string value)
    {
        _identityField = value;
        MarkDirty(clearPreview: false);
        return Task.CompletedTask;
    }

    private Task SetTitleField(string? value)
    {
        _titleField = string.IsNullOrWhiteSpace(value) ? null : value;
        MarkDirty(clearPreview: false);
        return Task.CompletedTask;
    }

    private Task SetEntrySelectStatement(string value)
    {
        _entrySelectStatement = value;
        MarkDirty(clearPreview: false);
        return Task.CompletedTask;
    }

    private Task SetSearchSelectStatement(string value)
    {
        _searchSelectStatement = value;
        MarkDirty(clearPreview: false);
        return Task.CompletedTask;
    }

    private Task SetCacheEnabled(bool value)
    {
        _cacheEnabled = value;
        MarkDirty(clearPreview: false);
        return Task.CompletedTask;
    }

    private Task SetCacheDurationSeconds(int value)
    {
        _cacheDurationSeconds = Math.Clamp(value, 1, 86_400);
        MarkDirty(clearPreview: false);
        return Task.CompletedTask;
    }

    private void MarkDirty(bool clearPreview = true)
    {
        _isDirty = true;
        if (clearPreview)
        {
            _preview = null;
            _previewError = null;
            _identityField = string.Empty;
            _titleField = null;
        }
        ClearPageMessage();
    }

    private void SetPageMessage(string message, bool isError)
    {
        _pageMessage = message;
        _pageMessageIsError = isError;
    }

    private void ClearPageMessage()
    {
        _pageMessage = null;
        _pageMessageIsError = false;
    }

    private static bool IsNotFound(AeroError error) => error switch
    {
        AeroError.NotFound => true,
        AeroError.HttpRequest { code: HttpStatusCode.NotFound } => true,
        _ => false
    };

    private static string FormatError(AeroError error) => error switch
    {
        AeroError.Validation validation => string.Join("; ", validation.Errors),
        AeroError.Error value => value.msg,
        AeroError.NotFound value => value.msg,
        AeroError.Conflict value => value.msg,
        AeroError.BadRequest value => value.msg,
        AeroError.InvalidRequest value => value.msg,
        AeroError.HttpRequest value => value.msg ?? $"Request failed with status {(int)value.code}.",
        _ => error.ToString()
    };

    public ValueTask DisposeAsync()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        return ValueTask.CompletedTask;
    }
}
