namespace Aero.Cms.Shared.Pages.Manager.SeoScripts;

/// <summary>
/// Represents a record for SeoProviderDefinition.
/// </summary>
public sealed record SeoProviderDefinition(
    string Key,
    string Name,
    string TrackingIdKey,
    string TrackingIdLabel,
    string Description,
    string? HostKey = null,
    string? HostLabel = null,
    string? HostDefault = null);

/// <summary>
/// Represents a record for SeoProviderSummary.
/// </summary>
public sealed record SeoProviderSummary(
    string Key,
    string Name,
    string Description,
    string TrackingId,
    bool Enabled,
    DateTime? LastModified);

/// <summary>
/// Represents a class for SeoProviderEditModel.
/// </summary>
public sealed class SeoProviderEditModel
{
        /// <summary>
    /// Gets or sets the Tracking Id.
    /// </summary>
public string TrackingId { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Host.
    /// </summary>
public string Host { get; set; } = string.Empty;
}
