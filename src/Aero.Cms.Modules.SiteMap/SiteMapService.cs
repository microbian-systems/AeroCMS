using System.Globalization;
using System.Xml.Linq;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Docs;
using Aero.Cms.Modules.Pages;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using Marten;
using Microsoft.AspNetCore.Http;
using static Aero.Core.Railway.Prelude;
using ZiggyCreatures.Caching.Fusion;

namespace Aero.Cms.Modules.SiteMap;

public sealed class SiteMapService : ISiteMapService
{
    private readonly IFusionCache _cache;
    private readonly IPageContentService _pageService;
    private readonly IDocsService _docsService;
    private readonly IQuerySession _session;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISiteContext _siteContext;

    public SiteMapService(
        IFusionCache cache,
        IPageContentService pageService,
        IDocsService docsService,
        IQuerySession session,
        IHttpContextAccessor httpContextAccessor,
        ISiteContext siteContext)
    {
        _cache = cache;
        _pageService = pageService;
        _docsService = docsService;
        _session = session;
        _httpContextAccessor = httpContextAccessor;
        _siteContext = siteContext;
    }

    public async Task<Result<string, AeroError>> BuildSitemapAsync(CancellationToken ct)
    {
        var siteId = _siteContext.SiteId;
        var cacheKey = $"sitemap:xml:{siteId}";

        var cached = await _cache.TryGetAsync<string>(cacheKey, token: ct);
        if (cached.HasValue)
            return Ok<string, AeroError>(cached.Value);

        var entriesResult = await GatherEntriesAsync(ct);
        if (entriesResult is Result<List<SitemapEntry>, AeroError>.Ok ok)
        {
            var xml = RenderSitemap(ok.Value);
            await _cache.SetAsync(cacheKey, xml, tags: ["sitemap"], token: ct);
            return Ok<string, AeroError>(xml);
        }

        return Fail<string, AeroError>(((Result<List<SitemapEntry>, AeroError>.Failure)entriesResult).Error);
    }

    private async Task<Result<List<SitemapEntry>, AeroError>> GatherEntriesAsync(CancellationToken ct)
    {
        var baseUrl = GetBaseUrl();
        if (baseUrl is null)
            return Fail<List<SitemapEntry>, AeroError>(new AeroError.Error("Sitemap generation requires an active HTTP request"));

        var entries = new List<SitemapEntry>();
        var errors = new List<string>();

        var pagesTask = GetPageEntriesAsync(baseUrl, ct);
        var postsTask = GetPostEntriesAsync(baseUrl, ct);
        var docsTask = GetDocEntriesAsync(baseUrl, ct);

        await Task.WhenAll(pagesTask, postsTask, docsTask);

        ProcessResult(pagesTask.Result, entries, errors);
        ProcessResult(postsTask.Result, entries, errors);
        ProcessResult(docsTask.Result, entries, errors);

        if (errors.Count > 0 && entries.Count == 0)
            return Fail<List<SitemapEntry>, AeroError>(new AeroError.Error(string.Join("; ", errors)));

        entries.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        return Ok<List<SitemapEntry>, AeroError>(entries);
    }

    private static void ProcessResult(Result<List<SitemapEntry>, AeroError> result, List<SitemapEntry> entries, List<string> errors)
    {
        if (result is Result<List<SitemapEntry>, AeroError>.Ok ok)
            entries.AddRange(ok.Value);
        else if (result is Result<List<SitemapEntry>, AeroError>.Failure fail)
            errors.Add(fail.Error.ToString());
    }

