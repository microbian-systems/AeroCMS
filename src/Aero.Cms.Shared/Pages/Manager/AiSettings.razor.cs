using Aero.Cms.Abstractions.Ai;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Ai;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;

namespace Aero.Cms.Shared.Pages.Manager;

/// <summary>
/// Represents a class for AiSettings.
/// </summary>
public partial class AiSettings
{
        /// <summary>
    /// Gets or sets the Ai Client.
    /// </summary>
[Inject] protected IAiHttpClient AiClient { get; set; } = default!;
    [Inject] private IStringLocalizer<Aero.Cms.Shared.Localization.ManagerResource> L { get; set; } = default!;

        /// <summary>
    /// Gets or sets the Is Loading.
    /// </summary>
protected bool IsLoading { get; set; } = true;
        /// <summary>
    /// Gets or sets the Is Saving.
    /// </summary>
protected bool IsSaving { get; set; }
        /// <summary>
    /// Gets or sets the Enabled.
    /// </summary>
protected bool Enabled { get; set; }
        /// <summary>
    /// Gets or sets the Default Provider Id.
    /// </summary>
protected string DefaultProviderId { get; set; } = "opencode";
        /// <summary>
    /// Gets or sets the Providers.
    /// </summary>
protected List<ProviderFormModel> Providers { get; set; } = [];

        /// <summary>
    /// OnInitializedAsync method.
    /// </summary>
protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

        /// <summary>
    /// LoadAsync method.
    /// </summary>
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
            Notify(NotificationSeverity.Error, L["AI settings failed"], failure.Error.ToString());
        }

        IsLoading = false;
    }

        /// <summary>
    /// SaveAsync method.
    /// </summary>
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
            Notify(NotificationSeverity.Success, L["AI settings saved"], L["Provider settings were updated."]);
        }
        else if (result is Result<AiSettingsConfiguration, AeroError>.Failure failure)
        {
            Notify(NotificationSeverity.Error, L["AI settings failed"], failure.Error.ToString());
        }

        IsSaving = false;
    }

        /// <summary>
    /// SelectDefaultProvider method.
    /// </summary>
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

        /// <summary>
    /// Represents a class for ProviderFormModel.
    /// </summary>
protected sealed class ProviderFormModel
    {
                /// <summary>
        /// Gets or sets the Id.
        /// </summary>
public string Id { get; set; } = string.Empty;
                /// <summary>
        /// Gets or sets the Display Name.
        /// </summary>
public string DisplayName { get; set; } = string.Empty;
                /// <summary>
        /// Gets or sets the Provider.
        /// </summary>
public AiProviderKind Provider { get; set; }
                /// <summary>
        /// Gets or sets the Enabled.
        /// </summary>
public bool Enabled { get; set; }
                /// <summary>
        /// Gets or sets the Endpoint.
        /// </summary>
public string Endpoint { get; set; } = string.Empty;
                /// <summary>
        /// Gets or sets the Model.
        /// </summary>
public string Model { get; set; } = string.Empty;
                /// <summary>
        /// Gets or sets the Has Api Key.
        /// </summary>
public bool HasApiKey { get; set; }
                /// <summary>
        /// Gets or sets the Api Key.
        /// </summary>
public string ApiKey { get; set; } = string.Empty;
                /// <summary>
        /// Gets or sets the Clear Api Key.
        /// </summary>
public bool ClearApiKey { get; set; }
                /// <summary>
        /// Gets or sets the Temperature.
        /// </summary>
public string Temperature { get; set; } = "0.3";
                /// <summary>
        /// Gets or sets the Max Output Tokens.
        /// </summary>
public string MaxOutputTokens { get; set; } = "1200";
                /// <summary>
        /// Gets or sets the Timeout Seconds.
        /// </summary>
public string TimeoutSeconds { get; set; } = "60";
                /// <summary>
        /// Gets or sets the Stream Responses.
        /// </summary>
public bool StreamResponses { get; set; }
                /// <summary>
        /// Gets or sets the Save Usage Telemetry.
        /// </summary>
public bool SaveUsageTelemetry { get; set; }
                /// <summary>
        /// Gets or sets the Supports Content Enhancement.
        /// </summary>
public bool SupportsContentEnhancement { get; set; }

                /// <summary>
        /// FromSettings method.
        /// </summary>
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

                /// <summary>
        /// ToUpdate method.
        /// </summary>
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
