using Aero.Cms.Abstractions.Ai;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Aero.Cms.Shared.Pages.Manager;

public partial class AiSettings
{
    [Inject] protected IAiHttpClient AiClient { get; set; } = default!;

    protected bool IsLoading { get; set; } = true;
    protected bool IsSaving { get; set; }
    protected bool Enabled { get; set; }
    protected string DefaultProviderId { get; set; } = "tornado";
    protected List<ProviderFormModel> Providers { get; set; } = [];

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    protected async Task LoadAsync()
    {
        IsLoading = true;
        var result = await AiClient.GetSettingsAsync();
        if (result is Result<AiSettingsConfiguration, AeroError>.Ok ok)
        {
            Enabled = ok.Value.Enabled;
            DefaultProviderId = ok.Value.DefaultProviderId;
            Providers = ok.Value.Providers.Select(ProviderFormModel.FromSettings).ToList();
        }
        else if (result is Result<AiSettingsConfiguration, AeroError>.Failure failure)
        {
            Notify(NotificationSeverity.Error, "AI settings failed", failure.Error.ToString());
        }

        IsLoading = false;
    }

    protected async Task SaveAsync()
    {
        IsSaving = true;

        var request = new SaveAiSettingsRequest(
            Enabled,
            DefaultProviderId,
            Providers.Select(provider => provider.ToUpdate()).ToList());

        var result = await AiClient.SaveSettingsAsync(request);
        if (result is Result<AiSettingsConfiguration, AeroError>.Ok ok)
        {
            Enabled = ok.Value.Enabled;
            DefaultProviderId = ok.Value.DefaultProviderId;
            Providers = ok.Value.Providers.Select(ProviderFormModel.FromSettings).ToList();
            Notify(NotificationSeverity.Success, "AI settings saved", "Provider settings were updated.");
        }
        else if (result is Result<AiSettingsConfiguration, AeroError>.Failure failure)
        {
            Notify(NotificationSeverity.Error, "AI settings failed", failure.Error.ToString());
        }

        IsSaving = false;
    }

    protected void SelectDefaultProvider(string providerId)
    {
        DefaultProviderId = providerId;
    }

    private void Notify(NotificationSeverity severity, string summary, string detail)
    {
        NotificationService.Notify(new NotificationMessage
        {
            Severity = severity,
            Summary = summary,
            Detail = detail,
            Duration = 4000
        });
    }

    protected sealed class ProviderFormModel
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public AiProviderKind Provider { get; set; }
        public bool Enabled { get; set; }
        public string Endpoint { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public bool HasApiKey { get; set; }
        public string ApiKey { get; set; } = string.Empty;
        public bool ClearApiKey { get; set; }
        public string Temperature { get; set; } = "0.3";
        public string MaxOutputTokens { get; set; } = "1200";
        public string TimeoutSeconds { get; set; } = "60";
        public bool StreamResponses { get; set; }
        public bool SaveUsageTelemetry { get; set; }
        public bool SupportsContentEnhancement { get; set; }

        public static ProviderFormModel FromSettings(AiProviderSettings settings)
            => new()
            {
                Id = settings.Id,
                DisplayName = settings.DisplayName,
                Provider = settings.Provider,
                Enabled = settings.Enabled,
                Endpoint = settings.Endpoint ?? string.Empty,
                Model = settings.Model ?? string.Empty,
                HasApiKey = settings.HasApiKey,
                Temperature = settings.Temperature.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                MaxOutputTokens = settings.MaxOutputTokens.ToString(System.Globalization.CultureInfo.InvariantCulture),
                TimeoutSeconds = settings.TimeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                StreamResponses = settings.StreamResponses,
                SaveUsageTelemetry = settings.SaveUsageTelemetry,
                SupportsContentEnhancement = settings.SupportsContentEnhancement
            };

        public AiProviderSettingsUpdate ToUpdate()
            => new(
                Id,
                DisplayName,
                Provider,
                Enabled,
                string.IsNullOrWhiteSpace(Endpoint) ? null : Endpoint,
                string.IsNullOrWhiteSpace(Model) ? null : Model,
                string.IsNullOrWhiteSpace(ApiKey) ? null : ApiKey,
                ClearApiKey,
                ParseFloat(Temperature, 0.3f),
                ParseInt(MaxOutputTokens, 1200),
                ParseInt(TimeoutSeconds, 60),
                StreamResponses,
                SaveUsageTelemetry);

        private static int ParseInt(string value, int fallback)
            => int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : fallback;

        private static float ParseFloat(string value, float fallback)
            => float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : fallback;
    }
}
