using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Abstractions.Interfaces;

public interface IAliasGrain : IContentGrain<AliasViewModel>;
public interface IAuthorGrain : IContentGrain<AuthorViewModel>;
public interface ICategoryGrain : IContentGrain<CategoryViewModel>;
public interface IDocsGrain : IContentGrain<DocViewModel>;
public interface IMediaGrain : IContentGrain<MediaViewModel>;
public interface IPageGrain : IContentGrain<PageViewModel>;
public interface IPostGrain : IContentGrain<PostViewModel>;
public interface ISiteGrain : IContentGrain<SiteViewModel>;
public interface ITagGrain : IContentGrain<TagViewModel>;