    private async Task<Result<List<SitemapEntry>, AeroError>> GetPageEntriesAsync(string baseUrl, CancellationToken ct)
    {
        var result = await _pageService.GetAllPagesAsync(0, int.MaxValue, null, ct);
        if (result is Result<(IReadOnlyList<PageDocument> Items, long TotalCount), AeroError>.Ok ok)
        {
            var pages = ok.Value;
            var entries = new List<SitemapEntry>(pages.Items.Count);

            foreach (var page in pages.Items)
            {
                if (!page.IsPubliclyVisible) continue;
                var isHomepage = page.Kind is PageKind.Homepage;
                entries.Add(new SitemapEntry
                {
                    Loc = BuildLoc(baseUrl, page.Slug, isHomepage),
                    LastMod = page.ModifiedOn ?? page.PublishedOn ?? page.CreatedOn,
                    ChangeFreq = isHomepage ? ChangeFrequency.Daily : ChangeFrequency.Weekly,
                    Priority = isHomepage ? 1.0 : 0.8
                });
            }

            return Ok<List<SitemapEntry>, AeroError>(entries);
        }

        return Fail<List<SitemapEntry>, AeroError>(((Result<(IReadOnlyList<PageDocument> Items, long TotalCount), AeroError>.Failure)result).Error);
    }

    private async Task<Result<List<SitemapEntry>, AeroError>> GetPostEntriesAsync(string baseUrl, CancellationToken ct)
    {
        try
        {
            var siteId = _siteContext.SiteId;
            var posts = await _session.Query<BlogPostDocument>()
                .Where(p => p.PublicationState == ContentPublicationState.Published
                         && p.SiteId == siteId)
                .ToListAsync(ct);

            var entries = new List<SitemapEntry>(posts.Count);
            foreach (var post in posts)
            {
                entries.Add(new SitemapEntry
                {
                    Loc = BuildLoc(baseUrl, post.Slug, false),
                    LastMod = post.ModifiedOn ?? post.PublishedOn ?? post.CreatedOn,
                    ChangeFreq = ChangeFrequency.Weekly,
                    Priority = 0.6
                });
            }

            return Ok<List<SitemapEntry>, AeroError>(entries);
        }
        catch (Exception ex)
        {
            return Fail<List<SitemapEntry>, AeroError>(new AeroError.Error($"Blog posts query failed: {ex.Message}"));
        }
    }

    private async Task<Result<List<SitemapEntry>, AeroError>> GetDocEntriesAsync(string baseUrl, CancellationToken ct)
    {
        var result = await _docsService.GetAllAsync(ct);
        if (result is Result<IReadOnlyList<DocsPage>, AeroError>.Ok ok)
        {
            var docs = ok.Value;
            var entries = new List<SitemapEntry>(docs.Count);

            foreach (var doc in docs)
            {
                if (!doc.IsPubliclyVisible) continue;
                entries.Add(new SitemapEntry
                {
                    Loc = BuildLoc(baseUrl, doc.Slug, false),
                    LastMod = doc.ModifiedOn ?? doc.PublishedOn ?? doc.CreatedOn,
                    ChangeFreq = ChangeFrequency.Monthly,
                    Priority = 0.5
                });
            }

            return Ok<List<SitemapEntry>, AeroError>(entries);
        }

        return Fail<List<SitemapEntry>, AeroError>(((Result<IReadOnlyList<DocsPage>, AeroError>.Failure)result).Error);
    }

    private string? GetBaseUrl()
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        return request is null ? null : $"{request.Scheme}://{request.Host}";
    }

    private static string BuildLoc(string baseUrl, string slug, bool isHomepage)
    {
        if (isHomepage) return baseUrl + "/";
        var cleanSlug = slug.Trim('/');
        return string.IsNullOrEmpty(cleanSlug) ? baseUrl + "/" : $"{baseUrl}/{cleanSlug}";
    }

    private static string RenderSitemap(List<SitemapEntry> entries)
    {
        XNamespace xmlns = "http://www.sitemaps.org/schemas/sitemap/0.9";

        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(xmlns + "urlset",
                from e in entries
                select new XElement(xmlns + "url",
                    new XElement(xmlns + "loc", e.Loc),
                    e.LastMod.HasValue
                        ? new XElement(xmlns + "lastmod", e.LastMod.Value.ToString("yyyy-MM-dd"))
                        : null,
                    new XElement(xmlns + "changefreq", e.ChangeFreq.ToString().ToLowerInvariant()),
                    new XElement(xmlns + "priority", e.Priority.ToString("F1", CultureInfo.InvariantCulture))
                )
            )
        );

        // XDocument.ToString() omits the declaration — prepend it manually
        return doc.Declaration + "\n" + doc.ToString(SaveOptions.OmitDuplicateNamespaces);
    }
}
