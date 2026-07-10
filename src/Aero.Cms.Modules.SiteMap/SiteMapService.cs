using System.Globalization;
using System.Xml.Linq;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Docs;
using Aero.Cms.Modules.Pages;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using AeroDB;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using static Aero.Core.Railway.Prelude;
using ZiggyCreatures.Caching.Fusion;

namespace Aero.Cms.Modules.SiteMap;

public sealed class SiteMapService : ISiteMapService
{
    private readonly IFusionCache _cache;
    private readonly IDocsService _docsService;
    private readonly IQuerySession _session;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISiteContext _siteContext;
    private readonly IHostEnvironment _environment;

    public SiteMapService(
        IFusionCache cache,
        IDocsService docsService,
        IQuerySession session,
        IHttpContextAccessor httpContextAccessor,
        ISiteContext siteContext,
        IHostEnvironment environment)
    {
        _cache = cache;
        _docsService = docsService;
        _session = session;
        _httpContextAccessor = httpContextAccessor;
        _siteContext = siteContext;
        _environment = environment;
    }

    public async Task<Result<string, AeroError>> BuildSitemapAsync(CancellationToken ct)
        => await BuildSitemapAsync(GetDefaultCulture(), ct);

    public async Task<Result<string, AeroError>> BuildSitemapAsync(string? culture, CancellationToken ct)
    {
        var siteId = _siteContext.SiteId;
        var normalizedCulture = NormalizeCultureOrDefault(culture, GetDefaultCulture());
        if (!GetSupportedCultures().Contains(normalizedCulture, StringComparer.OrdinalIgnoreCase))
            return Fail<string, AeroError>(AeroError.ValidationError([$"Culture '{culture}' is not supported by the current site."]));

        var cacheKey = $"sitemap:xml:{siteId}:{normalizedCulture}";

        if (_environment.IsProduction())
        {
            var cached = await _cache.TryGetAsync<string>(cacheKey, token: ct);
            if (cached.HasValue)
                return Ok<string, AeroError>(cached.Value);
        }

        var entriesResult = await GatherEntriesAsync(normalizedCulture, ct);
        if (entriesResult is Result<List<SitemapEntry>, AeroError>.Ok ok)
        {
            var xml = RenderSitemap(ok.Value);
            if (_environment.IsProduction())
            {
                await _cache.SetAsync(cacheKey, xml, tags: ["sitemap"], token: ct);
            }
            return Ok<string, AeroError>(xml);
        }

        return Fail<string, AeroError>(((Result<List<SitemapEntry>, AeroError>.Failure)entriesResult).Error);
    }

    public async Task<Result<string, AeroError>> BuildSitemapIndexAsync(CancellationToken ct)
    {
        var baseUrl = GetBaseUrl();
        if (baseUrl is null)
            return Fail<string, AeroError>(new AeroError.Error("Sitemap generation requires an active HTTP request"));

        var siteId = _siteContext.SiteId;
        var cacheKey = $"sitemap:index:{siteId}";

        if (_environment.IsProduction())
        {
            var cached = await _cache.TryGetAsync<string>(cacheKey, token: ct);
            if (cached.HasValue)
                return Ok<string, AeroError>(cached.Value);
        }

        var cultures = GetSupportedCultures();
        var xml = RenderSitemapIndex(cultures.Select(culture =>
            BuildLoc(baseUrl, $"sitemap-{culture.ToLowerInvariant()}.xml", isHomepage: false)));

        if (_environment.IsProduction())
        {
            await _cache.SetAsync(cacheKey, xml, tags: ["sitemap"], token: ct);
        }

        return Ok<string, AeroError>(xml);
    }

