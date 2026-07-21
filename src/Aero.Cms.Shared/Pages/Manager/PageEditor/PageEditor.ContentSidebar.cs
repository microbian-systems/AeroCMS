using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Pages.Composition;
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
    [Inject]
    private IContentTypesHttpClient ContentTypesApi { get; set; } = default!;

    protected HtmlPageEditorSidebarTab RightSidebarTab { get; private set; } =
        HtmlPageEditorSidebarTab.Elements;

    protected IReadOnlyList<ContentTypeSummary> ContentTypeOptions { get; private set; } = [];

    protected string? SelectedContentTypeAlias { get; private set; }

    protected ContentTypeDetail? SelectedContentType { get; private set; }

    protected bool IsContentPaletteLoading { get; private set; }

    protected string? ContentPaletteError { get; private set; }

    private bool _contentTypesLoaded;

    /// <summary>
    /// Gets the typed-content sidecar paired with the current HTML draft.
    /// </summary>
    protected PageCompositionDocument DraftComposition { get; private set; } = new();

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
