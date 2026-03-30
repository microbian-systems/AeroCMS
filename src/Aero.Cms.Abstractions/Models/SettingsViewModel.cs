namespace Aero.Cms.Abstractions.Models;

public class SettingsViewModel
{
    public long Id { get; set; }
    public long SiteId { get; set; }
    public Dictionary<string, (string field, object value)> Settings { get; } = [];
    public Dictionary<string, object> MetaData{ get; } = [];
    public DateTimeOffset? Created { get; set; }
    public DateTimeOffset? Updated { get; set; }
}