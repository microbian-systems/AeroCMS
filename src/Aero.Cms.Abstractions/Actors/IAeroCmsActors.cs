

using Aero.Actors;
using Aero.Cms.Abstractions.Interfaces;

namespace Aero.Cms.Abstractions.Actors;

public interface IAeroAliasActor : IAeroCmsContentActor<AliasViewModel>;
public interface IAeroAuthorActor : IAeroCmsContentActor<AuthorViewModel>;
public interface IAeroCategoryActor : IAeroCmsContentActor<CategoryViewModel>;
public interface IAeroDocsActor : IAeroCmsContentActor<DocViewModel>;
public interface IAeroMediaActor : IAeroCmsContentActor<MediaViewModel>;
public interface IAeroPageActor : IAeroCmsContentActor<PageViewModel>;
public interface IAeroPostActor : IAeroCmsContentActor<PostViewModel>;
public interface IAeroSiteActor : IAeroCmsContentActor<SiteViewModel>;
public interface IAeroTagActor : IAeroCmsContentActor<TagViewModel>;



public interface IAeroCmsContentActor<T> :
    IAeroActor,
    ICruddable<T, long>,
    ICanFindBySite<T, long>,
    ICanFindBySlug<T, long>,
    ICanFindBySlug<T, string>,
    IHaveState<T>
    where T : AeroEntityViewModel;


public interface IAeroCmsContentActor<T, TKey> :
    IAeroActor,
    ICruddable<T, TKey>,
    ICanFindBySite<T, TKey>,
    ICanFindBySlug<T, string>,
    IHaveState<T>
    where T : AeroEntityViewModel
    where TKey : IEquatable<TKey>, IComparable<TKey>;