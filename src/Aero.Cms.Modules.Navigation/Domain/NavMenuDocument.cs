using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Modules.Navigation.Events;
using Aero.Core.Entities;

namespace Aero.Cms.Modules.Navigation.Domain;

public sealed class NavMenuDocument : Entity, ISiteOwned
{
    public long SiteId { get; set; }
    public long? TranslationGroupId { get; set; }
    public string Culture { get; set; } = Aero.Cms.Core.Entities.SitesModel.DefaultCultureName;
    public string Name { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public NavMenuLifecycleState State { get; set; } = NavMenuLifecycleState.Draft;
    public bool HasPublishedSnapshot { get; set; }
    public DateTimeOffset? ArchivedOn { get; set; }
    public long? CreatedByUserId { get; set; }
    public long? ModifiedByUserId { get; set; }

    public static NavMenuDocument Create(long id, NavMenuCreated @event) => new()
    {
        Id = id,
        SiteId = @event.SiteId,
        TranslationGroupId = @event.TranslationGroupId,
        Culture = @event.Culture,
        Name = @event.Name.Trim(),
        Key = NormalizeKey(@event.Key),
        State = NavMenuLifecycleState.Draft,
        CreatedByUserId = @event.UserId,
        CreatedOn = @event.CreatedOn
    };

    public void Apply(NavMenuDraftSaved @event)
    {
        Name = @event.Name.Trim();
        Key = NormalizeKey(@event.Key);
        State = HasPublishedSnapshot
            ? NavMenuLifecycleState.PublishedWithDraft
            : NavMenuLifecycleState.Draft;
        Touch(@event.UserId, @event.SavedOn);
    }

    public void Apply(NavMenuPublished @event)
    {
        HasPublishedSnapshot = true;
        State = NavMenuLifecycleState.Published;
        Touch(@event.UserId, @event.PublishedOn);
    }

    public void Apply(NavMenuArchived @event)
    {
        State = NavMenuLifecycleState.Archived;
        ArchivedOn = @event.ArchivedOn;
        Touch(@event.UserId, @event.ArchivedOn);
    }

    public static string NormalizeKey(string key)
        => key.Trim().ToLowerInvariant();

    private void Touch(long? userId, DateTimeOffset timestamp)
    {
        ModifiedByUserId = userId;
        ModifiedOn = timestamp;
    }
}

public sealed class SiteNavigationSettingsDocument : Entity, ISiteOwned
{
    public long SiteId { get; set; }
    public long? DefaultNavMenuId { get; set; }
    public long? ModifiedByUserId { get; set; }

    public static SiteNavigationSettingsDocument Create(long siteId, SiteDefaultNavMenuChanged @event) => new()
    {
        Id = siteId,
        SiteId = siteId,
        DefaultNavMenuId = @event.NavMenuId,
        CreatedOn = @event.ChangedOn,
        ModifiedOn = @event.ChangedOn,
        ModifiedByUserId = @event.UserId
    };

    public void Apply(SiteDefaultNavMenuChanged @event)
    {
        DefaultNavMenuId = @event.NavMenuId;
        ModifiedOn = @event.ChangedOn;
        ModifiedByUserId = @event.UserId;
    }
}
