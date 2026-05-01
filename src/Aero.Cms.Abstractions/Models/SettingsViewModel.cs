namespace Aero.Cms.Abstractions.Models;

[Alias("SettingsViewModel")]
[GenerateSerializer]
public record SettingsViewModel : AeroEntityViewModel
{
    [Id(1)]
    public Dictionary<string, (string field, object value)> Settings { get; } = [];
}

[GenerateSerializer]
[Alias("SettingsErrorViewModel")]
public record SettingsErrorViewModel : AeroErrorViewModel<SettingsViewModel>;