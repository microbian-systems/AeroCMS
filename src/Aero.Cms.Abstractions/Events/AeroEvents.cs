using Aero.Models.Entities;

namespace Aero.Cms.Abstractions.Events;

public abstract record AeroEvent(string message);

/// <summary>
/// Event fired when a content's slug has been updated and published.
/// </summary>
public record SlugUpdated(
    long ContentId,
    string ContentType,
    string NewSlug,
    string? OldSlug = null) : AeroEvent($"{OldSlug}->{NewSlug}");


public abstract record AeroEvent<T>(T record, string? msg = null) : AeroEvent(msg!)
{
    // alias events
    public sealed record AliasCreated(AliasViewModel alias, string msg) : AeroEvent<AliasViewModel>(alias, msg);
    public sealed record AliasUpdated(AliasViewModel alias, string msg) : AeroEvent<AliasViewModel>(alias, msg);
    public sealed record AliasDeleted(AliasViewModel alias, string msg) : AeroEvent<AliasViewModel>(alias, msg);

    // category events
    public sealed record CategoryCreated(CategoryViewModel category, string msg) : AeroEvent<CategoryViewModel>(category, msg);
    public sealed record CategoryUpdated(CategoryViewModel category, string msg) : AeroEvent<CategoryViewModel>(category, msg);
    public sealed record CategoryDeleted(CategoryViewModel category, string msg) : AeroEvent<CategoryViewModel>(category, msg);

    // doc events
    public sealed record DocCreated(DocViewModel doc, string msg) : AeroEvent<DocViewModel>(doc, msg);
    public sealed record DocUpdated(DocViewModel doc, string msg) : AeroEvent<DocViewModel>(doc, msg);
    public sealed record DocDeleted(DocViewModel doc, string msg) : AeroEvent<DocViewModel>(doc, msg);

    // media events
    public sealed record MediaCreated(MediaViewModel media, string msg) : AeroEvent<MediaViewModel>(media, msg);
    public sealed record MediaUpdated(MediaViewModel media, string msg) : AeroEvent<MediaViewModel>(media, msg);
    public sealed record MediaDeleted(MediaViewModel media, string msg) : AeroEvent<MediaViewModel>(media, msg);

    // page events
    public sealed record PageCreated(PageViewModel page, string msg) : AeroEvent<PageViewModel>(page, msg);
    public sealed  record PageUpdated(PageViewModel page, string msg) : AeroEvent<PageViewModel>(page, msg);
    public sealed record PageDeleted(PageViewModel page, string msg) : AeroEvent<PageViewModel>(page, msg);

    // post events
    public sealed record PostCreated(PostViewModel post, string msg) : AeroEvent<PostViewModel>(post, msg);
    public sealed record PostUpdated(PostViewModel post, string msg) : AeroEvent<PostViewModel>(post, msg);
    public sealed record PostDeleted(PostViewModel post, string msg) : AeroEvent<PostViewModel>(post, msg);

    // settings events
    // todo - verify we won't be neediing created/deleted events as settings aren't supposed to be deleted or updated (the keys at least)
    public sealed record SettingsUpdated(SettingsViewModel settings, string msg) : AeroEvent<SettingsViewModel>(settings, msg);

    // site events
    public sealed record SiteCreated(SiteViewModel site, string msg) : AeroEvent<SiteViewModel>(site, msg);
    public sealed record SiteUpdated(SiteViewModel site, string msg) : AeroEvent<SiteViewModel>(site, msg);
    public sealed record SiteDeleted(SiteViewModel site, string msg) : AeroEvent<SiteViewModel>(site, msg);

    // tag events
    public sealed record TagCreated(TagViewModel tag, string msg) : AeroEvent<TagViewModel>(tag, msg);
    public sealed record TagUpdated(TagViewModel tag, string msg) : AeroEvent<TagViewModel>(tag, msg);
    public sealed record TagDeleted(TagViewModel tag, string msg) : AeroEvent<TagViewModel>(tag, msg);

    // user events
    public sealed record UserCreated(AeroUser user, string msg) : AeroEvent<AeroUser>(user, msg);
    public sealed record UserUpdated(AeroUser user, string msg) : AeroEvent<AeroUser>(user, msg);
    public sealed record UserDeleted(AeroUser user, string msg) : AeroEvent<AeroUser>(user, msg);
}