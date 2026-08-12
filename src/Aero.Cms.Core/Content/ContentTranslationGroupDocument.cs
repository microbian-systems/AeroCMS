using System.Text.Json;
using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Content;

/// <summary>
/// Durable owner of a content translation group's shared values and revision.
/// No content-item document may persist values represented here.
/// </summary>
public sealed class ContentTranslationGroupDocument : SableDocument, IAuditable
{
    public long SiteId { get; set; }
    public string ContentTypeAlias { get; set; } = string.Empty;
    public long SourceItemId { get; set; }
    public string SourceCulture { get; set; } = string.Empty;
    public int Revision { get; set; }
    public Dictionary<string, JsonElement> SharedFields { get; set; } = [];
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
}
