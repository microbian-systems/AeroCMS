using Aero.Core.Data;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Modules.Navigation.Events;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Navigation.Domain;

/// <summary>
/// Stores the queryable lifecycle and audit projection of a navigation-menu event stream.
/// </summary>
/// <remarks>
/// Editable and published component trees remain in stream events; this document carries
/// identity, site, culture, translation-group, lifecycle, and audit fields used for queries.
/// </remarks>
public sealed class NavMenuDocument : SableDocument, IAuditable, ISiteOwned
{
    /// <summary>
    /// Gets or sets the owning site identifier.
    /// </summary>
public long SiteId { get; set; }
    /// <summary>
    /// Gets or sets the identifier shared by culture variants of the same logical menu.
    /// </summary>
public long? TranslationGroupId { get; set; }
    /// <summary>
    /// Gets or sets the canonical culture name represented by this variant.
    /// </summary>
public string Culture { get; set; } = Aero.Cms.Core.Entities.SitesModel.DefaultCultureName;
    /// <summary>
    /// Gets or sets the editor-facing menu name.
    /// </summary>
public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the normalized key unique within the owning site and culture.
    /// </summary>
public string Key { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the projected draft, publication, or archive state.
    /// </summary>
public NavMenuLifecycleState State { get; set; } = NavMenuLifecycleState.Draft;
    /// <summary>
    /// Gets or sets whether the stream contains at least one published snapshot.
    /// </summary>
public bool HasPublishedSnapshot { get; set; }
    /// <summary>
    /// Gets or sets the timestamp of the applied archive event.
    /// </summary>
public DateTimeOffset? ArchivedOn { get; set; }
    // IAuditable
    /// <summary>
    /// Gets or sets when the creation event occurred.
    /// </summary>
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>
    /// Gets or sets when the latest lifecycle event was applied.
    /// </summary>
    public DateTimeOffset? ModifiedOn { get; set; }
    /// <summary>
    /// Gets or sets the string form of the actor that created the menu.
    /// </summary>
    public string? CreatedBy { get; set; }
    /// <summary>
    /// Gets or sets the string form of the actor that last changed the menu.
    /// </summary>
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Creates a document projection from the first event in a navigation stream.
    /// </summary>
    /// <param name="id">The identifier encoded by the stream key.</param>
    /// <param name="event">The creation event.</param>
    /// <returns>A draft document with trimmed name and normalized key.</returns>
    public static NavMenuDocument Create(long id, NavMenuCreated @event) => new()
    {
        Id = id,
        SiteId = @event.SiteId,
        TranslationGroupId = @event.TranslationGroupId,
        Culture = @event.Culture,
        Name = @event.Name.Trim(),
        Key = NormalizeKey(@event.Key),
        State = NavMenuLifecycleState.Draft,
        CreatedBy = @event.UserId.ToString(),
        CreatedOn = @event.CreatedOn
    };

    /// <summary>
    /// Applies draft metadata and marks whether the draft follows an existing publication.
    /// </summary>
    /// <param name="event">The appended draft event.</param>
public void Apply(NavMenuDraftSaved @event)
    {
        Name = @event.Name.Trim();
        Key = NormalizeKey(@event.Key);
        State = HasPublishedSnapshot
            ? NavMenuLifecycleState.PublishedWithDraft
            : NavMenuLifecycleState.Draft;
        Touch(@event.UserId, @event.SavedOn);
    }

    /// <summary>
    /// Marks the document as published and records the publication actor and timestamp.
    /// </summary>
    /// <param name="event">The appended publication event.</param>
public void Apply(NavMenuPublished @event)
    {
        HasPublishedSnapshot = true;
        State = NavMenuLifecycleState.Published;
        Touch(@event.UserId, @event.PublishedOn);
    }

    /// <summary>
    /// Marks the document as archived and records the archive metadata.
    /// </summary>
    /// <param name="event">The appended archive event.</param>
public void Apply(NavMenuArchived @event)
    {
        State = NavMenuLifecycleState.Archived;
        ArchivedOn = @event.ArchivedOn;
        Touch(@event.UserId, @event.ArchivedOn);
    }

    /// <summary>
    /// Trims a navigation key and converts it to invariant lower case.
    /// </summary>
    /// <param name="key">The key to normalize.</param>
    /// <returns>The normalized key.</returns>
public static string NormalizeKey(string key)
        => key.Trim().ToLowerInvariant();

    /// <summary>
    /// Updates modification audit fields from an event.
    /// </summary>
    /// <param name="userId">The optional actor identifier.</param>
    /// <param name="timestamp">The event timestamp.</param>
    private void Touch(long? userId, DateTimeOffset timestamp)
    {
        ModifiedBy = userId?.ToString();
        ModifiedOn = timestamp;
    }
}

/// <summary>
/// Projects a site's default-navigation selection from its settings event stream.
/// </summary>
public sealed class SiteNavigationSettingsDocument : SableDocument, IAuditable, ISiteOwned
{
    /// <summary>
    /// Gets or sets the site that owns this settings document.
    /// </summary>
public long SiteId { get; set; }
    /// <summary>
    /// Gets or sets the selected default menu identifier, or <see langword="null"/> when cleared.
    /// </summary>
public long? DefaultNavMenuId { get; set; }
    // IAuditable
    /// <summary>
    /// Gets or sets when the settings projection was first created.
    /// </summary>
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>
    /// Gets or sets when the default selection last changed.
    /// </summary>
    public DateTimeOffset? ModifiedOn { get; set; }
    /// <summary>
    /// Gets or sets the creating actor; the current projector leaves this unset.
    /// </summary>
    public string? CreatedBy { get; set; }
    /// <summary>
    /// Gets or sets the string form of the actor that last changed the selection.
    /// </summary>
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Creates the site settings projection from its first change event.
    /// </summary>
    /// <param name="siteId">The site identifier encoded by the stream key.</param>
    /// <param name="event">The initial default-menu change.</param>
    /// <returns>A settings document whose document identifier equals the site identifier.</returns>
    public static SiteNavigationSettingsDocument Create(long siteId, SiteDefaultNavMenuChanged @event) => new()
    {
        Id = siteId,
        SiteId = siteId,
        DefaultNavMenuId = @event.NavMenuId,
        CreatedOn = @event.ChangedOn,
        ModifiedOn = @event.ChangedOn,
        ModifiedBy = @event.UserId.ToString()
    };

    /// <summary>
    /// Applies a later default-menu selection and its modification audit fields.
    /// </summary>
    /// <param name="event">The default-menu change event.</param>
    public void Apply(SiteDefaultNavMenuChanged @event)
    {
        DefaultNavMenuId = @event.NavMenuId;
        ModifiedOn = @event.ChangedOn;
        ModifiedBy = @event.UserId.ToString();
    }
}
