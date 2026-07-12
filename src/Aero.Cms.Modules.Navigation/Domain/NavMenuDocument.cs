using Aero.Core.Data;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Modules.Navigation.Events;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Navigation.Domain;

/// <summary>
/// Represents a class for NavMenuDocument.
/// </summary>
public sealed class NavMenuDocument : SableDocument, IAuditable, ISiteOwned
{
        /// <summary>
    /// Gets or sets the Site Id.
    /// </summary>
public long SiteId { get; set; }
        /// <summary>
    /// Gets or sets the Translation Group Id.
    /// </summary>
public long? TranslationGroupId { get; set; }
        /// <summary>
    /// Gets or sets the Culture.
    /// </summary>
public string Culture { get; set; } = Aero.Cms.Core.Entities.SitesModel.DefaultCultureName;
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public string Name { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Key.
    /// </summary>
public string Key { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the State.
    /// </summary>
public NavMenuLifecycleState State { get; set; } = NavMenuLifecycleState.Draft;
        /// <summary>
    /// Gets or sets the Has Published Snapshot.
    /// </summary>
public bool HasPublishedSnapshot { get; set; }
        /// <summary>
    /// Gets or sets the Archived On.
    /// </summary>
public DateTimeOffset? ArchivedOn { get; set; }
    // IAuditable
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

        /// <summary>
    /// Create method.
    /// </summary>
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
    /// Apply method.
    /// </summary>
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
    /// Apply method.
    /// </summary>
public void Apply(NavMenuPublished @event)
    {
        HasPublishedSnapshot = true;
        State = NavMenuLifecycleState.Published;
        Touch(@event.UserId, @event.PublishedOn);
    }

        /// <summary>
    /// Apply method.
    /// </summary>
public void Apply(NavMenuArchived @event)
    {
        State = NavMenuLifecycleState.Archived;
        ArchivedOn = @event.ArchivedOn;
        Touch(@event.UserId, @event.ArchivedOn);
    }

        /// <summary>
    /// NormalizeKey method.
    /// </summary>
public static string NormalizeKey(string key)
        => key.Trim().ToLowerInvariant();

    private void Touch(long? userId, DateTimeOffset timestamp)
    {
        ModifiedBy = userId?.ToString();
        ModifiedOn = timestamp;
    }
}

/// <summary>
/// Represents a class for SiteNavigationSettingsDocument.
/// </summary>
public sealed class SiteNavigationSettingsDocument : SableDocument, IAuditable, ISiteOwned
{
        /// <summary>
    /// Gets or sets the Site Id.
    /// </summary>
public long SiteId { get; set; }
        /// <summary>
    /// Gets or sets the Default Nav Menu Id.
    /// </summary>
public long? DefaultNavMenuId { get; set; }
    // IAuditable
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

        /// <summary>
    /// Create method.
    /// </summary>
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
    /// Apply method.
    /// </summary>
    public void Apply(SiteDefaultNavMenuChanged @event)
    {
        DefaultNavMenuId = @event.NavMenuId;
        ModifiedOn = @event.ChangedOn;
        ModifiedBy = @event.UserId.ToString();
    }
}
