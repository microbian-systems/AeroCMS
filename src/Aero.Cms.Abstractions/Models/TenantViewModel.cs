namespace Aero.Cms.Abstractions.Models;


// todo - move this into a completely different private project ❗❗❗
public class TenantViewModel
{
    public long Id { get; set; }
    public long AccountId { get; set; }
    public string? Name { get; set; }
    public string? Host { get; set; }
    public List<(long siteId, string siteName)> Settings { get; } = [];
    public Dictionary<string, object> MetaData{ get; } = [];
    public DateTimeOffset? Created { get; set; }
    public DateTimeOffset? Updated { get; set; }
}
