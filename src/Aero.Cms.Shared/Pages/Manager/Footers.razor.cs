using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Radzen;
using Radzen.Blazor;

namespace Aero.Cms.Shared.Pages.Manager;

/// <summary>
/// Represents a class for Footers.
/// </summary>
public partial class Footers
{
    [Inject] private IFootersHttpClient FootersClient { get; set; } = default!;
    [Inject] private DialogService DialogService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ILogger<Footers> Logger { get; set; } = default!;
    [Inject] private IStringLocalizer<Aero.Cms.Shared.Localization.ManagerResource> L { get; set; } = default!;

    private RadzenDataGrid<FooterSummary>? _footerGrid;
    private IReadOnlyList<FooterSummary> _footers = Array.Empty<FooterSummary>();
    private bool _isLoading;
    private bool _isSaving;

        /// <summary>
    /// OnInitializedAsync method.
    /// </summary>
protected override async Task OnInitializedAsync()
    {
        await LoadFootersAsync();
    }

    private async Task LoadFootersAsync()
    {
        _isLoading = true;
        try
        {
            var result = await FootersClient.GetAllAsync();
            if (result is Result<IReadOnlyList<FooterSummary>, AeroError>.Ok ok)
            {
                _footers = ok.Value.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Name).ToList();
            }
            else if (result is Result<IReadOnlyList<FooterSummary>, AeroError>.Failure fail)
            {
                Notify(NotificationSeverity.Error, L[L["Footers failed to load"]], fail.Error.ToString());
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load footers");
            Notify(NotificationSeverity.Error, L["Footers failed to load"], ex.Message);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task CreateFooterAsync()
    {
        var dialogResult = await DialogService.OpenAsync<CreateFooterDialog>(
            L["New Footer"],
            null,
            new DialogOptions { Width = "460px", Resizable = false, Draggable = false });

        if (dialogResult is not CreateFooterDialogResult request)
        {
            return;
        }

        _isSaving = true;
        try
        {
            var result = await FootersClient.CreateAsync(new CreateFooterRequest(
                request.Name.Trim(),
                request.Description?.Trim()));

            if (result is Result<FooterDetail, AeroError>.Ok ok)
            {
                Notify(NotificationSeverity.Success, L["Footer created"]);
                Navigation.NavigateTo(EditorUrl(ok.Value.Id));
            }
            else if (result is Result<FooterDetail, AeroError>.Failure fail)
            {
                Notify(NotificationSeverity.Error, L["Footer was not created"], fail.Error.ToString());
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to create footer");
            Notify(NotificationSeverity.Error, L["Footer was not created"], ex.Message);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private void OpenEditor(FooterSummary? footer)
    {
        if (footer is null)
        {
            return;
        }

        Navigation.NavigateTo(EditorUrl(footer.Id));
    }

    private static string EditorUrl(long id) => $"/manager/footers/editor/{id}";

    private static string DisplayState(string? state)
        => string.IsNullOrWhiteSpace(state)
            ? "Draft"
            : state.Replace("PublishedWithDraft", "Published + Draft", StringComparison.Ordinal);

    private static BadgeStyle BadgeStyleFor(string? state)
        => state switch
        {
            "Published" => BadgeStyle.Success,
            "PublishedWithDraft" => BadgeStyle.Warning,
            "Archived" => BadgeStyle.Danger,
            _ => BadgeStyle.Info
        };

    private void Notify(NotificationSeverity severity, string summary, string? detail = null)
    {
        NotificationService.Notify(new NotificationMessage
        {
            Severity = severity,
            Summary = summary,
            Detail = detail ?? string.Empty,
            Duration = severity == NotificationSeverity.Error ? 6000 : 3500
        });
    }
}
