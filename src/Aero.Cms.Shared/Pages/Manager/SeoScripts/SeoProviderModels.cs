namespace Aero.Cms.Shared.Pages.Manager.SeoScripts;

public sealed record SeoProviderDefinition(
    string Key,
    string Name,
    string TrackingIdKey,
    string TrackingIdLabel,
    string Description,
    string? HostKey = null,
    string? HostLabel = null,
    string? HostDefault = null);

public sealed record SeoProviderSummary(
    string Key,
    string Name,
    string Description,
    string TrackingId,
    bool Enabled,
    DateTime? LastModified);

public sealed class SeoProviderEditModel
{
    public string TrackingId { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
}
