namespace Aero.Cms.Abstractions.Models;

/// <summary>
/// Represents a record for SettingsViewModel.
/// </summary>
[Alias("SettingsViewModel")]
[GenerateSerializer]
public record SettingsViewModel : AeroEntityViewModel
{
        /// <summary>
    /// Gets or sets the Settings.
    /// </summary>
[Id(1)]
    public Dictionary<string, (string field, object value)> Settings { get; } = [];
}

/// <summary>
/// Represents a record for SettingsErrorViewModel.
/// </summary>
[GenerateSerializer]
[Alias("SettingsErrorViewModel")]
public record SettingsErrorViewModel : AeroErrorViewModel<SettingsViewModel>;