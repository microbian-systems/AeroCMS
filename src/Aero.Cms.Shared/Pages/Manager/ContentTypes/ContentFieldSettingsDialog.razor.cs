using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Http.Clients;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;

namespace Aero.Cms.Shared.Pages.Manager.ContentTypes;

/// <summary>
/// Transactional dialog wrapper around <see cref="ContentFieldSettingsEditor"/>.
/// </summary>
public partial class ContentFieldSettingsDialog
{
    [Parameter, EditorRequired]
    public ContentFieldDefinition Field { get; set; } = default!;

    [Parameter]
    public IReadOnlyList<ContentFieldDefinition> OwnerFields { get; set; } = [];

    [Parameter]
    public IReadOnlyList<ContentTypeSummary> ContentTypes { get; set; } = [];

    [Parameter]
    public string CurrentContentTypeAlias { get; set; } = string.Empty;

    [Parameter]
    public string FieldTypeLabel { get; set; } = string.Empty;

    [Parameter]
    public bool LocalizationModeLocked { get; set; }

    [Parameter]
    public string? LocalizationModeLockedReason { get; set; }

    [Inject]
    private DialogService DialogService { get; set; } = default!;

    [Inject]
    private IStringLocalizer<Aero.Cms.Shared.Localization.ManagerResource> L { get; set; } = default!;

    private void Cancel() => DialogService.Close();

    private void Apply() => DialogService.Close(Field);
}
