using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Modules.Footer.Events;
using Aero.Core.Entities;

namespace Aero.Cms.Modules.Footer.Domain;

/// <summary>
/// Represents a class for FooterDocument.
/// </summary>
public sealed class FooterDocument : Entity, ISiteOwned
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
    /// Gets or sets the Description.
    /// </summary>
public string? Description { get; set; }
        /// <summary>
    /// Gets or sets the State.
    /// </summary>
public FooterLifecycleState State { get; set; } = FooterLifecycleState.Draft;
        /// <summary>
    /// Gets or sets the Has Published Snapshot.
    /// </summary>
public bool HasPublishedSnapshot { get; set; }
        /// <summary>
    /// Gets or sets the Archived On.
    /// </summary>
public DateTimeOffset? ArchivedOn { get; set; }
        /// <summary>
    /// Gets or sets the Created By User Id.
    /// </summary>
public long? CreatedByUserId { get; set; }
        /// <summary>
    /// Gets or sets the Modified By User Id.
    /// </summary>
public long? ModifiedByUserId { get; set; }

        /// <summary>
    /// Create method.
    /// </summary>
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
        CreatedByUserId = @event.UserId,
        CreatedOn = @event.CreatedOn
    };

        /// <summary>
    /// Apply method.
    /// </summary>
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
    /// Apply method.
    /// </summary>
public void Apply(FooterPublished @event)
    {
        HasPublishedSnapshot = true;
        State = FooterLifecycleState.Published;
        Touch(@event.UserId, @event.PublishedOn);
    }

        /// <summary>
    /// Apply method.
    /// </summary>
public void Apply(FooterArchived @event)
    {
        State = FooterLifecycleState.Archived;
        ArchivedOn = @event.ArchivedOn;
        Touch(@event.UserId, @event.ArchivedOn);
    }

        /// <summary>
    /// NormalizeKey method.
    /// </summary>
public static string NormalizeKey(string key)
        => key.Trim().ToLowerInvariant().Replace(' ', '-');

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void Touch(long? userId, DateTimeOffset timestamp)
    {
        ModifiedByUserId = userId;
        ModifiedOn = timestamp;
    }
}

/// <summary>
/// Represents a class for SiteFooterSettingsDocument.
/// </summary>
public sealed class SiteFooterSettingsDocument : Entity, ISiteOwned
{
        /// <summary>
    /// Gets or sets the Site Id.
    /// </summary>
public long SiteId { get; set; }
        /// <summary>
    /// Gets or sets the Default Footer Id.
    /// </summary>
public long? DefaultFooterId { get; set; }
        /// <summary>
    /// Gets or sets the Modified By User Id.
    /// </summary>
public long? ModifiedByUserId { get; set; }

        /// <summary>
    /// Create method.
    /// </summary>
public static SiteFooterSettingsDocument Create(long siteId, SiteDefaultFooterChanged @event) => new()
    {
        Id = siteId,
        SiteId = siteId,
        DefaultFooterId = @event.FooterId,
        CreatedOn = @event.ChangedOn,
        ModifiedOn = @event.ChangedOn,
        ModifiedByUserId = @event.UserId
    };

        /// <summary>
    /// Apply method.
    /// </summary>
public void Apply(SiteDefaultFooterChanged @event)
    {
        DefaultFooterId = @event.FooterId;
        ModifiedOn = @event.ChangedOn;
        ModifiedByUserId = @event.UserId;
    }
}

/// <summary>
/// Defines an enumeration for FooterLifecycleState.
/// </summary>
public enum FooterLifecycleState
{
    Draft,
    Published,
    PublishedWithDraft,
    Archived
}
