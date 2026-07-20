using Aero.Core.Data;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Modules.Footer.Events;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Footer.Domain;

/// <summary>
/// Materialized authoring metadata and lifecycle state for one site-and-culture footer stream.
/// </summary>
/// <remarks>
/// Draft and published snapshots remain in the event stream; this document records only searchable
/// metadata and whether a published snapshot exists.
/// </remarks>
public sealed class FooterDocument : SableDocument, IAuditable, ISiteOwned
{
    /// <summary>Gets or sets the identifier of the site that owns the footer.</summary>
    public long SiteId { get; set; }

    /// <summary>Gets or sets the identifier shared by translated variants.</summary>
    public long? TranslationGroupId { get; set; }

    /// <summary>Gets or sets the normalized culture represented by this footer.</summary>
    public string Culture { get; set; } = Aero.Cms.Core.Entities.SitesModel.DefaultCultureName;

    /// <summary>Gets or sets the author-facing footer name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the normalized key, unique within a site and culture.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional trimmed author-facing description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the projected authoring lifecycle state.</summary>
    public FooterLifecycleState State { get; set; } = FooterLifecycleState.Draft;

    /// <summary>Gets or sets whether the event stream contains a published snapshot.</summary>
    public bool HasPublishedSnapshot { get; set; }

    /// <summary>Gets or sets the archive timestamp, when the footer has been archived.</summary>
    public DateTimeOffset? ArchivedOn { get; set; }

    // IAuditable
    /// <inheritdoc />
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public DateTimeOffset? ModifiedOn { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Creates the initial projected document from a stream identifier and creation event.
    /// </summary>
    /// <param name="id">The footer identifier parsed from the event stream key.</param>
    /// <param name="event">The event that establishes the footer.</param>
    /// <returns>A draft document with trimmed metadata and a normalized key.</returns>
    public static FooterDocument Create(long id, FooterCreated @event) => new()
    {
        Id = id,
        SiteId = @event.SiteId,
        TranslationGroupId = @event.TranslationGroupId,
        Culture = @event.Culture,
        Name = @event.Name.Trim(),
        Key = NormalizeKey(@event.Key),
        Description = Clean(@event.Description),
        State = FooterLifecycleState.Draft,
        CreatedBy = @event.UserId.ToString(),
        CreatedOn = @event.CreatedOn
    };

    /// <summary>
    /// Applies a saved draft's metadata and marks whether it follows an existing publication.
    /// </summary>
    /// <param name="event">The saved-draft event to apply.</param>
    public void Apply(FooterDraftSaved @event)
    {
        Name = @event.Name.Trim();
        Key = NormalizeKey(@event.Key);
        Description = Clean(@event.Description);
        State = HasPublishedSnapshot
            ? FooterLifecycleState.PublishedWithDraft
            : FooterLifecycleState.Draft;
        Touch(@event.UserId, @event.SavedOn);
    }

    /// <summary>
    /// Records that a published snapshot exists and clears the projected draft-after-publish state.
    /// </summary>
    /// <param name="event">The publication event whose actor and timestamp update the audit fields.</param>
    public void Apply(FooterPublished @event)
    {
        HasPublishedSnapshot = true;
        State = FooterLifecycleState.Published;
        Touch(@event.UserId, @event.PublishedOn);
    }

    /// <summary>
    /// Marks the footer archived and records the archive audit data.
    /// </summary>
    /// <param name="event">The archive event to apply.</param>
    public void Apply(FooterArchived @event)
    {
        State = FooterLifecycleState.Archived;
        ArchivedOn = @event.ArchivedOn;
        Touch(@event.UserId, @event.ArchivedOn);
    }

    /// <summary>
    /// Produces the persisted key representation used by the footer document.
    /// </summary>
    /// <param name="key">The key to normalize.</param>
    /// <returns>The trimmed, invariant-lowercase key with space characters replaced by hyphens.</returns>
    /// <remarks>This normalization does not remove punctuation or replace non-space whitespace.</remarks>
    public static string NormalizeKey(string key)
        => key.Trim().ToLowerInvariant().Replace(' ', '-');

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void Touch(long? userId, DateTimeOffset timestamp)
    {
        ModifiedBy = userId?.ToString();
        ModifiedOn = timestamp;
    }
}

/// <summary>
/// Materialized selection of the default footer for a site.
/// </summary>
public sealed class SiteFooterSettingsDocument : SableDocument, IAuditable, ISiteOwned
{
    /// <summary>Gets or sets the site whose selection is represented.</summary>
    public long SiteId { get; set; }

    /// <summary>Gets or sets the selected footer identifier, or <see langword="null"/> when none is selected.</summary>
    public long? DefaultFooterId { get; set; }

    // IAuditable
    /// <inheritdoc />
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public DateTimeOffset? ModifiedOn { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Creates a site settings document from the first default-selection event.
    /// </summary>
    /// <param name="siteId">The site identifier parsed from the event stream key.</param>
    /// <param name="event">The initial default-selection event.</param>
    /// <returns>A settings document whose identity is the site identifier.</returns>
    /// <remarks>The current projection records the actor as <see cref="ModifiedBy"/>; it does not set <see cref="CreatedBy"/>.</remarks>
    public static SiteFooterSettingsDocument Create(long siteId, SiteDefaultFooterChanged @event) => new()
    {
        Id = siteId,
        SiteId = siteId,
        DefaultFooterId = @event.FooterId,
        CreatedOn = @event.ChangedOn,
        ModifiedOn = @event.ChangedOn,
        ModifiedBy = @event.UserId.ToString()
    };

    /// <summary>
    /// Replaces the selected footer and updates modification audit data.
    /// </summary>
    /// <param name="event">The default-selection event to apply.</param>
    public void Apply(SiteDefaultFooterChanged @event)
    {
        DefaultFooterId = @event.FooterId;
        ModifiedOn = @event.ChangedOn;
        ModifiedBy = @event.UserId.ToString();
    }
}

/// <summary>
/// Describes the projected authoring state of a footer stream.
/// </summary>
public enum FooterLifecycleState
{
    /// <summary>The footer has an editable draft but has never been published.</summary>
    Draft,

    /// <summary>The latest editor snapshot is published.</summary>
    Published,

    /// <summary>A published snapshot exists and a newer draft has been saved.</summary>
    PublishedWithDraft,

    /// <summary>The footer has been archived and is excluded from normal listing and resolution.</summary>
    Archived
}
