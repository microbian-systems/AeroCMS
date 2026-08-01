using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Html;
using Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor;

/// <summary>
/// Contains the additive structured-content sidebar orchestration for <see cref="PageEditor"/>.
/// </summary>
public partial class PageEditor
{
    private const int ContentItemPickerPageSize = 10;

    [Inject]
    private IContentTypesHttpClient ContentTypesApi { get; set; } = default!;

    [Inject]
    private IContentItemsHttpClient ContentItemsApi { get; set; } = default!;

    protected HtmlPageEditorSidebarTab RightSidebarTab { get; private set; } =
        HtmlPageEditorSidebarTab.Elements;

    protected IReadOnlyList<ContentTypeSummary> ContentTypeOptions { get; private set; } = [];

    protected string? SelectedContentTypeAlias { get; private set; }

    protected ContentTypeDetail? SelectedContentType { get; private set; }

    protected IReadOnlyList<ContentItemSummary> ContentItemOptions { get; private set; } = [];

    protected long? SelectedContentItemId { get; private set; }

    protected string ContentItemSearchText { get; private set; } = string.Empty;

    protected int ContentItemSkip { get; private set; }

    protected int ContentItemTake => ContentItemPickerPageSize;

    protected long ContentItemTotalCount { get; private set; }

    protected bool IsContentPaletteLoading { get; private set; }

    protected bool IsContentItemsLoading { get; private set; }

    protected string? ContentPaletteError { get; private set; }

    protected string? ContentListSettingsError { get; private set; }

    private bool _contentTypesLoaded;
    private long _contentItemsRequestVersion;

    protected PageContentListScope? SelectedContentListScope =>
        HtmlEditor.SelectedNodeId is { } selectedNodeId
            ? (HtmlEditor.Composition.ContentLists ?? [])
                .FirstOrDefault(scope => scope.NodeId == selectedNodeId)
            : null;

    protected IReadOnlyList<ContentFieldDefinition> SelectedContentListFields =>
        SelectedContentListScope is { } scope
        && SelectedContentType is { } contentType
        && contentType.Id == scope.ContentTypeId
            ? contentType.Fields
            : [];

    /// <summary>
    /// Gets the typed-content sidecar paired with the current HTML draft.
    /// </summary>
    protected PageCompositionDocument DraftComposition => HtmlEditor.Composition;

    protected string RightSidebarTitle => RightSidebarTab switch
    {
        HtmlPageEditorSidebarTab.Document => L["Document"],
        HtmlPageEditorSidebarTab.Elements => L["Elements"],
        HtmlPageEditorSidebarTab.Content => L["Content"],
        HtmlPageEditorSidebarTab.Inspector => L["Inspector"],
        _ => L["Page editor"]
    };

    protected async Task SetRightSidebarTabAsync(HtmlPageEditorSidebarTab tab)
    {
        RightSidebarTab = tab;
        RightSidebarCollapsed = false;

        if (tab == HtmlPageEditorSidebarTab.Content)
        {
            await EnsureContentTypesLoadedAsync();
        }
    }

    protected Task RefreshContentTypesAsync() => EnsureContentTypesLoadedAsync(force: true);

    protected async Task SelectContentTypeAsync(string? alias)
    {
        SelectedContentTypeAlias = string.IsNullOrWhiteSpace(alias) ? null : alias;
        SelectedContentType = null;
        ContentItemOptions = [];
        SelectedContentItemId = null;
        ContentItemSearchText = string.Empty;
        ContentItemSkip = 0;
        ContentItemTotalCount = 0;
        ContentPaletteError = null;

        if (SelectedContentTypeAlias is null)
        {
            return;
        }

        IsContentPaletteLoading = true;
        try
        {
            var selectedAlias = SelectedContentTypeAlias;
            var result = await ContentTypesApi.GetByAliasAsync(selectedAlias);
            switch (result)
            {
                case Result<ContentTypeDetail, AeroError>.Ok ok
                    when string.Equals(SelectedContentTypeAlias, selectedAlias, StringComparison.OrdinalIgnoreCase):
                    SelectedContentType = ok.Value;
                    await LoadContentItemsAsync(selectedAlias, skip: 0);
                    break;
                case Result<ContentTypeDetail, AeroError>.Failure failure:
                    ContentPaletteError = FormatError(failure.Error);
                    break;
            }
        }
        finally
        {
            IsContentPaletteLoading = false;
        }
    }

