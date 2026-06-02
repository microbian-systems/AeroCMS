using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Radzen;
using CategoryCreateRequest = Aero.Cms.Abstractions.Http.Clients.CreateCategoryRequest;
using CategoryUpdateRequest = Aero.Cms.Abstractions.Http.Clients.UpdateCategoryRequest;
using SeriesCreateRequest = Aero.Cms.Abstractions.Http.Clients.CreateSeriesRequest;
using SeriesUpdateRequest = Aero.Cms.Abstractions.Http.Clients.UpdateSeriesRequest;
using TagCreateRequest = Aero.Cms.Abstractions.Http.Clients.CreateTagRequest;
using TagUpdateRequest = Aero.Cms.Abstractions.Http.Clients.UpdateTagRequest;

namespace Aero.Cms.Shared.Pages.Manager;

public partial class TaxonomyOverview : ComponentBase
{
    [SupplyParameterFromQuery(Name = "kind")]
    protected string? RequestedKind { get; set; }

    [Inject] protected ICategoriesHttpClient CategoriesClient { get; set; } = default!;
    [Inject] protected ITagsHttpClient TagsClient { get; set; } = default!;
    [Inject] protected ISeriesHttpClient SeriesClient { get; set; } = default!;
    [Inject] protected DialogService DialogService { get; set; } = default!;

    protected TaxonomyKind ActiveKind { get; set; } = TaxonomyKind.Categories;
    protected List<TaxonomyItem> Items { get; set; } = [];
    protected bool IsLoading { get; set; }
    protected bool IsSaving { get; set; }
    protected string? ErrorMessage { get; set; }
    protected long? EditingId { get; set; }
    protected string DraftName { get; set; } = string.Empty;
    protected string DraftSlug { get; set; } = string.Empty;
    protected string DraftDescription { get; set; } = string.Empty;
    protected List<SeriesTranslationSummary> SeriesTranslations { get; set; } = [];
    protected bool IsLoadingSeriesTranslations { get; set; }
    protected bool IsSavingSeriesTranslation { get; set; }
    protected string TranslationCulture { get; set; } = string.Empty;
    protected string TranslationName { get; set; } = string.Empty;
    protected string TranslationSlug { get; set; } = string.Empty;
    protected string TranslationDescription { get; set; } = string.Empty;

    protected IReadOnlyList<TaxonomyOption> TaxonomyOptions { get; } =
    [
        new(TaxonomyKind.Categories, "Categories", "Category", "category", "Organize posts into editorial buckets."),
        new(TaxonomyKind.Tags, "Tags", "Tag", "label", "Label posts with flexible keywords."),
        new(TaxonomyKind.Series, "Series", "Series", "view_list", "Group posts into one editorial sequence.")
    ];

    protected TaxonomyOption ActiveOption => TaxonomyOptions.First(x => x.Key == ActiveKind);

    protected override async Task OnInitializedAsync()
    {
        ActiveKind = ParseKind(RequestedKind);
        await LoadAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        var requested = ParseKind(RequestedKind);
        if (requested != ActiveKind)
        {
            ActiveKind = requested;
            ResetDraft();
            await LoadAsync();
        }
    }

    protected async Task SelectTaxonomyAsync(TaxonomyKind kind)
    {
        if (ActiveKind == kind)
            return;

        ActiveKind = kind;
        ResetDraft();
        await LoadAsync();
    }

    protected string TaxonomyButtonClass(TaxonomyKind kind)
        => ActiveKind == kind
            ? "pe-btn pe-btn-primary pe-btn-sm"
            : "pe-btn pe-btn-secondary pe-btn-sm";

    protected void StartCreate() => ResetDraft();

    protected async Task StartEdit(TaxonomyItem item)
    {
        EditingId = item.Id;
        DraftName = item.Name;
        DraftSlug = item.Slug;
        DraftDescription = item.Description ?? string.Empty;

        if (ActiveKind == TaxonomyKind.Series)
            await LoadSeriesTranslationsAsync(item.Id);
    }

    protected void ResetDraft()
    {
        EditingId = null;
        DraftName = string.Empty;
        DraftSlug = string.Empty;
        DraftDescription = string.Empty;
        ErrorMessage = null;
        SeriesTranslations = [];
        ResetTranslationDraft();
    }

    protected void OnNameChanged(string name)
    {
        DraftName = name;
        if (string.IsNullOrWhiteSpace(DraftSlug))
            DraftSlug = TitleToSlug(name);
    }

