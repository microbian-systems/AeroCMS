using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Radzen;

namespace Aero.Cms.Shared.Pages.Manager;

/// <summary>Displays the immutable deployment-installed theme catalog.</summary>
public partial class Themes
{
    [Inject] private IThemesHttpClient ThemesClient { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ILogger<Themes> Logger { get; set; } = default!;
    [Inject] private IStringLocalizer<Aero.Cms.Shared.Localization.ManagerResource> L { get; set; } = default!;

    private IReadOnlyList<ThemeSummary> _themes = [];
    private bool _isLoading;
    private string? _errorMessage;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await LoadThemesAsync();
    }

    private async Task LoadThemesAsync()
    {
        _isLoading = true;
        _errorMessage = null;
        try
        {
            var result = await ThemesClient.GetAllAsync();
            if (result is Result<IReadOnlyList<ThemeSummary>, AeroError>.Ok ok)
            {
                _themes = ok.Value
                    .OrderByDescending(static theme => theme.IsSafeDefault)
                    .ThenBy(static theme => theme.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static theme => theme.Version, StringComparer.Ordinal)
                    .ToList();
            }
            else if (result is Result<IReadOnlyList<ThemeSummary>, AeroError>.Failure failure)
            {
                _errorMessage = failure.Error.ToString();
                Notify(L["Themes failed to load"], _errorMessage);
            }
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Failed to load the deployment-installed theme catalog.");
            _errorMessage = exception.Message;
            Notify(L["Themes failed to load"], exception.Message);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void OpenSites() => Navigation.NavigateTo("/manager/sites");

    private void Notify(string summary, string detail)
    {
        NotificationService.Notify(new NotificationMessage
        {
            Severity = NotificationSeverity.Error,
            Summary = summary,
            Detail = detail,
            Duration = 6000
        });
    }
}
