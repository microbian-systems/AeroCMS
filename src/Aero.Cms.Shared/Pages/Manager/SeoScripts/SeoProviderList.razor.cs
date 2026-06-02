using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using Radzen.Blazor;

namespace Aero.Cms.Shared.Pages.Manager.SeoScripts;

public partial class SeoProviderList
{
    [Inject] private ISettingsHttpClient SettingsClient { get; set; } = default!;
    [Inject] private DialogService DialogService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IStringLocalizer<Aero.Cms.Shared.Localization.ManagerResource> L { get; set; } = default!;

    private RadzenDataGrid<SeoProviderSummary>? _grid;
    private IReadOnlyList<SeoProviderSummary> _providers = [];
    private bool _isLoading = true;

    private static IReadOnlyList<SeoProviderDefinition> ProviderDefinitions => SeoProviderRegistry.Definitions;

    protected override async Task OnInitializedAsync()
        => await LoadAsync();

    private async Task LoadAsync()
    {
        _isLoading = true;
        try
        {
            var result = await SettingsClient.GetByCategoryAsync("SEO");
            if (result is Result<IReadOnlyList<SettingDetail>, AeroError>.Ok ok)
            {
                _providers = ProviderDefinitions.Select(provider => ToSummary(provider, ok.Value)).ToList();
                return;
            }

            if (result is Result<IReadOnlyList<SettingDetail>, AeroError>.Failure failure)
            {
                Notify(NotificationSeverity.Error, "SEO providers failed to load", failure.Error.ToString());
            }

            _providers = ProviderDefinitions.Select(provider => ToSummary(provider, [])).ToList();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private static SeoProviderSummary ToSummary(SeoProviderDefinition provider, IReadOnlyList<SettingDetail> settings)
    {
        var trackingSetting = settings.FirstOrDefault(setting => setting.Key == provider.TrackingIdKey);
        var hostSetting = provider.HostKey is null
            ? null
            : settings.FirstOrDefault(setting => setting.Key == provider.HostKey);

        return new SeoProviderSummary(
            provider.Key,
            provider.Name,
            provider.Description,
            trackingSetting?.Value ?? string.Empty,
            !string.IsNullOrWhiteSpace(trackingSetting?.Value),
            new[] { trackingSetting?.UpdatedAt, hostSetting?.UpdatedAt }
                .Where(value => value.HasValue)
                .DefaultIfEmpty()
                .Max());
    }

    private void OnRowClick(DataGridRowMouseEventArgs<SeoProviderSummary> args)
    {
        if (args.Data is not null)
        {
            EditProvider(args.Data.Key);
        }
    }

    private void EditProvider(string key)
        => Navigation.NavigateTo($"/manager/seo/{Uri.EscapeDataString(key)}");

    private async Task DisableProviderAsync(string key)
    {
        var provider = ProviderDefinitions.FirstOrDefault(item => item.Key == key);
        if (provider is null)
        {
            return;
        }

        var confirmed = await DialogService.Confirm(
            $"Disable {provider.Name}? The public layout will stop rendering this provider's scripts.",
            "Disable SEO Provider",
            new ConfirmOptions { OkButtonText = "Disable", CancelButtonText = "Cancel" });

        if (confirmed != true)
        {
            return;
        }

        var results = new List<Result<SettingDetail, AeroError>>
        {
            await SettingsClient.SetAsync(new SetSettingRequest(provider.TrackingIdKey, string.Empty, "SEO", "string"))
        };

        if (provider.HostKey is not null)
        {
            results.Add(await SettingsClient.SetAsync(new SetSettingRequest(provider.HostKey, string.Empty, "SEO", "string")));
        }

        if (results.OfType<Result<SettingDetail, AeroError>.Failure>().FirstOrDefault() is { } failure)
        {
            Notify(NotificationSeverity.Error, "Disable failed", failure.Error.ToString());
            return;
        }

        Notify(NotificationSeverity.Success, "Provider disabled", $"{provider.Name} scripts are disabled.");
        await LoadAsync();
        _grid?.Reload();
    }

    private void Notify(NotificationSeverity severity, string summary, string detail)
        => NotificationService.Notify(new NotificationMessage
        {
            Severity = severity,
            Summary = summary,
            Detail = detail,
            Duration = 4000
        });
}