    protected void StartSeriesTranslation(SeriesTranslationSummary translation)
    {
        TranslationCulture = translation.Culture;
        TranslationName = translation.Name;
        TranslationSlug = translation.Slug;
        TranslationDescription = translation.Description ?? string.Empty;
    }

    protected void OnTranslationNameChanged(string name)
    {
        TranslationName = name;
        if (string.IsNullOrWhiteSpace(TranslationSlug))
            TranslationSlug = TitleToSlug(name);
    }

    protected async Task SaveSeriesTranslationAsync()
    {
        if (EditingId is null || string.IsNullOrWhiteSpace(TranslationCulture))
            return;

        if (string.IsNullOrWhiteSpace(TranslationName))
        {
            ErrorMessage = "Translation name is required.";
            return;
        }

        IsSavingSeriesTranslation = true;
        ErrorMessage = null;

        try
        {
            var slug = string.IsNullOrWhiteSpace(TranslationSlug) ? TitleToSlug(TranslationName) : TranslationSlug.Trim();
            var result = await SeriesClient.UpsertTranslationAsync(
                EditingId.Value,
                TranslationCulture,
                new UpsertSeriesTranslationRequest(TranslationName, slug, TranslationDescription));

            if (result is Result<SeriesTranslationSummary, AeroError>.Failure failure)
            {
                ErrorMessage = failure.Error.ToString();
                return;
            }

            await LoadSeriesTranslationsAsync(EditingId.Value);
            ResetTranslationDraft();
        }
        finally
        {
            IsSavingSeriesTranslation = false;
        }
    }

