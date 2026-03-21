using Aero.Cms.Modules.Blog;
using Aero.Cms.Modules.Pages;
using Aero.Cms.Modules.Setup;
using Marten;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Integration;

internal sealed class InMemoryCmsDocumentSessionHarness
{
    private readonly Dictionary<string, PageDocument> _pageDocuments = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BlogPostDocument> _blogPostDocuments = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ContentSlugDocument> _slugDocuments = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SetupStateDocument> _setupStateDocuments = new(StringComparer.Ordinal);

    public InMemoryCmsDocumentSessionHarness()
    {
        Session = Substitute.For<IDocumentSession>();

        Session.LoadAsync<PageDocument>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                _pageDocuments.TryGetValue(callInfo.ArgAt<string>(0), out var page);
                return page is null ? null : Clone(page);
            });

        Session.LoadAsync<BlogPostDocument>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                _blogPostDocuments.TryGetValue(callInfo.ArgAt<string>(0), out var post);
                return post is null ? null : Clone(post);
            });

        Session.LoadAsync<ContentSlugDocument>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                _slugDocuments.TryGetValue(callInfo.ArgAt<string>(0), out var slugDocument);
                return slugDocument is null ? null : Clone(slugDocument);
            });

        Session.LoadAsync<SetupStateDocument>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                _setupStateDocuments.TryGetValue(callInfo.ArgAt<string>(0), out var setupState);
                return setupState is null ? null : Clone(setupState);
            });

        Session.When(call => call.Store(Arg.Any<PageDocument[]>()))
            .Do(callInfo =>
            {
                foreach (var page in callInfo.Arg<PageDocument[]>())
                {
                    OnStore?.Invoke(page);
                    _pageDocuments[page.Id] = Clone(page);
                }
            });

        Session.When(call => call.Store(Arg.Any<BlogPostDocument[]>()))
            .Do(callInfo =>
            {
                foreach (var post in callInfo.Arg<BlogPostDocument[]>())
                {
                    OnStore?.Invoke(post);
                    _blogPostDocuments[post.Id] = Clone(post);
                }
            });

        Session.When(call => call.Store(Arg.Any<ContentSlugDocument[]>()))
            .Do(callInfo =>
            {
                foreach (var slugDocument in callInfo.Arg<ContentSlugDocument[]>())
                {
                    OnStore?.Invoke(slugDocument);
                    _slugDocuments[slugDocument.Id] = Clone(slugDocument);
                }
            });

        Session.When(call => call.Store(Arg.Any<SetupStateDocument[]>()))
            .Do(callInfo =>
            {
                foreach (var setupState in callInfo.Arg<SetupStateDocument[]>())
                {
                    OnStore?.Invoke(setupState);
                    _setupStateDocuments[setupState.Id] = Clone(setupState);
                }
            });

        Session.When(call => call.Delete(Arg.Any<ContentSlugDocument>()))
            .Do(callInfo =>
            {
                var slugDocument = callInfo.Arg<ContentSlugDocument>();
                _slugDocuments.Remove(slugDocument.Id);
            });

        Session.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
    }

    public IDocumentSession Session { get; }
    public Action<object>? OnStore { get; set; }
    public IReadOnlyDictionary<string, PageDocument> Pages => _pageDocuments;
    public IReadOnlyDictionary<string, BlogPostDocument> BlogPosts => _blogPostDocuments;
    public IReadOnlyDictionary<string, SetupStateDocument> SetupStates => _setupStateDocuments;

    private static PageDocument Clone(PageDocument page)
        => new()
        {
            Id = page.Id,
            Kind = page.Kind,
            Slug = page.Slug,
            Title = page.Title,
            Summary = page.Summary,
            SeoTitle = page.SeoTitle,
            SeoDescription = page.SeoDescription,
            Body = page.Body,
            PublicationState = page.PublicationState,
            CreatedAtUtc = page.CreatedAtUtc,
            UpdatedAtUtc = page.UpdatedAtUtc,
            PublishedAtUtc = page.PublishedAtUtc
        };

    private static BlogPostDocument Clone(BlogPostDocument post)
        => new()
        {
            Id = post.Id,
            Slug = post.Slug,
            Title = post.Title,
            Excerpt = post.Excerpt,
            SeoTitle = post.SeoTitle,
            SeoDescription = post.SeoDescription,
            Body = post.Body,
            PublicationState = post.PublicationState,
            CreatedAtUtc = post.CreatedAtUtc,
            UpdatedAtUtc = post.UpdatedAtUtc,
            PublishedAtUtc = post.PublishedAtUtc
        };

    private static ContentSlugDocument Clone(ContentSlugDocument slugDocument)
        => new()
        {
            Id = slugDocument.Id,
            Slug = slugDocument.Slug,
            NormalizedSlug = slugDocument.NormalizedSlug,
            OwnerId = slugDocument.OwnerId,
            OwnerType = slugDocument.OwnerType
        };

    private static SetupStateDocument Clone(SetupStateDocument setupState)
        => new()
        {
            Id = setupState.Id,
            IsComplete = setupState.IsComplete,
            CompletedAtUtc = setupState.CompletedAtUtc
        };
}
