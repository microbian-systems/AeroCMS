using Aero.Core.Data;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Localization;
using AeroDB.Sable;

namespace Aero.Cms.Core.Content;

/// <summary>
/// Stores the site-scoped definition of a content type.
/// </summary>
public sealed class ContentTypeDocument : SableDocument, IAuditable
{
    /// <summary>Gets or sets the identifier of the site that owns this definition.</summary>
    public long SiteId { get; set; }
    /// <summary>Gets or sets the site-scoped alias used to identify the content type.</summary>
    public string Alias { get; set; } = string.Empty;
    /// <summary>Gets or sets the display name of the content type.</summary>
    public string Name { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the optional descriptive text for the content type.
    /// </summary>
public string? Description { get; set; }
        /// <summary>
    /// Gets or sets the optional category used to organize the content type.
    /// </summary>
public string? Category { get; set; }
        /// <summary>
    /// Gets or sets the optional icon identifier used by clients.
    /// </summary>
public string? Icon { get; set; }
        /// <summary>Gets or sets whether this type is a singleton or collection.</summary>
public ContentCardinality Cardinality { get; set; } = ContentCardinality.Collection;
        /// <summary>Gets or sets whether items are flat or hierarchical.</summary>
public ContentStructure Structure { get; set; } = ContentStructure.Flat;
        /// <summary>Gets or sets the rules for hierarchical placement.</summary>
public ContentHierarchyRules HierarchyRules { get; set; } = new();
        /// <summary>
    /// Gets or sets whether items of this type may be addressed by a public URL.
    /// </summary>
public bool AllowPublicUrl { get; set; }
    /// <summary>
    /// Gets or sets whether published items of this type may enter the site-search index.
    /// </summary>
    public bool IncludeInSearch { get; set; } = true;

    /// <summary>
    /// Gets or sets whether otherwise eligible items may enter the public AI corpus.
    /// </summary>
    public bool IncludeInPublicAi { get; set; }
        /// <summary>
    /// Gets or sets the field definitions that form this content type's schema.
    /// </summary>
public List<ContentFieldDefinition> Fields { get; set; } = [];
        /// <summary>
    /// Gets or sets the optional Scriban template used to render items of this type.
    /// </summary>
public string? ScribanTemplate { get; set; }
        /// <summary>
    /// Gets or sets the optional scheduling configuration for items of this type.
    /// </summary>
    public ContentTypeScheduleConfig? ScheduleConfig { get; set; }

    /// <summary>Gets or sets the culture and AI-review policy for this content type.</summary>
    public ContentLocalizationSettings Localization { get; set; } = new();

    // IAuditable
    /// <summary>Gets or sets the audit creation timestamp.</summary>
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>Gets or sets the most recent audit modification timestamp.</summary>
    public DateTimeOffset? ModifiedOn { get; set; }
    /// <summary>Gets or sets the identity that created this definition, if recorded.</summary>
    public string? CreatedBy { get; set; }
    /// <summary>Gets or sets the identity that last modified this definition, if recorded.</summary>
    public string? ModifiedBy { get; set; }
}