    protected async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            Items = ActiveKind switch
            {
                TaxonomyKind.Categories => await LoadCategoriesAsync(),
                TaxonomyKind.Tags => await LoadTagsAsync(),
                TaxonomyKind.Series => await LoadSeriesAsync(),
                _ => []
            };
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(DraftName))
        {
            ErrorMessage = "Name is required.";
            return;
        }

        IsSaving = true;
        ErrorMessage = null;

        try
        {
            var slug = string.IsNullOrWhiteSpace(DraftSlug) ? TitleToSlug(DraftName) : DraftSlug.Trim();
            var result = ActiveKind switch
            {
                TaxonomyKind.Categories => await SaveCategoryAsync(slug),
                TaxonomyKind.Tags => await SaveTagAsync(slug),
                TaxonomyKind.Series => await SaveSeriesAsync(slug),
                _ => Prelude.Fail<bool, AeroError>(AeroError.CreateError("Unknown taxonomy type"))
            };

            if (result is Result<bool, AeroError>.Failure failure)
            {
                ErrorMessage = failure.Error.ToString();
                return;
            }

            ResetDraft();
            await LoadAsync();
        }
        finally
        {
            IsSaving = false;
        }
    }

    protected async Task DeleteAsync(TaxonomyItem item)
    {
        var confirmed = await DialogService.Confirm(
            $"Delete '{item.Name}'?",
            "Delete Taxonomy Entry",
            new ConfirmOptions { OkButtonText = "Delete", CancelButtonText = "Cancel" });

        if (confirmed != true)
            return;

        var result = ActiveKind switch
        {
            TaxonomyKind.Categories => await CategoriesClient.DeleteAsync(item.Id),
            TaxonomyKind.Tags => await TagsClient.DeleteAsync(item.Id),
            TaxonomyKind.Series => await SeriesClient.DeleteAsync(item.Id),
            _ => Prelude.Fail<bool, AeroError>(AeroError.CreateError("Unknown taxonomy type"))
        };

        if (result is Result<bool, AeroError>.Failure failure)
        {
            ErrorMessage = failure.Error.ToString();
            return;
        }

        await LoadAsync();
    }

    private async Task<List<TaxonomyItem>> LoadCategoriesAsync()
    {
        var result = await CategoriesClient.GetAllAsync();
        return result is Result<IReadOnlyList<CategorySummary>, AeroError>.Ok ok
            ? ok.Value.Select(x => new TaxonomyItem(x.Id, x.Name, x.Slug, null, x.ContentCount)).ToList()
            : [];
    }

    private async Task<List<TaxonomyItem>> LoadTagsAsync()
    {
        var result = await TagsClient.GetAllAsync();
        return result is Result<IReadOnlyList<TagSummary>, AeroError>.Ok ok
            ? ok.Value.Select(x => new TaxonomyItem(x.Id, x.Name, x.Slug, null, x.ContentCount)).ToList()
            : [];
    }

    private async Task<List<TaxonomyItem>> LoadSeriesAsync()
    {
        var generalTask = SeriesClient.EnsureGeneralAsync();
        var result = await SeriesClient.GetAllAsync();
        await generalTask;

        var items = result is Result<IReadOnlyList<SeriesSummary>, AeroError>.Ok ok
            ? ok.Value.Select(x => new TaxonomyItem(x.Id, x.Name, x.Slug, x.Description, x.ContentCount)).ToList()
            : [];

        if (generalTask.Result is Result<SeriesDetail, AeroError>.Ok general
            && items.All(x => x.Id != general.Value.Id))
        {
            items.Insert(0, new TaxonomyItem(general.Value.Id, general.Value.Name, general.Value.Slug, general.Value.Description, general.Value.ContentCount));
        }

        return items;
    }

    private async Task LoadSeriesTranslationsAsync(long seriesId)
    {
        IsLoadingSeriesTranslations = true;
        SeriesTranslations = [];

        try
        {
            var result = await SeriesClient.ListTranslationsAsync(seriesId);
            if (result is Result<IReadOnlyList<SeriesTranslationSummary>, AeroError>.Ok ok)
                SeriesTranslations = ok.Value.ToList();
            else if (result is Result<IReadOnlyList<SeriesTranslationSummary>, AeroError>.Failure failure)
                ErrorMessage = failure.Error.ToString();
        }
        finally
        {
            IsLoadingSeriesTranslations = false;
        }
    }

    private async Task<Result<bool, AeroError>> SaveCategoryAsync(string slug)
    {
        if (EditingId is { } id)
        {
            var updated = await CategoriesClient.UpdateAsync(id, new CategoryUpdateRequest(DraftName, slug, DraftDescription, null));
            return updated is Result<CategoryDetail, AeroError>.Failure updateFailure ? updateFailure.Error : true;
        }

        var created = await CategoriesClient.CreateAsync(new CategoryCreateRequest(DraftName, slug, DraftDescription, null));
        return created is Result<CategoryDetail, AeroError>.Failure createFailure ? createFailure.Error : true;
    }

    private async Task<Result<bool, AeroError>> SaveTagAsync(string slug)
    {
        if (EditingId is { } id)
        {
            var updated = await TagsClient.UpdateAsync(id, new TagUpdateRequest(DraftName, slug, DraftDescription));
            return updated is Result<TagDetail, AeroError>.Failure updateFailure ? updateFailure.Error : true;
        }

        var created = await TagsClient.CreateAsync(new TagCreateRequest(DraftName, slug, DraftDescription));
        return created is Result<TagDetail, AeroError>.Failure createFailure ? createFailure.Error : true;
    }

    private async Task<Result<bool, AeroError>> SaveSeriesAsync(string slug)
    {
        if (EditingId is { } id)
        {
            var updated = await SeriesClient.UpdateAsync(id, new SeriesUpdateRequest(DraftName, slug, DraftDescription));
            return updated is Result<SeriesDetail, AeroError>.Failure updateFailure ? updateFailure.Error : true;
        }

        var created = await SeriesClient.CreateAsync(new SeriesCreateRequest(DraftName, slug, DraftDescription));
        return created is Result<SeriesDetail, AeroError>.Failure createFailure ? createFailure.Error : true;
    }

    private static TaxonomyKind ParseKind(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            "tag" or "tags" => TaxonomyKind.Tags,
            "series" => TaxonomyKind.Series,
            _ => TaxonomyKind.Categories
        };

    protected string FormatCulture(string culture)
    {
        try
        {
            var info = CultureInfo.GetCultureInfo(culture);
            return $"{info.DisplayName} ({info.Name})";
        }
        catch (CultureNotFoundException)
        {
            return culture;
        }
    }

    private void ResetTranslationDraft()
    {
        TranslationCulture = string.Empty;
        TranslationName = string.Empty;
        TranslationSlug = string.Empty;
        TranslationDescription = string.Empty;
    }

    private static string TitleToSlug(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        var normalized = title.Normalize(NormalizationForm.FormD);
        var filtered = normalized.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || c == '-');
        var slug = new string(filtered.ToArray())
            .Normalize(NormalizationForm.FormC)
            .ToLowerInvariant();

        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-");
        slug = Regex.Replace(slug, @"-{2,}", "-");
        return slug.Trim('-');
    }

    protected enum TaxonomyKind
    {
        Categories,
        Tags,
        Series
    }

    protected sealed record TaxonomyOption(TaxonomyKind Key, string Label, string SingularLabel, string Icon, string Description);

    protected sealed record TaxonomyItem(long Id, string Name, string Slug, string? Description, int ContentCount);
}
