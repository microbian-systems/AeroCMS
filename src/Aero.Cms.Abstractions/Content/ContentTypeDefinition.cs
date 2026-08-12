using Aero.Core.Data;
using Aero.Cms.Abstractions.Content.Localization;

namespace Aero.Cms.Abstractions.Content;

/// <summary>
/// Represents a class for ContentTypeDefinition.
/// </summary>
public sealed class ContentTypeDefinition : IAuditable
{
    private ContentLocalizationSettings localization = new();
    /// <summary>
    /// Gets or sets the persisted content-type identifier. A value of zero represents a new definition.
    /// </summary>
    public long Id { get; set; }

        /// <summary>
    /// Gets or sets the Site Id.
    /// </summary>
public long SiteId { get; set; }
        /// <summary>
    /// Gets or sets the Alias.
    /// </summary>
public string Alias { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public string Name { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description { get; set; }
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public string? Category { get; set; }
        /// <summary>
    /// Gets or sets the Icon.
    /// </summary>
public string? Icon { get; set; }

    /// <summary>Gets or sets whether this type is a singleton or collection.</summary>
    public ContentCardinality Cardinality { get; set; } = ContentCardinality.Collection;

    /// <summary>Gets or sets whether items are flat or hierarchical.</summary>
    public ContentStructure Structure { get; set; } = ContentStructure.Flat;

    /// <summary>Gets or sets hierarchy rules used when <see cref="Structure"/> is hierarchical.</summary>
    public ContentHierarchyRules HierarchyRules { get; set; } = new();

    /// <summary>
    /// When true, entries of this type can be addressed by their own public URL.
    /// Content types are embedded-first by default to avoid accidental public pages.
    /// </summary>
    public bool AllowPublicUrl { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public DateTimeOffset? ModifiedOn { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// When true, published entries of this type may contribute to site-wide search.
    /// </summary>
    public bool IncludeInSearch { get; set; } = true;

    /// <summary>
    /// When true, otherwise eligible entries may contribute public fields to the public AI corpus.
    /// Public AI eligibility also requires search inclusion and publication.
    /// </summary>
    public bool IncludeInPublicAi { get; set; }

    /// <summary>
    /// The fields that this content type defines.
    /// </summary>
    public List<ContentFieldDefinition> Fields { get; set; } = [];

    /// <summary>
    /// Optional custom Scriban template. When null/empty, the system
    /// auto-generates one from Fields.
    /// </summary>
    public string? ScribanTemplate { get; set; }

    /// <summary>
    /// Optional scheduling configuration.
    /// </summary>
    public ContentTypeScheduleConfig? ScheduleConfig { get; set; }

    /// <summary>
    /// Gets or sets the culture-resolution and AI-review rules for entries of this type.
    /// </summary>
    public ContentLocalizationSettings Localization
    {
        get => localization;
        set => localization = value ?? new();
    }
}