    protected Task SelectContentItemAsync(long? contentItemId)
    {
        SelectedContentItemId = contentItemId is { } value
            && ContentItemOptions.Any(item => item.Id == value)
                ? value
                : null;
        return Task.CompletedTask;
    }

    protected async Task SearchContentItemsAsync(string searchText)
    {
        ContentItemSearchText = searchText.Trim();
        if (SelectedContentTypeAlias is { } alias)
        {
            await LoadContentItemsAsync(alias, skip: 0);
        }
    }

    protected async Task LoadPreviousContentItemsAsync()
    {
        if (SelectedContentTypeAlias is not { } alias || ContentItemSkip <= 0)
        {
            return;
        }

        await LoadContentItemsAsync(alias, Math.Max(0, ContentItemSkip - ContentItemPickerPageSize));
    }

    protected async Task LoadNextContentItemsAsync()
    {
        if (SelectedContentTypeAlias is not { } alias
            || ContentItemSkip + ContentItemPickerPageSize >= ContentItemTotalCount)
        {
            return;
        }

        await LoadContentItemsAsync(alias, ContentItemSkip + ContentItemPickerPageSize);
    }

    private async Task LoadContentItemsAsync(string alias, int skip)
    {
        var requestVersion = ++_contentItemsRequestVersion;
        IsContentItemsLoading = true;
        try
        {
            var result = await ContentItemsApi.GetAllAsync(
                alias,
                skip,
                ContentItemPickerPageSize,
                ContentItemSearchText);
            if (requestVersion != _contentItemsRequestVersion
                || !string.Equals(SelectedContentTypeAlias, alias, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            switch (result)
            {
                case Result<PagedResult<ContentItemSummary>, AeroError>.Ok ok:
                    ContentItemOptions = ok.Value.Items.ToArray();
                    ContentItemSkip = ok.Value.Skip;
                    ContentItemTotalCount = ok.Value.TotalCount;
                    SelectedContentItemId = ContentItemOptions.Any(item => item.Id == SelectedContentItemId)
                        ? SelectedContentItemId
                        : ContentItemOptions.FirstOrDefault()?.Id;
                    break;
                case Result<PagedResult<ContentItemSummary>, AeroError>.Failure failure:
                    ContentItemOptions = [];
                    SelectedContentItemId = null;
                    ContentItemSkip = 0;
                    ContentItemTotalCount = 0;
                    ContentPaletteError = FormatError(failure.Error);
                    break;
            }
        }
        finally
        {
            if (requestVersion == _contentItemsRequestVersion)
            {
                IsContentItemsLoading = false;
            }
        }
    }

    protected Task AddContentPaletteItemAsync(HtmlContentPaletteRequest request)
    {
        var result = ApplyContentPaletteRequest(request, null, null);
        HandleHtmlEditorResult(result, ContentPaletteSuccessMessage(request.ItemKind));
        return Task.CompletedTask;
    }

    private Result<HtmlNode> InsertContentPaletteItemRelative(
        HtmlContentPaletteRequest request,
        long targetNodeId,
        HtmlRelativePlacement placement) =>
        ApplyContentPaletteRequest(request, targetNodeId, placement);

    private Result<HtmlNode> ApplyContentPaletteRequest(
        HtmlContentPaletteRequest request,
        long? targetNodeId,
        HtmlRelativePlacement? placement)
    {
        var validation = ValidateContentPaletteRequest(request);
        if (validation is Result<HtmlContentPaletteRequest>.Failure failure)
        {
            return failure.Error;
        }

        var validated = ((Result<HtmlContentPaletteRequest>.Ok)validation).Value;
        return (validated.ItemKind, targetNodeId, placement) switch
        {
            (HtmlPaletteItemKind.ContentList, { } targetId, { } relativePlacement) =>
                HtmlEditor.AddContentListRelative(
                    validated.ContentTypeId,
                    validated.ContentTypeAlias,
                    targetId,
                    relativePlacement),
            (HtmlPaletteItemKind.ContentList, null, null) =>
                HtmlEditor.AddContentList(
                    validated.ContentTypeId,
                    validated.ContentTypeAlias),
            (HtmlPaletteItemKind.ContentItem, { } targetId, { } relativePlacement) =>
                HtmlEditor.AddContentItemRelative(
                    validated.ContentTypeId,
                    validated.ContentTypeAlias,
                    validated.ContentItemId!.Value,
                    validated.ContentItemSlug,
                    validated.ContentItemTitle,
                    targetId,
                    relativePlacement),
            (HtmlPaletteItemKind.ContentItem, null, null) =>
                HtmlEditor.AddContentItem(
                    validated.ContentTypeId,
                    validated.ContentTypeAlias,
                    validated.ContentItemId!.Value,
                    validated.ContentItemSlug,
                    validated.ContentItemTitle),
            (HtmlPaletteItemKind.ContentField, { } targetId, { } relativePlacement) =>
                HtmlEditor.AddContentFieldRelative(
                    validated.ContentTypeId,
                    validated.FieldName!,
                    validated.FieldType!,
                    validated.FieldLabel,
                    targetId,
                    relativePlacement),
            (HtmlPaletteItemKind.ContentField, null, null) =>
                HtmlEditor.AddContentField(
                    validated.ContentTypeId,
                    validated.FieldName!,
                    validated.FieldType!,
                    validated.FieldLabel),
            _ => AeroError.ValidationError(["The structured-content palette request is incomplete."])
        };
    }

    private Result<HtmlContentPaletteRequest> ValidateContentPaletteRequest(
        HtmlContentPaletteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (SelectedContentType is not { } selectedType
            || selectedType.Id <= 0
            || request.ContentTypeId != selectedType.Id
            || !string.Equals(
                request.ContentTypeAlias,
                selectedType.Alias,
                StringComparison.OrdinalIgnoreCase))
        {
            return AeroError.ValidationError(
                ["The dragged item no longer matches the selected content type. Select the type and try again."]);
        }

        return request.ItemKind switch
        {
            HtmlPaletteItemKind.ContentList => request with
            {
                ContentTypeAlias = selectedType.Alias
            },
            HtmlPaletteItemKind.ContentItem => ValidateContentItemRequest(request, selectedType),
            HtmlPaletteItemKind.ContentField => ValidateContentFieldRequest(request, selectedType),
            _ => AeroError.ValidationError(["The structured-content palette item is not supported."])
        };
    }

    private Result<HtmlContentPaletteRequest> ValidateContentItemRequest(
        HtmlContentPaletteRequest request,
        ContentTypeDetail selectedType)
    {
        var item = request.ContentItemId is { } itemId
            ? ContentItemOptions.FirstOrDefault(option =>
                option.Id == itemId
                && string.Equals(
                    option.ContentTypeAlias,
                    selectedType.Alias,
                    StringComparison.OrdinalIgnoreCase))
            : null;
        return item is null
            ? AeroError.ValidationError(
                ["Select an available content item before adding an item scope."])
            : request with
            {
                ContentTypeAlias = selectedType.Alias,
                ContentItemId = item.Id,
                ContentItemSlug = item.Slug,
                ContentItemTitle = item.Title
            };
    }

    private static Result<HtmlContentPaletteRequest> ValidateContentFieldRequest(
        HtmlContentPaletteRequest request,
        ContentTypeDetail selectedType)
    {
        var field = selectedType.Fields.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, request.FieldName, StringComparison.OrdinalIgnoreCase));
        return field is null
            ? AeroError.ValidationError(
                ["The selected field no longer exists on this content type."])
            : request with
            {
                ContentTypeAlias = selectedType.Alias,
                FieldName = field.Name,
                FieldType = field.FieldType,
                FieldLabel = field.Label
            };
    }

