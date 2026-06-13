using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Modules.Footer.Events;
using Aero.Core.Entities;

namespace Aero.Cms.Modules.Footer.Domain;

public sealed class FooterDocument : Entity, ISiteOwned
{
    public long SiteId { get; set; }
    public long? TranslationGroupId { get; set; }
    public string Culture { get; set; } = Aero.Cms.Core.Entities.SitesModel.DefaultCultureName;
    public string Name { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string? Description { get; set; }
    public FooterLifecycleState State { get; set; } = FooterLifecycleState.Draft;
    public bool HasPublishedSnapshot { get; set; }
    public DateTimeOffset? ArchivedOn { get; set; }
    public long? CreatedByUserId { get; set; }
    public long? ModifiedByUserId { get; set; }

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

    public void Apply(FooterPublished @event)
    {
        HasPublishedSnapshot = true;
        State = FooterLifecycleState.Published;
        Touch(@event.UserId, @event.PublishedOn);
    }

    public void Apply(FooterArchived @event)
    {
        State = FooterLifecycleState.Archived;
        ArchivedOn = @event.ArchivedOn;
        Touch(@event.UserId, @event.ArchivedOn);
    }

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

public sealed class SiteFooterSettingsDocument : Entity, ISiteOwned
{
    public long SiteId { get; set; }
    public long? DefaultFooterId { get; set; }
    public long? ModifiedByUserId { get; set; }

    public static SiteFooterSettingsDocument Create(long siteId, SiteDefaultFooterChanged @event) => new()
    {
        Id = siteId,
        SiteId = siteId,
        DefaultFooterId = @event.FooterId,
        CreatedOn = @event.ChangedOn,
        ModifiedOn = @event.ChangedOn,
        ModifiedByUserId = @event.UserId
    };

    public void Apply(SiteDefaultFooterChanged @event)
    {
        DefaultFooterId = @event.FooterId;
        ModifiedOn = @event.ChangedOn;
        ModifiedByUserId = @event.UserId;
    }
}

public enum FooterLifecycleState
{
    Draft,
    Published,
    PublishedWithDraft,
    Archived
}
