using Aero.Core.Data;
using Aero.Cms.Abstractions.Theming;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Theming;

public sealed class ThemeDefinitionDocument : SableDocument, IAuditable
{ public long TenantId { get; set; } public string Name { get; set; } = string.Empty; public string Slug { get; set; } = string.Empty; public string? Description { get; set; } public ThemeTokenSet DraftTokenSet { get; set; } = new(); public long Revision { get; set; } = 1; public bool Archived { get; set; } public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow; public DateTimeOffset? ModifiedOn { get; set; } public string? CreatedBy { get; set; } public string? ModifiedBy { get; set; } }
public sealed class ThemeVersionDocument : SableDocument
{ public long TenantId { get; set; } public long ThemeDefinitionId { get; set; } public string ThemeId { get; set; } = string.Empty; public string Version { get; set; } = string.Empty; public string DataThemeName { get; set; } = string.Empty; public ThemeTokenSet TokenSet { get; set; } = new(); public string Css { get; set; } = string.Empty; public string CssSha256 { get; set; } = string.Empty; public DateTimeOffset PublishedOn { get; set; } = DateTimeOffset.UtcNow; public string? PublishedBy { get; set; } }
public sealed class SiteThemePublicationDocument : SableDocument
{ public long TenantId { get; set; } public long SiteId { get; set; } public string ThemeId { get; set; } = string.Empty; public string Version { get; set; } = string.Empty; public long Revision { get; set; } public DateTimeOffset PublishedOn { get; set; } = DateTimeOffset.UtcNow; public string? PublishedBy { get; set; } public string? PreviousThemeId { get; set; } public string? PreviousVersion { get; set; } }