    private static string ContentPaletteSuccessMessage(HtmlPaletteItemKind itemKind) => itemKind switch
    {
        HtmlPaletteItemKind.ContentList => "Content list added.",
        HtmlPaletteItemKind.ContentItem => "Content item added.",
        HtmlPaletteItemKind.ContentField => "Content field added.",
        _ => "Content added."
    };

    protected async Task EnsureSelectedContentScopeMetadataAsync()
    {
        ContentListSettingsError = null;
        if (SelectedContentListScope is not { } scope
            || SelectedContentType?.Id == scope.ContentTypeId)
        {
            return;
        }

        await SelectContentTypeAsync(scope.ContentTypeAlias);
        if (SelectedContentType?.Id != scope.ContentTypeId)
        {
            ContentListSettingsError = ContentPaletteError
                ?? "The content-type fields for this list could not be loaded.";
        }
    }

    protected Task UpdateContentListSettingsAsync(HtmlContentListSettingsRequest request)
    {
        var validation = ValidateContentListSettingsRequest(request);
        if (validation is Result<HtmlContentListSettingsRequest>.Failure validationFailure)
        {
            ContentListSettingsError = FormatError(validationFailure.Error);
            ShowToast(ContentListSettingsError, "error");
            return Task.CompletedTask;
        }

        var validated = ((Result<HtmlContentListSettingsRequest>.Ok)validation).Value;
        var result = HtmlEditor.UpdateContentListSettings(
            validated.ScopeNodeId,
            validated.Query,
            validated.EmptyState);
        switch (result)
        {
            case Result<PageContentListScope>.Ok:
                ContentListSettingsError = null;
                MarkDirty();
                ShowToast(L["Content list settings updated."], "success");
                break;
            case Result<PageContentListScope>.Failure failure:
                ContentListSettingsError = FormatError(failure.Error);
                ShowToast(ContentListSettingsError, "error");
                break;
        }

        return Task.CompletedTask;
    }

