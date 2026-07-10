using System.Globalization;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;

namespace Aero.Cms.Shared.Pages.Manager.DocsEditor;

/// <summary>
/// Represents a class for DocsEditor.
/// </summary>
public partial class DocsEditor
{
        /// <summary>
    /// Gets or sets the Space Id.
    /// </summary>
[Parameter] public long SpaceId { get; set; }
        /// <summary>
    /// Gets or sets the Section Id.
    /// </summary>
[Parameter] public long? SectionId { get; set; }

    [Inject] private IDocsHttpClient DocsClient { get; set; } = default!;
    [Inject] private ISitesHttpClient SitesClient { get; set; } = default!;
    [Inject] private ICurrentSiteAccessor CurrentSiteAccessor { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private DialogService DialogService { get; set; } = default!;
    [Inject] private IStringLocalizer<Aero.Cms.Shared.Localization.ManagerResource> L { get; set; } = default!;

        /// <summary>
    /// Gets or sets the Space.
    /// </summary>
protected DocsDetail? Space { get; private set; }
        /// <summary>
    /// Gets or sets the Current.
    /// </summary>
protected DocsDetail? Current { get; private set; }
        /// <summary>
    /// Gets or sets the Outline.
    /// </summary>
protected IReadOnlyList<OutlineNode> Outline { get; private set; } = [];
        /// <summary>
    /// Gets or sets the Visible Outline.
    /// </summary>
protected IReadOnlyList<OutlineNode> VisibleOutline => GetVisibleOutline();
        /// <summary>
    /// Gets or sets the Parent Options.
    /// </summary>
protected IReadOnlyList<ParentOption> ParentOptions { get; private set; } = [];
        /// <summary>
    /// Gets or sets the Selected Node.
    /// </summary>
protected OutlineNode? SelectedNode { get; private set; }
        /// <summary>
    /// Gets or sets the Active Tab.
    /// </summary>
protected string ActiveTab { get; set; } = "content";
        /// <summary>
    /// Gets or sets the Preview Mode.
    /// </summary>
protected bool PreviewMode { get; set; }
        /// <summary>
    /// Gets or sets the Has Unpublished Changes.
    /// </summary>
protected bool HasUnpublishedChanges { get; private set; }
        /// <summary>
    /// Gets or sets the Is Editing Space Root.
    /// </summary>
protected bool IsEditingSpaceRoot => Current?.Id == Space?.Id;
        /// <summary>
    /// Gets or sets the Parent Select Value.
    /// </summary>
protected string ParentSelectValue => Current?.ParentId?.ToString() ?? string.Empty;
        /// <summary>
    /// Gets or sets the Current Site.
    /// </summary>
protected SiteViewModel? CurrentSite { get; private set; }
        /// <summary>
    /// Gets or sets the Doc Culture Variants.
    /// </summary>
protected IReadOnlyList<DocsDetail> DocCultureVariants { get; private set; } = [];
        /// <summary>
    /// Gets or sets the Selected Translation Culture.
    /// </summary>
protected string SelectedTranslationCulture { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Translation Slug.
    /// </summary>
protected string TranslationSlug { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Is Loading Translations.
    /// </summary>
protected bool IsLoadingTranslations { get; private set; }
        /// <summary>
    /// Gets or sets the Is Creating Translation.
    /// </summary>
protected bool IsCreatingTranslation { get; private set; }
        /// <summary>
    /// Gets or sets the Supported Cultures.
    /// </summary>
protected IReadOnlyList<string> SupportedCultures =>
        CurrentSite?.SupportedCultures is { Count: > 0 } cultures
            ? cultures
            : [Current?.Culture ?? Space?.Culture ?? CurrentSite?.DefaultCulture ?? "en-US"];

        /// <summary>
    /// Gets or sets the Available Translation Cultures.
    /// </summary>
protected IEnumerable<string> AvailableTranslationCultures =>
        SupportedCultures
            .Select(NormalizeCultureName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(culture => !DocCultureVariants.Any(variant =>
                string.Equals(variant.Culture, culture, StringComparison.OrdinalIgnoreCase)))
            .ToList();

    private IReadOnlyList<DocsSummary> _allDocs = [];
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _dirty;
    private bool _multiSelect;
    private string _outlineSearch = string.Empty;
    private long? _loadedParentId;
    private readonly HashSet<long> _selectedIds = [];

        /// <summary>
    /// OnParametersSetAsync method.
    /// </summary>
protected override async Task OnParametersSetAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        try
        {
            CurrentSite ??= await ResolveCurrentSiteAsync();
            var allResult = await DocsClient.GetAllAsync();
            if (allResult is Result<IReadOnlyList<DocsSummary>, AeroError>.Ok allOk)
            {
                _allDocs = allOk.Value;
            }
            else if (allResult is Result<IReadOnlyList<DocsSummary>, AeroError>.Failure allFailure)
            {
                NotifyError("Failed to load docs", allFailure.Error.ToString());
                _allDocs = [];
            }

            Space = await LoadDetailAsync(SpaceId);
            if (Space is null)
            {
                return;
            }

            _allDocs = _allDocs
                .Where(doc => string.Equals(doc.Culture, Space.Culture, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var selectedId = SectionId ?? SpaceId;
            Current = await LoadDetailAsync(selectedId);
            Current ??= Space;

            Outline = BuildOutline(Space);
            SelectedNode = Outline.FirstOrDefault(node => node.Id == Current.Id);
            ParentOptions = BuildParentOptions();
            HasUnpublishedChanges = Current.DraftVersion > Current.PublishedVersion;
            _loadedParentId = Current.ParentId;
            _dirty = false;
            await LoadDocTranslationsAsync();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task<DocsDetail?> LoadDetailAsync(long id)
    {
        var result = await DocsClient.GetByIdAsync(id);
        if (result is Result<DocsDetail, AeroError>.Ok ok)
        {
            return ok.Value;
        }

        if (result is Result<DocsDetail, AeroError>.Failure failure)
        {
            NotifyError("Failed to load doc", failure.Error.ToString());
        }

        return null;
    }

    private IReadOnlyList<OutlineNode> BuildOutline(DocsDetail space)
    {
        var rows = new List<OutlineNode>();
        var childrenByParent = _allDocs
            .Where(doc => doc.Id == space.Id || IsUnderSpace(doc, space))
            .GroupBy(doc => doc.ParentId)
            .ToDictionary(group => group.Key, group => group.OrderBy(doc => doc.Order).ThenBy(doc => doc.Title).ToList());

        AddNode(space.Id, depth: 0);
        return rows;

        void AddNode(long id, int depth)
        {
            var source = id == space.Id
                ? ToSummary(space)
                : _allDocs.FirstOrDefault(doc => doc.Id == id);

            if (source is null)
            {
                return;
            }

            var children = childrenByParent.TryGetValue(id, out var list) ? list : [];
            rows.Add(new OutlineNode(source.Id, source.Title, source.Slug, depth, children.Count));

            foreach (var child in children)
            {
                AddNode(child.Id, depth + 1);
            }
        }
    }

    private bool IsUnderSpace(DocsSummary doc, DocsDetail space)
    {
        if (doc.ParentId == space.Id)
        {
            return true;
        }

        var parentId = doc.ParentId;
        while (parentId is { } id)
        {
            if (id == space.Id)
            {
                return true;
            }

            parentId = _allDocs.FirstOrDefault(item => item.Id == id)?.ParentId;
        }

        return false;
    }

    private IReadOnlyList<ParentOption> BuildParentOptions()
    {
        if (Space is null)
        {
            return [];
        }

        var excluded = Current is null ? new HashSet<long>() : GetDescendantIds(Current.Id);
        if (Current is not null)
        {
            excluded.Add(Current.Id);
        }

        return Outline
            .Where(node => !excluded.Contains(node.Id))
            .Select(node => new ParentOption(node.Id.ToString(), $"{new string(' ', node.Depth * 2)}{node.Title}"))
            .ToList();
    }

    private IReadOnlyList<OutlineNode> GetVisibleOutline()
    {
        if (string.IsNullOrWhiteSpace(_outlineSearch))
        {
            return Outline;
        }

        var matchingIds = Outline
            .Where(node => MatchesOutlineSearch(node, _outlineSearch))
            .Select(node => node.Id)
            .ToHashSet();

        return Outline
            .Where(node => matchingIds.Contains(node.Id) || GetDescendantIds(node.Id).Any(matchingIds.Contains))
            .ToList();
    }

    private static bool MatchesOutlineSearch(OutlineNode node, string search)
        => node.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
           || node.Slug.Contains(search, StringComparison.OrdinalIgnoreCase);

    private HashSet<long> GetDescendantIds(long parentId)
    {
        var ids = new HashSet<long>();
        AddChildren(parentId);
        return ids;

        void AddChildren(long id)
        {
            foreach (var child in _allDocs.Where(doc => doc.ParentId == id))
            {
                if (ids.Add(child.Id))
                {
                    AddChildren(child.Id);
                }
            }
        }
    }

    private void SelectNode(OutlineNode node)
    {
        if (node.Id == SpaceId)
        {
            Navigation.NavigateTo($"/manager/docs/{SpaceId}");
            return;
        }

        Navigation.NavigateTo($"/manager/docs/{SpaceId}/sections/{node.Id}");
    }

    private async Task CreateChildAsync()
    {
        await CreateChildAsync(Current?.Id ?? SpaceId);
    }

    private async Task CreateChildAsync(long parentId)
    {
        var parent = parentId == Space?.Id
            ? Space
            : await LoadDetailAsync(parentId);

        if (parent is null)
        {
            NotifyError("Missing parent", "Select a section before creating a child.");
            return;
        }

        var title = parent.Id == SpaceId ? "New section" : "New child section";
        var result = await DocsClient.CreateChildAsync(SpaceId, parent.Id, new DocsCreateChildRequest(title));
        if (result is Result<DocsDetail, AeroError>.Ok ok)
        {
            NotificationService.Notify(NotificationSeverity.Success, "Section created", ok.Value.Title);
            Navigation.NavigateTo($"/manager/docs/{SpaceId}/sections/{ok.Value.Id}");
            return;
        }

        if (result is Result<DocsDetail, AeroError>.Failure failure)
        {
            NotifyError("Create failed", failure.Error.ToString());
        }
    }

    private async Task SaveCurrentAsync()
    {
        if (Current is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Current.Title) || string.IsNullOrWhiteSpace(Current.Slug))
        {
            NotifyError("Missing fields", "Title and slug are required.");
            return;
        }

        _isSaving = true;
        try
        {
            var page = Current with { Slug = NormalizeSlug(Current.Slug) };
            if (!IsEditingSpaceRoot && page.ParentId != _loadedParentId)
            {
                if (page.ParentId is null)
                {
                    NotifyError("Missing parent", "Sections must stay inside the current docs space.");
                    return;
                }

                var moveResult = await DocsClient.MoveAsync(SpaceId, page.Id, new DocsMoveRequest(page.ParentId.Value, page.Order));
                if (moveResult is Result<DocsDetail, AeroError>.Ok moved)
                {
                    page = page with { ParentId = moved.Value.ParentId, Slug = moved.Value.Slug, Order = moved.Value.Order };
                }
                else if (moveResult is Result<DocsDetail, AeroError>.Failure moveFailure)
                {
                    NotifyError("Move failed", moveFailure.Error.ToString());
                    return;
                }
            }

            var result = await DocsClient.SaveAsync(page);
            if (result is Result<DocsDetail, AeroError>.Ok ok)
            {
                Current = ok.Value;
                NotificationService.Notify(NotificationSeverity.Success, "Saved", ok.Value.Title);
                await LoadAsync();
                return;
            }

            if (result is Result<DocsDetail, AeroError>.Failure failure)
            {
                NotifyError("Save failed", failure.Error.ToString());
            }
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task LoadDocTranslationsAsync()
    {
        if (Current is null)
        {
            DocCultureVariants = [];
            ResetTranslationDraft();
            return;
        }

        IsLoadingTranslations = true;
        try
        {
            var result = await DocsClient.ListCultureVariantsAsync(Current.Id);
            DocCultureVariants = result is Result<IReadOnlyList<DocsDetail>, AeroError>.Ok ok
                ? ok.Value.OrderBy(doc => doc.Culture, StringComparer.OrdinalIgnoreCase).ToList()
                : [];

            ResetTranslationDraft();
        }
        finally
        {
            IsLoadingTranslations = false;
        }
    }

        /// <summary>
    /// CreateTranslationAsync method.
    /// </summary>
protected async Task CreateTranslationAsync()
    {
        if (Current is null || IsCreatingTranslation)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedTranslationCulture))
        {
            NotifyError("Choose a target culture", "Select a supported site culture before creating a translation.");
            return;
        }

        var slug = string.IsNullOrWhiteSpace(TranslationSlug)
            ? Current.Slug
            : TranslationSlug.Trim();

        if (string.IsNullOrWhiteSpace(slug))
        {
            NotifyError("Enter a translated slug", "Docs translations need a culture-specific slug.");
            return;
        }

        IsCreatingTranslation = true;
        try
        {
            var result = await DocsClient.ForkToCultureAsync(Current.Id, new ForkDocsCultureRequest(SelectedTranslationCulture, NormalizeSlug(slug)));
            if (result is Result<DocsDetail, AeroError>.Ok ok)
            {
                NotificationService.Notify(NotificationSeverity.Success, $"Created {FormatCulture(ok.Value.Culture)} translation", ok.Value.Title);
                if (Current?.Id == Space?.Id)
                {
                    Navigation.NavigateTo($"/manager/docs/{ok.Value.Id}");
                }
                else
                {
                    Navigation.NavigateTo($"/manager/docs/{SpaceId}/sections/{ok.Value.Id}");
                }
                return;
            }

            if (result is Result<DocsDetail, AeroError>.Failure failure)
            {
                NotifyError("Translation failed", failure.Error.ToString());
            }
        }
        finally
        {
            IsCreatingTranslation = false;
        }
    }

        /// <summary>
    /// OpenTranslation method.
    /// </summary>
protected void OpenTranslation(long docId)
        => Navigation.NavigateTo($"/manager/docs/{SpaceId}/sections/{docId}");

    private async Task PublishCurrentAsync()
    {
        if (Current is null)
        {
            return;
        }

        if (_dirty)
        {
            await SaveCurrentAsync();
        }

        var result = await DocsClient.PublishAsync(Current.Id);
        if (result is Result<DocsDetail, AeroError>.Ok ok)
        {
            Current = ok.Value;
            NotificationService.Notify(NotificationSeverity.Success, "Published", ok.Value.Title);
            await LoadAsync();
            return;
        }

        if (result is Result<DocsDetail, AeroError>.Failure failure)
        {
            NotifyError("Publish failed", failure.Error.ToString());
        }
    }

    private async Task UnpublishCurrentAsync()
    {
        if (Current is null)
        {
            return;
        }

        var result = await DocsClient.UnpublishAsync(Current.Id);
        if (result is Result<DocsDetail, AeroError>.Ok ok)
        {
            Current = ok.Value;
            NotificationService.Notify(NotificationSeverity.Success, "Unpublished", ok.Value.Title);
            await LoadAsync();
            return;
        }

        if (result is Result<DocsDetail, AeroError>.Failure failure)
        {
            NotifyError("Unpublish failed", failure.Error.ToString());
        }
    }

    private async Task DuplicateCurrentAsync()
    {
        if (Current is null)
        {
            return;
        }

        await DuplicateDocAsync(Current);
    }

    private async Task DuplicateDocAsync(DocsDetail source)
    {
        var copy = DocsDetail.Create(
            $"{source.Title} copy",
            GenerateUniqueChildSlug(GetParentSlug(source), $"{SlugLeaf(source.Slug)} copy"),
            source.ParentId,
            source.Summary,
            ContentPublicationState.Draft) with
            {
                MarkdownContent = source.MarkdownContent,
                SeoTitle = source.SeoTitle,
                SeoDescription = source.SeoDescription,
                ShowHeaderNavigation = source.ShowHeaderNavigation,
                HeaderImageUrl = source.HeaderImageUrl,
                Order = source.Order + 1,
                Culture = source.Culture
            };

        var result = await DocsClient.SaveAsync(copy);
        if (result is Result<DocsDetail, AeroError>.Ok ok)
        {
            NotificationService.Notify(NotificationSeverity.Success, "Duplicated", ok.Value.Title);
            Navigation.NavigateTo($"/manager/docs/{SpaceId}/sections/{ok.Value.Id}");
            return;
        }

        if (result is Result<DocsDetail, AeroError>.Failure failure)
        {
            NotifyError("Duplicate failed", failure.Error.ToString());
        }
    }

    private async Task DeleteCurrentAsync()
    {
        if (Current is null || IsEditingSpaceRoot)
        {
            return;
        }

        var confirmed = await DialogService.Confirm(
            $"Delete '{Current.Title}' and any child sections?",
            "Delete section",
            new ConfirmOptions { OkButtonText = "Delete", CancelButtonText = "Cancel" });

        if (confirmed != true)
        {
            return;
        }

        var result = await DocsClient.DeleteAsync(Current.Id);
        if (result is Result<bool, AeroError>.Ok)
        {
            NotificationService.Notify(NotificationSeverity.Success, "Deleted", Current.Title);
            Navigation.NavigateTo($"/manager/docs/{SpaceId}");
            return;
        }

        if (result is Result<bool, AeroError>.Failure failure)
        {
            NotifyError("Delete failed", failure.Error.ToString());
        }
    }

    private void EditNodeAttributes(OutlineNode node)
    {
        SelectNode(node);
        ActiveTab = "attributes";
        PreviewMode = false;
    }

    private async Task DuplicateNodeAsync(long nodeId)
    {
        if (nodeId == SpaceId)
        {
            return;
        }

        var detail = await LoadDetailAsync(nodeId);
        if (detail is null)
        {
            return;
        }

        await DuplicateDocAsync(detail);
    }

    private async Task PublishNodeAsync(long nodeId)
    {
        var result = await DocsClient.PublishAsync(nodeId);
        if (result is Result<DocsDetail, AeroError>.Ok ok)
        {
            NotificationService.Notify(NotificationSeverity.Success, "Published", ok.Value.Title);
            await LoadAsync();
            return;
        }

        if (result is Result<DocsDetail, AeroError>.Failure failure)
        {
            NotifyError("Publish failed", failure.Error.ToString());
        }
    }

    private async Task UnpublishNodeAsync(long nodeId)
    {
        var result = await DocsClient.UnpublishAsync(nodeId);
        if (result is Result<DocsDetail, AeroError>.Ok ok)
        {
            NotificationService.Notify(NotificationSeverity.Success, "Unpublished", ok.Value.Title);
            await LoadAsync();
            return;
        }

        if (result is Result<DocsDetail, AeroError>.Failure failure)
        {
            NotifyError("Unpublish failed", failure.Error.ToString());
        }
    }

    private async Task DeleteNodeAsync(long nodeId)
    {
        if (nodeId == SpaceId)
        {
            return;
        }

        var detail = await LoadDetailAsync(nodeId);
        if (detail is null)
        {
            return;
        }

        var confirmed = await DialogService.Confirm(
            $"Delete '{detail.Title}' and any child sections?",
            "Delete section",
            new ConfirmOptions { OkButtonText = "Delete", CancelButtonText = "Cancel" });

        if (confirmed != true)
        {
            return;
        }

        var result = await DocsClient.DeleteAsync(nodeId);
        if (result is Result<bool, AeroError>.Ok)
        {
            NotificationService.Notify(NotificationSeverity.Success, "Deleted", detail.Title);
            if (nodeId == Current?.Id)
            {
                Navigation.NavigateTo($"/manager/docs/{SpaceId}");
            }

            await LoadAsync();
            return;
        }

        if (result is Result<bool, AeroError>.Failure failure)
        {
            NotifyError("Delete failed", failure.Error.ToString());
        }
    }

    private async Task PublishSelectedAsync()
        => await ApplySelectedAsync(PublishNodeAsync);

    private async Task UnpublishSelectedAsync()
        => await ApplySelectedAsync(UnpublishNodeAsync);

    private async Task DeleteSelectedAsync()
    {
        var ids = _selectedIds.Where(id => id != SpaceId).ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var confirmed = await DialogService.Confirm(
            $"Delete {ids.Count} selected section(s)?",
            "Delete selected sections",
            new ConfirmOptions { OkButtonText = "Delete", CancelButtonText = "Cancel" });

        if (confirmed != true)
        {
            return;
        }

        foreach (var id in ids)
        {
            var result = await DocsClient.DeleteAsync(id);
            if (result is Result<bool, AeroError>.Failure failure)
            {
                NotifyError("Delete failed", failure.Error.ToString());
                break;
            }
        }

        _selectedIds.Clear();
        Navigation.NavigateTo($"/manager/docs/{SpaceId}");
        await LoadAsync();
    }

    private async Task ApplySelectedAsync(Func<long, Task> action)
    {
        var ids = _selectedIds.Where(id => id != SpaceId).ToList();
        foreach (var id in ids)
        {
            await action(id);
        }

        _selectedIds.Clear();
    }

    private void BackToSpaces()
    {
        Navigation.NavigateTo("/manager/docs");
    }

    private void TogglePreview()
    {
        PreviewMode = !PreviewMode;
        ActiveTab = PreviewMode ? "preview" : "content";
    }

    private void ToggleMultiSelect(ChangeEventArgs args)
    {
        _multiSelect = args.Value is true;
        if (!_multiSelect)
        {
            _selectedIds.Clear();
        }
    }

    private void ToggleNodeSelection(long nodeId, ChangeEventArgs args)
    {
        if (nodeId == SpaceId)
        {
            return;
        }

        if (args.Value is true)
        {
            _selectedIds.Add(nodeId);
        }
        else
        {
            _selectedIds.Remove(nodeId);
        }
    }

    private void OnOutlineSearchChanged(ChangeEventArgs args)
    {
        _outlineSearch = args.Value?.ToString() ?? string.Empty;
    }

    private void OnTitleChanged(ChangeEventArgs args)
    {
        UpdateCurrent(current => current.Title = args.Value?.ToString() ?? string.Empty);
    }

    private void OnSlugChanged(ChangeEventArgs args)
    {
        UpdateCurrent(current => current.Slug = args.Value?.ToString() ?? string.Empty);
    }

    private void OnMarkdownChanged(ChangeEventArgs args)
    {
        UpdateCurrent(current => current.MarkdownContent = args.Value?.ToString());
    }

    private void OnOrderChanged(ChangeEventArgs args)
    {
        UpdateCurrent(current =>
        {
            current.Order = int.TryParse(args.Value?.ToString(), out var order) ? order : 0;
        });
    }

    private void OnShowHeaderNavigationChanged(ChangeEventArgs args)
    {
        UpdateCurrent(current => current.ShowHeaderNavigation = args.Value is true);
    }

    private void OnStatusChanged(ChangeEventArgs args)
    {
        if (Enum.TryParse<ContentPublicationState>(args.Value?.ToString(), out var state))
        {
            UpdateCurrent(current =>
            {
                current.PublicationState = state;
                current.PublishedOn = state == ContentPublicationState.Published
                    ? current.PublishedOn ?? DateTimeOffset.UtcNow
                    : null;
            });
        }
    }

    private void OnParentChanged(ChangeEventArgs args)
    {
        if (Current is null || !long.TryParse(args.Value?.ToString(), out var parentId))
        {
            return;
        }

        var parentSlug = GetParentSlug(parentId);
        var leaf = SlugLeaf(Current.Slug);
        UpdateCurrent(current =>
        {
            current.ParentId = parentId;
            current.Slug = GenerateUniqueChildSlug(parentSlug, leaf, Current.Id);
        });
        ParentOptions = BuildParentOptions();
    }

    private async Task MoveNodeAsync(long nodeId, int direction)
    {
        if (nodeId == SpaceId)
        {
            return;
        }

        var node = _allDocs.FirstOrDefault(doc => doc.Id == nodeId);
        if (node is null)
        {
            return;
        }

        var siblings = _allDocs
            .Where(doc => doc.ParentId == node.ParentId)
            .OrderBy(doc => doc.Order)
            .ThenBy(doc => doc.Title)
            .ToList();

        var currentIndex = siblings.FindIndex(doc => doc.Id == nodeId);
        var nextIndex = currentIndex + direction;
        if (currentIndex < 0 || nextIndex < 0 || nextIndex >= siblings.Count)
        {
            return;
        }

        (siblings[currentIndex], siblings[nextIndex]) = (siblings[nextIndex], siblings[currentIndex]);
        await SaveSiblingOrderAsync(siblings);
    }

    private async Task SaveSiblingOrderAsync(IReadOnlyList<DocsSummary> siblings)
    {
        var parentId = siblings.FirstOrDefault()?.ParentId;
        if (parentId is null)
        {
            return;
        }

        _isSaving = true;
        try
        {
            var result = await DocsClient.ReorderAsync(SpaceId, new DocsReorderRequest(parentId.Value, siblings.Select(item => item.Id).ToList()));
            if (result is Result<bool, AeroError>.Ok)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Outline updated", "Section order saved.");
                await LoadAsync();
                return;
            }

            if (result is Result<bool, AeroError>.Failure failure)
                NotifyError("Reorder failed", failure.Error.ToString());
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task MoveNodeToSpaceRootAsync(long nodeId)
    {
        if (nodeId == SpaceId || Space is null)
        {
            return;
        }

        var detail = await LoadDetailAsync(nodeId);
        if (detail is null)
        {
            return;
        }

        var result = await DocsClient.MoveAsync(SpaceId, nodeId, new DocsMoveRequest(SpaceId));

        if (result is Result<DocsDetail, AeroError>.Ok)
        {
            NotificationService.Notify(NotificationSeverity.Success, "Moved", $"{detail.Title} is now at the space root.");
            await LoadAsync();
            return;
        }

        if (result is Result<DocsDetail, AeroError>.Failure failure)
        {
            NotifyError("Move failed", failure.Error.ToString());
        }
    }

    private void UpdateCurrent(Action<MutableDoc> update)
    {
        if (Current is null)
        {
            return;
        }

        var mutable = MutableDoc.From(Current);
        update(mutable);
        Current = mutable.ToDetail();
        _dirty = true;
        HasUnpublishedChanges = true;
    }

    private void InsertSnippet(string type)
    {
        if (Current is null)
        {
            return;
        }

        var snippet = type switch
        {
            "code" => "\n\n```csharp\n// code sample\n```\n",
            "callout" => "\n\n> [!NOTE]\n> Add a clear callout for the reader.\n",
            "table" => "\n\n| Name | Description |\n|---|---|\n| Item | Details |\n",
            "image" => "\n\n![Alt text](/media/docs/example.png)\n",
            "children" => "\n\n## In this space\n\n- Add related child pages here.\n",
            _ => "\n\n## New section\n\nStart writing here.\n"
        };

        UpdateCurrent(current => current.MarkdownContent = $"{current.MarkdownContent}{snippet}");
        ActiveTab = "content";
    }

        /// <summary>
    /// NodeClass method.
    /// </summary>
protected string NodeClass(OutlineNode node)
    {
        var classes = "pe-doc-tree-node";
        if (node.Id == Current?.Id)
        {
            classes += " active";
        }

        if (node.Id == SpaceId)
        {
            classes += " root";
        }

        return classes;
    }

        /// <summary>
    /// TabClass method.
    /// </summary>
protected string TabClass(string tab)
        => ActiveTab == tab ? "active" : string.Empty;

        /// <summary>
    /// StatusClass method.
    /// </summary>
protected string StatusClass(ContentPublicationState state)
        => state == ContentPublicationState.Published
            ? "pe-doc-status published"
            : "pe-doc-status draft";

        /// <summary>
    /// IsSpaceNode method.
    /// </summary>
protected bool IsSpaceNode(OutlineNode node)
        => node.Id == SpaceId;

        /// <summary>
    /// PublicUrl method.
    /// </summary>
protected string PublicUrl(DocsDetail doc)
        => $"/docs/{doc.Slug}";

    private async Task<SiteViewModel?> ResolveCurrentSiteAsync()
    {
        var selectedSite = await CurrentSiteAccessor.GetCurrentSiteAsync();
        if (selectedSite is not null)
        {
            return selectedSite;
        }

        var defaultResult = await SitesClient.GetDefaultAsync();
        return defaultResult is Result<SiteViewModel, AeroError>.Ok ok ? ok.Value : null;
    }

    private void ResetTranslationDraft()
    {
        SelectedTranslationCulture = AvailableTranslationCultures.FirstOrDefault() ?? string.Empty;
        TranslationSlug = string.Empty;
    }

        /// <summary>
    /// FormatCulture method.
    /// </summary>
protected static string FormatCulture(string? culture)
    {
        var normalized = NormalizeCultureName(culture);
        try
        {
            var info = CultureInfo.GetCultureInfo(normalized);
            return $"{info.DisplayName} ({info.Name})";
        }
        catch (CultureNotFoundException)
        {
            return normalized;
        }
    }

    private static string NormalizeCultureName(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            return "en-US";
        }

        try
        {
            return CultureInfo.GetCultureInfo(culture.Trim()).Name;
        }
        catch (CultureNotFoundException)
        {
            return culture.Trim();
        }
    }

    private string GetParentSlug(DocsDetail doc)
        => GetParentSlug(doc.ParentId);

    private string GetParentSlug(long? parentId)
    {
        if (parentId == Space?.Id)
        {
            return Space.Slug;
        }

        return _allDocs.FirstOrDefault(item => item.Id == parentId)?.Slug ?? Space?.Slug ?? "docs";
    }

    private string GenerateUniqueChildSlug(string parentSlug, string title, long? excludeId = null)
    {
        var baseSlug = $"{NormalizeSlug(parentSlug)}/{GenerateSlug(title)}".Trim('/');
        var candidate = baseSlug;
        var suffix = 2;

        while (_allDocs.Any(doc => doc.Id != excludeId && string.Equals(doc.Slug, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{baseSlug}-{suffix++}";
        }

        return candidate;
    }

    private static DocsSummary ToSummary(DocsDetail detail)
        => new(
            detail.Id,
            detail.Title,
            detail.Slug,
            detail.ParentId,
            detail.Order,
            detail.Summary,
            detail.PublicationState,
            detail.PublishedOn,
            detail.ModifiedOn,
            detail.SeoTitle,
            detail.SeoDescription,
            detail.ShowHeaderNavigation,
            detail.HeaderImageUrl,
            detail.PublishedVersion,
            detail.DraftVersion,
            detail.Culture,
            detail.TranslationGroupId);

    private static string NormalizeSlug(string value)
        => string.Join('/', value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(GenerateSlug)
            .Where(part => !string.IsNullOrWhiteSpace(part)));

    private static string SlugLeaf(string value)
    {
        var parts = value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? value : parts[^1];
    }

    private static string GenerateSlug(string value)
    {
        var slug = new string(value
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray());

        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return slug.Trim('-');
    }

    private void NotifyError(string summary, string detail)
    {
        NotificationService.Notify(new NotificationMessage
        {
            Severity = NotificationSeverity.Error,
            Summary = summary,
            Detail = detail,
            Duration = 5000
        });
    }

        /// <summary>
    /// Represents a record for OutlineNode.
    /// </summary>
protected sealed record OutlineNode(long Id, string Title, string Slug, int Depth, int ChildCount);
        /// <summary>
    /// Represents a record for ParentOption.
    /// </summary>
protected sealed record ParentOption(string Id, string Label);

    private sealed class MutableDoc
    {
                /// <summary>
        /// Gets or sets the Id.
        /// </summary>
public long Id { get; set; }
                /// <summary>
        /// Gets or sets the Title.
        /// </summary>
public string Title { get; set; } = string.Empty;
                /// <summary>
        /// Gets or sets the Slug.
        /// </summary>
public string Slug { get; set; } = string.Empty;
                /// <summary>
        /// Gets or sets the Summary.
        /// </summary>
public string? Summary { get; set; }
                /// <summary>
        /// Gets or sets the Markdown Content.
        /// </summary>
public string? MarkdownContent { get; set; }
                /// <summary>
        /// Gets or sets the Seo Title.
        /// </summary>
public string? SeoTitle { get; set; }
                /// <summary>
        /// Gets or sets the Seo Description.
        /// </summary>
public string? SeoDescription { get; set; }
                /// <summary>
        /// Gets or sets the Parent Id.
        /// </summary>
public long? ParentId { get; set; }
                /// <summary>
        /// Gets or sets the Order.
        /// </summary>
public int Order { get; set; }
                /// <summary>
        /// Gets or sets the Publication State.
        /// </summary>
public ContentPublicationState PublicationState { get; set; }
                /// <summary>
        /// Gets or sets the Published On.
        /// </summary>
public DateTimeOffset? PublishedOn { get; set; }
                /// <summary>
        /// Gets or sets the Show Header Navigation.
        /// </summary>
public bool ShowHeaderNavigation { get; set; }
                /// <summary>
        /// Gets or sets the Header Image Url.
        /// </summary>
public string? HeaderImageUrl { get; set; }
                /// <summary>
        /// Gets or sets the Created On.
        /// </summary>
public DateTimeOffset CreatedOn { get; set; }
                /// <summary>
        /// Gets or sets the Modified On.
        /// </summary>
public DateTimeOffset? ModifiedOn { get; set; }
                /// <summary>
        /// Gets or sets the Published Version.
        /// </summary>
public long PublishedVersion { get; set; }
                /// <summary>
        /// Gets or sets the Draft Version.
        /// </summary>
public long DraftVersion { get; set; }
                /// <summary>
        /// Gets or sets the Culture.
        /// </summary>
public string Culture { get; set; } = "en-US";
                /// <summary>
        /// Gets or sets the Translation Group Id.
        /// </summary>
public long? TranslationGroupId { get; set; }

                /// <summary>
        /// From method.
        /// </summary>
public static MutableDoc From(DocsDetail detail)
            => new()
            {
                Id = detail.Id,
                Title = detail.Title,
                Slug = detail.Slug,
                Summary = detail.Summary,
                MarkdownContent = detail.MarkdownContent,
                SeoTitle = detail.SeoTitle,
                SeoDescription = detail.SeoDescription,
                ParentId = detail.ParentId,
                Order = detail.Order,
                PublicationState = detail.PublicationState,
                PublishedOn = detail.PublishedOn,
                ShowHeaderNavigation = detail.ShowHeaderNavigation,
                HeaderImageUrl = detail.HeaderImageUrl,
                CreatedOn = detail.CreatedOn,
                ModifiedOn = detail.ModifiedOn,
                PublishedVersion = detail.PublishedVersion,
                DraftVersion = detail.DraftVersion,
                Culture = detail.Culture,
                TranslationGroupId = detail.TranslationGroupId
            };

                /// <summary>
        /// ToDetail method.
        /// </summary>
public DocsDetail ToDetail()
            => new(
                Id,
                Title,
                Slug,
                Summary,
                MarkdownContent,
                ParentId,
                Order,
                PublicationState,
                SeoTitle,
                SeoDescription,
                PublishedOn,
                ShowHeaderNavigation,
                HeaderImageUrl,
                CreatedOn,
                ModifiedOn,
                PublishedVersion,
                DraftVersion,
                Culture,
                TranslationGroupId);
    }
}
