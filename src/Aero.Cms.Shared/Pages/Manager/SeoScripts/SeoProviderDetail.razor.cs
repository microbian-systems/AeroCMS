using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;

namespace Aero.Cms.Shared.Pages.Manager.SeoScripts;

public partial class SeoProviderDetail
{
    [Parameter] public string ProviderKey { get; set; } = string.Empty;

    [Inject] private ISettingsHttpClient SettingsClient { get; set; } = default!;
    [Inject] private DialogService DialogService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IStringLocalizer<Aero.Cms.Shared.Localization.ManagerResource> L { get; set; } = default!;

    private SeoProviderDefinition? _provider;
    private SeoProviderEditModel _model = new();
    private bool _isLoading = true;
    private bool _isSaving;

    private bool IsEnabled => !string.IsNullOrWhiteSpace(_model.TrackingId);

    protected override async Task OnParametersSetAsync()
    {
        _provider = SeoProviderRegistry.Find(ProviderKey);
        if (_provider is null)
        {
            Notify(NotificationSeverity.Error, "SEO provider not found", $"No SEO provider exists for '{ProviderKey}'.");
            Navigation.NavigateTo("/manager/seo");
            return;
        }

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_provider is null)
        {
            return;
        }

        _isLoading = true;
        try
        {
            var result = await SettingsClient.GetByCategoryAsync("SEO");
            if (result is Result<IReadOnlyList<SettingDetail>, AeroError>.Ok ok)
            {
                _model = new SeoProviderEditModel
                {
                    TrackingId = GetString(ok.Value, _provider.TrackingIdKey),
                    Host = _provider.HostKey is null
                        ? string.Empty
                        : GetString(ok.Value, _provider.HostKey, _provider.HostDefault ?? string.Empty)
                };
                return;
            }

            if (result is Result<IReadOnlyList<SettingDetail>, AeroError>.Failure failure)
            {
                Notify(NotificationSeverity.Error, "Provider failed to load", failure.Error.ToString());
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task SaveAsync(SeoProviderEditModel model)
    {
        if (_provider is null)
        {
            return;
        }

        _isSaving = true;
        try
        {
            var results = new List<Result<SettingDetail, AeroError>>
            {
                await SettingsClient.SetAsync(new SetSettingRequest(_provider.TrackingIdKey, model.TrackingId.Trim(), "SEO", "string"))
            };

            if (_provider.HostKey is not null)
            {
                var host = string.IsNullOrWhiteSpace(model.Host)
                    ? _provider.HostDefault ?? string.Empty
                    : model.Host.Trim();
                results.Add(await SettingsClient.SetAsync(new SetSettingRequest(_provider.HostKey, host, "SEO", "string")));
            }

            if (results.OfType<Result<SettingDetail, AeroError>.Failure>().FirstOrDefault() is { } failure)
            {
                Notify(NotificationSeverity.Error, "Save failed", failure.Error.ToString());
                return;
            }

            Notify(NotificationSeverity.Success, "Provider saved", $"{_provider.Name} settings were updated.");
            await LoadAsync();
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task DisableAsync()
    {
        if (_provider is null)
        {
            return;
        }

        var confirmed = await DialogService.Confirm(
            $"Disable {_provider.Name}? The public layout will stop rendering this provider's scripts.",
            "Disable SEO Provider",
            new ConfirmOptions { OkButtonText = "Disable", CancelButtonText = "Cancel" });

        if (confirmed != true)
        {
            return;
        }

        _isSaving = true;
        try
        {
            var results = new List<Result<SettingDetail, AeroError>>
            {
                await SettingsClient.SetAsync(new SetSettingRequest(_provider.TrackingIdKey, string.Empty, "SEO", "string"))
            };

            if (_provider.HostKey is not null)
            {
                results.Add(await SettingsClient.SetAsync(new SetSettingRequest(_provider.HostKey, string.Empty, "SEO", "string")));
            }

            if (results.OfType<Result<SettingDetail, AeroError>.Failure>().FirstOrDefault() is { } failure)
            {
                Notify(NotificationSeverity.Error, "Disable failed", failure.Error.ToString());
                return;
            }

            Notify(NotificationSeverity.Success, "Provider disabled", $"{_provider.Name} scripts are disabled.");
            await LoadAsync();
        }
        finally
        {
            _isSaving = false;
        }
    }

    private void BackToList()
        => Navigation.NavigateTo("/manager/seo");

    private static string GetString(IReadOnlyList<SettingDetail> settings, string key, string defaultValue = "")
        => settings.FirstOrDefault(setting => setting.Key == key)?.Value ?? defaultValue;

    private void Notify(NotificationSeverity severity, string summary, string detail)
        => NotificationService.Notify(new NotificationMessage
        {
            Severity = severity,
            Summary = summary,
            Detail = detail,
            Duration = 4000
        });
}