    private async Task<Result<List<SitemapEntry>, AeroError>> GatherEntriesAsync(string culture, CancellationToken ct)
    {
        var baseUrl = GetBaseUrl();
        if (baseUrl is null)
            return Fail<List<SitemapEntry>, AeroError>(new AeroError.Error("Sitemap generation requires an active HTTP request"));

        var entries = new List<SitemapEntry>();
        var errors = new List<string>();

        var pagesTask = GetPageEntriesAsync(baseUrl, culture, ct);
        var postsTask = GetPostEntriesAsync(baseUrl, culture, ct);
        var docsTask = GetDocEntriesAsync(baseUrl, culture, ct);

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

    private async Task<Result<List<SitemapEntry>, AeroError>> GetPageEntriesAsync(string baseUrl, string culture, CancellationToken ct)
    {
        try
        {
            var siteId = _siteContext.SiteId;
            var pages = await _session.Query<PageDocument>()
                .Where(p => p.SiteId == siteId
                         && p.Culture == culture
                         && p.PublicationState == ContentPublicationState.Published
                         && !p.IsHidden
                         && !p.Deleted)
                .ToListAsync(ct);

            var TranslationGroupIds = pages
                .Select(p => p.TranslationGroupId ?? p.Id)
                .Distinct()
                .ToList();

            var variantLookup = await BuildPageVariantLookupAsync(baseUrl, TranslationGroupIds, ct);
            var entries = new List<SitemapEntry>(pages.Count);

            foreach (var page in pages)
            {
                if (!page.IsPubliclyVisible || page.IsHidden) continue;
                var isHomepage = page.Kind is PageKind.Homepage;
                var TranslationGroupId = page.TranslationGroupId ?? page.Id;
                entries.Add(new SitemapEntry
                {
                    Loc = BuildCultureLoc(baseUrl, page.Culture, page.Slug, isHomepage),
                    LastMod = page.ModifiedOn ?? page.PublishedOn ?? page.CreatedOn,
                    ChangeFreq = isHomepage ? ChangeFrequency.Daily : ChangeFrequency.Weekly,
                    Priority = isHomepage ? 1.0 : 0.8,
                    Alternates = variantLookup.GetValueOrDefault(TranslationGroupId, [])
                });
            }

            return Ok<List<SitemapEntry>, AeroError>(entries);
        }
        catch (Exception ex)
        {
            return Fail<List<SitemapEntry>, AeroError>(new AeroError.Error($"Pages query failed: {ex.Message}"));
        }
    }

    private async Task<Result<List<SitemapEntry>, AeroError>> GetPostEntriesAsync(string baseUrl, string culture, CancellationToken ct)
    {
        try
        {
            var siteId = _siteContext.SiteId;
            var posts = await _session.Query<PostDocument>()
                .Where(p => p.PublicationState == ContentPublicationState.Published
                         && p.SiteId == siteId
                         && p.Culture == culture)
                .ToListAsync(ct);

            var TranslationGroupIds = posts
                .Select(p => p.TranslationGroupId ?? p.Id)
                .Distinct()
                .ToList();

            var variantLookup = await BuildPostVariantLookupAsync(baseUrl, TranslationGroupIds, ct);
            var entries = new List<SitemapEntry>(posts.Count);
            foreach (var post in posts)
            {
                var TranslationGroupId = post.TranslationGroupId ?? post.Id;
                entries.Add(new SitemapEntry
                {
                    Loc = BuildBlogPostLoc(baseUrl, post.Culture, post.Slug),
                    LastMod = post.ModifiedOn ?? post.PublishedOn ?? post.CreatedOn,
                    ChangeFreq = ChangeFrequency.Weekly,
                    Priority = 0.6,
                    Alternates = variantLookup.GetValueOrDefault(TranslationGroupId, [])
                });
            }

            return Ok<List<SitemapEntry>, AeroError>(entries);
        }
        catch (Exception ex)
        {
            return Fail<List<SitemapEntry>, AeroError>(new AeroError.Error($"Blog posts query failed: {ex.Message}"));
        }
    }

    private async Task<Result<List<SitemapEntry>, AeroError>> GetDocEntriesAsync(string baseUrl, string culture, CancellationToken ct)
    {
        var result = await _docsService.GetPublishedAsync(culture, ct);
        if (result is Result<IReadOnlyList<DocsPage>, AeroError>.Ok ok)
        {
            var docs = ok.Value;
            var TranslationGroupIds = docs
                .Select(doc => doc.TranslationGroupId ?? doc.Id)
                .Distinct()
                .ToList();
            var variantLookup = await BuildDocVariantLookupAsync(baseUrl, TranslationGroupIds, ct);
            var entries = new List<SitemapEntry>(docs.Count);

            foreach (var doc in docs)
            {
                if (!doc.IsPubliclyVisible) continue;
                var TranslationGroupId = doc.TranslationGroupId ?? doc.Id;
                entries.Add(new SitemapEntry
                {
                    Loc = BuildCultureLoc(baseUrl, doc.Culture, doc.Slug, false),
                    LastMod = doc.ModifiedOn ?? doc.PublishedOn ?? doc.CreatedOn,
                    ChangeFreq = ChangeFrequency.Monthly,
                    Priority = 0.5,
                    Alternates = variantLookup.GetValueOrDefault(TranslationGroupId, [])
                });
            }

            return Ok<List<SitemapEntry>, AeroError>(entries);
        }

        return Fail<List<SitemapEntry>, AeroError>(((Result<IReadOnlyList<DocsPage>, AeroError>.Failure)result).Error);
    }

    private async Task<Dictionary<long, IReadOnlyList<SitemapAlternateLink>>> BuildDocVariantLookupAsync(
        string baseUrl,
        IReadOnlyList<long> TranslationGroupIds,
        CancellationToken ct)
    {
        if (TranslationGroupIds.Count == 0)
            return [];

        var TranslationGroupIdValues = TranslationGroupIds
            .Select(id => (long?)id)
            .ToArray();

        var variants = await _session.Query<DocsPage>()
            .Where(doc => doc.SiteId == _siteContext.SiteId
                       && doc.PublicationState == ContentPublicationState.Published
                       && doc.TranslationGroupId != null
                       && TranslationGroupIdValues.Contains(doc.TranslationGroupId))
            .ToListAsync(ct);

        return variants
            .GroupBy(doc => doc.TranslationGroupId ?? doc.Id)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<SitemapAlternateLink>)BuildDocAlternates(baseUrl, group));
    }

    private async Task<Dictionary<long, IReadOnlyList<SitemapAlternateLink>>> BuildPageVariantLookupAsync(
        string baseUrl,
        IReadOnlyList<long> TranslationGroupIds,
        CancellationToken ct)
    {
        if (TranslationGroupIds.Count == 0)
            return [];

        var TranslationGroupIdValues = TranslationGroupIds
            .Select(id => (long?)id)
            .ToArray();

        var variants = await _session.Query<PageDocument>()
            .Where(p => p.SiteId == _siteContext.SiteId
                     && p.PublicationState == ContentPublicationState.Published
                     && !p.IsHidden
                     && !p.Deleted
                     && p.TranslationGroupId != null
                     && TranslationGroupIdValues.Contains(p.TranslationGroupId))
            .ToListAsync(ct);

        return variants
            .GroupBy(p => p.TranslationGroupId ?? p.Id)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<SitemapAlternateLink>)BuildPageAlternates(baseUrl, group));
    }

    private async Task<Dictionary<long, IReadOnlyList<SitemapAlternateLink>>> BuildPostVariantLookupAsync(
        string baseUrl,
        IReadOnlyList<long> TranslationGroupIds,
        CancellationToken ct)
    {
        if (TranslationGroupIds.Count == 0)
            return [];

        var TranslationGroupIdValues = TranslationGroupIds
            .Select(id => (long?)id)
            .ToArray();

        var variants = await _session.Query<PostDocument>()
            .Where(p => p.SiteId == _siteContext.SiteId
                     && p.PublicationState == ContentPublicationState.Published
                     && p.TranslationGroupId != null
                     && TranslationGroupIdValues.Contains(p.TranslationGroupId))
            .ToListAsync(ct);

        return variants
            .GroupBy(p => p.TranslationGroupId ?? p.Id)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<SitemapAlternateLink>)BuildPostAlternates(baseUrl, group));
    }

    private static List<SitemapAlternateLink> BuildPageAlternates(string baseUrl, IEnumerable<PageDocument> variants)
        => variants
            .GroupBy(p => p.Culture, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(p => new SitemapAlternateLink(
                p.Culture.ToLowerInvariant(),
                BuildCultureLoc(baseUrl, p.Culture, p.Slug, p.Kind is PageKind.Homepage)))
            .ToList();

    private static List<SitemapAlternateLink> BuildPostAlternates(string baseUrl, IEnumerable<PostDocument> variants)
        => variants
            .GroupBy(p => p.Culture, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(p => new SitemapAlternateLink(
                p.Culture.ToLowerInvariant(),
                BuildBlogPostLoc(baseUrl, p.Culture, p.Slug)))
            .ToList();

    private static List<SitemapAlternateLink> BuildDocAlternates(string baseUrl, IEnumerable<DocsPage> variants)
        => variants
            .GroupBy(doc => doc.Culture, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(doc => new SitemapAlternateLink(
                doc.Culture.ToLowerInvariant(),
                BuildCultureLoc(baseUrl, doc.Culture, doc.Slug, false)))
            .ToList();

    private string? GetBaseUrl()
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        return request is null ? null : $"{request.Scheme}://{request.Host}";
    }

    private IReadOnlyList<string> GetSupportedCultures()
    {
        var slice = _httpContextAccessor.HttpContext?.Features.Get<IAeroSiteSlice>();
        var defaultCulture = GetDefaultCulture();
        var cultures = slice?.SupportedCultures is { Count: > 0 } supportedCultures
            ? supportedCultures
            : [defaultCulture];

        var normalized = cultures
            .Select(culture => NormalizeCultureOrDefault(culture, defaultCulture))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!normalized.Contains(defaultCulture, StringComparer.OrdinalIgnoreCase))
            normalized.Insert(0, defaultCulture);

        return normalized;
    }

    private string GetDefaultCulture()
    {
        var slice = _httpContextAccessor.HttpContext?.Features.Get<IAeroSiteSlice>();
        return NormalizeCultureOrDefault(slice?.DefaultCulture, SitesModel.DefaultCultureName);
    }

    private static string NormalizeCultureOrDefault(string? culture, string fallback)
    {
        if (string.IsNullOrWhiteSpace(culture))
            return fallback;

        try
        {
            return CultureInfo.GetCultureInfo(culture.Trim()).Name;
        }
        catch (CultureNotFoundException)
        {
            return fallback;
        }
    }

    private static string BuildLoc(string baseUrl, string slug, bool isHomepage)
    {
        if (isHomepage) return baseUrl + "/";
        var cleanSlug = slug.Trim('/');
        return string.IsNullOrEmpty(cleanSlug) ? baseUrl + "/" : $"{baseUrl}/{cleanSlug}";
    }

    private static string BuildCultureLoc(string baseUrl, string culture, string slug, bool isHomepage)
    {
        var normalizedCulture = culture.ToLowerInvariant();
        if (isHomepage) return $"{baseUrl}/{normalizedCulture}/";
        var cleanSlug = slug.Trim('/');
        return string.IsNullOrEmpty(cleanSlug)
            ? $"{baseUrl}/{normalizedCulture}/"
            : $"{baseUrl}/{normalizedCulture}/{cleanSlug}";
    }

    private static string BuildBlogPostLoc(string baseUrl, string culture, string slug)
    {
        var cleanSlug = slug.Trim('/');
        return $"{baseUrl}/{culture.ToLowerInvariant()}/blog/{cleanSlug}";
    }

    private static string RenderSitemap(List<SitemapEntry> entries)
    {
        XNamespace xmlns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        XNamespace xhtml = "http://www.w3.org/1999/xhtml";

        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(xmlns + "urlset",
                new XAttribute(XNamespace.Xmlns + "xhtml", xhtml),
                from e in entries
                select new XElement(xmlns + "url",
                    new XElement(xmlns + "loc", e.Loc),
                    e.Alternates.Select(alternate =>
                        new XElement(xhtml + "link",
                            new XAttribute("rel", "alternate"),
                            new XAttribute("hreflang", alternate.Hreflang),
                            new XAttribute("href", alternate.Href))),
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

    private static string RenderSitemapIndex(IEnumerable<string> sitemapUrls)
    {
        XNamespace xmlns = "http://www.sitemaps.org/schemas/sitemap/0.9";

        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(xmlns + "sitemapindex",
                sitemapUrls.Select(url =>
                    new XElement(xmlns + "sitemap",
                        new XElement(xmlns + "loc", url)))));

        return doc.Declaration + "\n" + doc.ToString(SaveOptions.OmitDuplicateNamespaces);
    }
}