    private Result<HtmlContentListSettingsRequest> ValidateContentListSettingsRequest(
        HtmlContentListSettingsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (SelectedContentListScope is not { } scope
            || scope.NodeId != request.ScopeNodeId)
        {
            return AeroError.ValidationError(
                ["The selected content-list scope changed. Select it and try again."]);
        }

        if (SelectedContentType is not { } contentType || contentType.Id != scope.ContentTypeId)
        {
            return AeroError.ValidationError(
                ["The content-type fields for this list are not available. Reload the scope and try again."]);
        }

        var knownFields = contentType.Fields.ToDictionary(
            field => field.Name,
            StringComparer.OrdinalIgnoreCase);
        string? normalizedSortField = null;
        if (!string.IsNullOrWhiteSpace(request.Query.SortField))
        {
            if (!knownFields.TryGetValue(request.Query.SortField, out var sortField))
            {
                return AeroError.ValidationError(
                    ["The selected sort field no longer exists on this content type."]);
            }

            normalizedSortField = sortField.Name;
        }

        var normalizedFilters = new List<PageContentFilter>();
        foreach (var filter in request.Query.Filters ?? [])
        {
            if (!knownFields.TryGetValue(filter.FieldName, out var field))
            {
                return AeroError.ValidationError(
                    [$"The filter field '{filter.FieldName}' no longer exists on this content type."]);
            }

            normalizedFilters.Add(filter with { FieldName = field.Name });
        }

        return request with
        {
            Query = request.Query with
            {
                SortField = normalizedSortField,
                Filters = normalizedFilters.ToArray()
            }
        };
    }

    private async Task EnsureContentTypesLoadedAsync(bool force = false)
    {
        if (_contentTypesLoaded && !force)
        {
            return;
        }

        IsContentPaletteLoading = true;
        ContentPaletteError = null;
        try
        {
            var result = await ContentTypesApi.GetAllAsync();
            switch (result)
            {
                case Result<IReadOnlyList<ContentTypeSummary>, AeroError>.Ok ok:
                {
                    ContentTypeOptions = ok.Value
                        .OrderBy(type => type.Name, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    _contentTypesLoaded = true;

                    var selectedAlias = ContentTypeOptions.Any(type =>
                        string.Equals(type.Alias, SelectedContentTypeAlias, StringComparison.OrdinalIgnoreCase))
                            ? SelectedContentTypeAlias
                            : ContentTypeOptions.FirstOrDefault()?.Alias;

                    IsContentPaletteLoading = false;
                    await SelectContentTypeAsync(selectedAlias);
                    break;
                }
                case Result<IReadOnlyList<ContentTypeSummary>, AeroError>.Failure failure:
                    ContentTypeOptions = [];
                    SelectedContentTypeAlias = null;
                    SelectedContentType = null;
                    ContentItemOptions = [];
                    SelectedContentItemId = null;
                    ContentItemSearchText = string.Empty;
                    ContentItemSkip = 0;
                    ContentItemTotalCount = 0;
                    ContentPaletteError = FormatError(failure.Error);
                    break;
            }
        }
        finally
        {
            IsContentPaletteLoading = false;
        }
    }
}
