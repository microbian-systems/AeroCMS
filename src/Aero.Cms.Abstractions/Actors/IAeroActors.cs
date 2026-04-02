

using Aero.Actors;

namespace Aero.Cms.Abstractions.Interfaces;

public interface IAeroAliasService : IAeroContentActor<AliasViewModel>;
public interface IAeroAuthorService : IAeroContentActor<AuthorViewModel>;
public interface IAeroCategoryService : IAeroContentActor<CategoryViewModel>;
public interface IAeroDocsService : IAeroContentActor<DocViewModel>;
public interface IAeroMediaService : IAeroContentActor<MediaViewModel>;
public interface IAeroPageService : IAeroContentActor<PageViewModel>;
public interface IAeroPostService : IAeroContentActor<PostViewModel>;
public interface IAeroSiteService : IAeroContentActor<SiteViewModel>;
public interface IAeroTagService : IAeroContentActor<TagViewModel>;

public interface IAeroContentActor<T> :
    IAeroActor,
    ICruddable<T, long>,
    ICanFindBySite<T, long>,
    ICanFindBySlug<T, long>,
    ICanFindBySlug<T, string>,
    IHaveState<T>
    where T : class, new()
{
}

public interface IAeroContentActor<T, TKey> :
    IAeroActor,
    ICruddable<T, TKey>,
    ICanFindBySite<T, TKey>,
    ICanFindBySlug<T, string>,
    IHaveState<T>
    where T : class, new()
    where TKey : IEquatable<TKey>, IComparable<TKey>
{
}
