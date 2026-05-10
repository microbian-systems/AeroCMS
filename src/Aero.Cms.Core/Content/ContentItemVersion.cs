using Aero.Core.Entities;

namespace Aero.Cms.Core.Content;

public sealed class ContentItemVersion : Entity
{
    public long ContentItemId { get; set; }
    public int VersionNumber { get; set; }
    public string FieldsJson { get; set; } = "{}";
    public DateTimeOffset CreatedUtc { get; set; }
}
