using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Aero.Cms.Shared.Pages.Manager;

public partial class AiSettings
{
    [Inject] protected ISettingsHttpClient SettingsClient { get; set; } = default!;

    protected bool Enabled { get; set; }
    protected string Provider { get; set; } = "Tornado";
    protected string Endpoint { get; set; } = string.Empty;
    protected string Model { get; set; } = string.Empty;
    protected string ApiKeySecretName { get; set; } = string.Empty;
    protected string ApiKeyEnvironmentVariable { get; set; } = string.Empty;
    protected string Temperature { get; set; } = "0.3";
    protected string MaxOutputTokens { get; set; } = "1200";
    protected string TimeoutSeconds { get; set; } = "60";
    protected bool StreamResponses { get; set; }
    protected bool SaveUsageTelemetry { get; set; }
    protected bool IsSaving { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    protected async Task LoadAsync()
    {
        var result = await SettingsClient.GetByCategoryAsync("AI");
        if (result is not Result<IReadOnlyList<SettingDetail>, AeroError>.Ok ok)
        {
            return;
        }

        var settings = ok.Value.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        Enabled = Get(settings, "Ai.Enabled", "false").Equals("true", StringComparison.OrdinalIgnoreCase);
        Provider = Get(settings, "Ai.Provider", "Tornado");
        Endpoint = Get(settings, "Ai.Endpoint", string.Empty);
        Model = Get(settings, "Ai.Model", string.Empty);
        ApiKeySecretName = Get(settings, "Ai.ApiKeySecretName", string.Empty);
        ApiKeyEnvironmentVariable = Get(settings, "Ai.ApiKeyEnvironmentVariable", string.Empty);
        Temperature = Get(settings, "Ai.Temperature", "0.3");
        MaxOutputTokens = Get(settings, "Ai.MaxOutputTokens", "1200");
        TimeoutSeconds = Get(settings, "Ai.TimeoutSeconds", "60");
        StreamResponses = Get(settings, "Ai.StreamResponses", "false").Equals("true", StringComparison.OrdinalIgnoreCase);
        SaveUsageTelemetry = Get(settings, "Ai.SaveUsageTelemetry", "false").Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    protected async Task SaveAsync()
    {
        IsSaving = true;

        var settings = new Dictionary<string, string>
        {
            ["Ai.Enabled"] = Enabled.ToString(),
            ["Ai.Provider"] = Provider,
            ["Ai.Endpoint"] = Endpoint,
            ["Ai.Model"] = Model,
            ["Ai.ApiKeySecretName"] = ApiKeySecretName,
            ["Ai.ApiKeyEnvironmentVariable"] = ApiKeyEnvironmentVariable,
            ["Ai.Temperature"] = Temperature,
            ["Ai.MaxOutputTokens"] = MaxOutputTokens,
            ["Ai.TimeoutSeconds"] = TimeoutSeconds,
            ["Ai.StreamResponses"] = StreamResponses.ToString(),
            ["Ai.SaveUsageTelemetry"] = SaveUsageTelemetry.ToString()
        };

        foreach (var setting in settings)
        {
            var result = await SettingsClient.SetAsync(new SetSettingRequest(setting.Key, setting.Value, "AI", "string"));
            if (result is Result<SettingDetail, AeroError>.Failure failure)
            {
                Notify(NotificationSeverity.Error, "AI settings failed", failure.Error.ToString());
                IsSaving = false;
                return;
            }
        }

        Notify(NotificationSeverity.Success, "AI settings saved", "Provider settings were updated.");
        IsSaving = false;
    }

    private static string Get(IReadOnlyDictionary<string, string> settings, string key, string fallback)
        => settings.TryGetValue(key, out var value) ? value : fallback;

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
}
