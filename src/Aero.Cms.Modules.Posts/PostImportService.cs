using System.Text.Json;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Enums;
using Aero.Core.Http;
using Aero.Cms.Modules.Posts.Parsers;
using Aero.Services.Images;

namespace Aero.Cms.Modules.Posts;

/// <summary>
/// Orchestrates the blog post import pipeline: file parsing, tag resolution,
/// Pexels image search, slug dedup, and batch persistence.
/// </summary>
public interface IPostImportService
{
    /// <summary>
    /// Imports blog posts from an uploaded file (Base64-encoded).
    /// </summary>
    /// <param name="request">The import request with file content and options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result with counts of imported, skipped, and errored posts.</returns>
    Task<Result<ImportBlogResult, AeroError>> ImportAsync(
        ImportFileRequest request, CancellationToken ct = default);
}

public sealed class PostsImportService : IPostImportService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string PlaceholderImage = "/img/placeholder_1600x500.webp";
    private const string BlogImportFolder = "blog-import";
    private const int MaxPexelsRetries = 2;
    private const int PexelsConcurrency = 3;

    private readonly IEnumerable<IPostImportParser> _parsers;
    private readonly IDocumentSession _session;
    private readonly IPexelsService? _pexels;
    private readonly ISiteContext _siteContext;
    private readonly ILogger<PostsImportService> _log;

    public PostsImportService(
        IEnumerable<IPostImportParser> parsers,
        IDocumentSession session,
        IPexelsService? pexels,
        ISiteContext siteContext,
        ILogger<PostsImportService> log)
    {
        _parsers = parsers;
        _session = session;
        _pexels = pexels;
        _siteContext = siteContext;
        _log = log;
    }

    /// <inheritdoc />
    public async Task<Result<ImportBlogResult, AeroError>> ImportAsync(
        ImportFileRequest request, CancellationToken ct = default)
    {
        try
        {
            // 1. Decode the file
            byte[] fileData;
            try
            {
                fileData = Convert.FromBase64String(request.Base64Data);
            }
            catch (FormatException ex)
            {
                return Prelude.Fail<ImportBlogResult, AeroError>(
                    AeroError.CreateError($"Invalid Base64 data: {ex.Message}"));
            }

            if (fileData.Length == 0)
            {
                return Prelude.Fail<ImportBlogResult, AeroError>(
                    AeroError.CreateError("Uploaded file is empty"));
            }

            // 2. Find the right parser
            var parser = _parsers.FirstOrDefault(p => p.Supports(request.FileName));
            if (parser is null)
            {
                return Prelude.Fail<ImportBlogResult, AeroError>(
                    AeroError.CreateError($"Unsupported file type: '{request.FileName}'. Accepted: .json, .md, .zip"));
            }

            // 3. Parse the file
            using var fileStream = new MemoryStream(fileData);
            var parseResult = await parser.ParseAsync(fileStream, request.FileName, ct);
            if (parseResult is Result<List<ImportablePost>, AeroError>.Failure parseFail)
            {
                return Prelude.Fail<ImportBlogResult, AeroError>(parseFail.Error);
            }

            if (parseResult is not Result<List<ImportablePost>, AeroError>.Ok parseOk)
            {
                return Prelude.Fail<ImportBlogResult, AeroError>(
                    AeroError.CreateError("Unexpected parse result"));
            }

            // Fallback: if the client didn't provide a SiteId (e.g. from NoopSiteContext in WASM),
            // stamp it from the server-side ISiteContext (reads cookie / IAeroSiteSlice).
            var effectiveSiteId = request.SiteId > 0 ? request.SiteId : _siteContext.SiteId;
            request = request with { SiteId = effectiveSiteId };

            var importablePosts = parseOk.Value;
            if (importablePosts.Count == 0)
            {
                return Prelude.Fail<ImportBlogResult, AeroError>(
                    AeroError.CreateError("No blog posts found in the uploaded file"));
            }

            // Log parsed data for diagnostics
            _log.LogInformation("[Import] Parsed {Count} posts from '{File}'", importablePosts.Count, request.FileName);
            foreach (var p in importablePosts.Take(3))
            {
                _log.LogInformation("[Import] Parsed: slug={Slug}, rawDate='{RawDate}', parsedDate={ParsedDate}",
                    p.Slug, p.PublishedOn?.ToString("o") ?? "<null>", p.PublishedOn?.ToString("yyyy-MM-dd") ?? "<null>");
            }

            // 4. Batch resolve tags across all posts
            var tagMap = await ResolveTagsAsync(importablePosts, ct);
            var generalSeriesId = await EnsureGeneralSeriesIdAsync(request.SiteId, ct);

            // 5. Pre-check existing slugs
            var existingSlugs = await GetExistingSlugsAsync(importablePosts, ct);

            // 6. Determine duplicate behavior
            var behavior = (request.DuplicateBehavior ?? DuplicateSlugBehavior.Skip).ToLowerInvariant();

            // 7. Process posts (resolve images in parallel)
            var importedPosts = new List<PostDocument>();
            var skippedPosts = new List<SkippedPostInfo>();
            var errors = new List<ImportError>();
            var slugReservations = new List<ContentSlugDocument>();

            // Parallel image resolution with throttling
            using var pexelsSemaphore = new SemaphoreSlim(PexelsConcurrency);

            var imageTasks = importablePosts.Select(post => 
                ResolveImageAsync(post, behavior == DuplicateSlugBehavior.Overwrite, pexelsSemaphore, ct));
            var imageResults = await Task.WhenAll(imageTasks);

            for (int i = 0; i < importablePosts.Count; i++)
            {
                var post = importablePosts[i];
                var resolvedImageUrl = imageResults[i];

                // Check slug
                var normalizedSlug = ContentSlugDocument.Normalize(post.Slug);
                var existingSlug = existingSlugs.FirstOrDefault(s => s.NormalizedSlug == normalizedSlug);

                if (existingSlug is not null)
                {
                    switch (behavior)
                    {
                        case DuplicateSlugBehavior.Skip:
                            skippedPosts.Add(new SkippedPostInfo(post.Slug, "Slug already exists"));
                            continue;

                        case DuplicateSlugBehavior.Suffix:
                            // Find next available suffix
                            var suffix = 2;
                            string suffixedSlug;
                            do
                            {
                                suffixedSlug = $"{post.Slug}-{suffix}";
                                suffix++;
                            } while (existingSlugs.Any(s => s.NormalizedSlug == ContentSlugDocument.Normalize(suffixedSlug)));
                            post = post with { Slug = suffixedSlug };
                            break;

                        case DuplicateSlugBehavior.Overwrite:
                            // Will replace existing content below
                            break;
                    }
                }

                try
                {
                    var postId = Snowflake.NewId();
                    var now = DateTimeOffset.UtcNow;

                    // Handle overwrite: load and update existing post
                    PostDocument document;
                    ContentSlugDocument? slugDoc = null;

                    if (existingSlug is not null && behavior == DuplicateSlugBehavior.Overwrite)
                    {
                        var existingDoc = await _session.LoadAsync<PostDocument>(existingSlug.OwnerId, ct);
                        if (existingDoc is not null)
                        {
                            // Remove old slug reservation for this owner
                            var oldSlugDoc = await _session.Query<ContentSlugDocument>()
                                .FirstOrDefaultAsync(s =>
                                    s.SiteId == request.SiteId &&
                                    s.OwnerId == existingDoc.Id &&
                                    s.OwnerType == ContentSlugOwnerType.BlogPost, ct);
                            if (oldSlugDoc is not null)
                                _session.Delete(oldSlugDoc);

                            document = existingDoc;
                            document.Title = post.Title;
                            document.Slug = post.Slug;
                            document.Content = CreateContentBlocks(post.MarkdownContent);
                            document.ImageUrl = resolvedImageUrl;
                            document.PublicationState = request.PublishImported
                                ? ContentPublicationState.Published
                                : ContentPublicationState.Draft;
                            document.PublishedOn = request.PublishImported ? post.PublishedOn ?? now : null;
                            document.ModifiedOn = now;
                            document.TagIds = post.Tags.Select(t => tagMap.GetValueOrDefault(t.ToLowerInvariant(), 0L))
                                .Where(id => id > 0).ToList();
                            document.SeriesId ??= generalSeriesId;
                            if (request.DefaultAuthorId.HasValue)
                                document.AuthorId = request.DefaultAuthorId;
                        }
                        else
                        {
                            // Existing slug but no document — treat as new
                            document = CreateNewPost(post, postId, now, resolvedImageUrl, tagMap, request, generalSeriesId);
                        }
                    }
                    else
                    {
                        _log.LogInformation("[Import] Creating: slug={Slug}, parsedPublishedOn={Parsed}, publishFlag={Publish}",
                            post.Slug, post.PublishedOn?.ToString("yyyy-MM-dd") ?? "<null>", request.PublishImported);
                        document = CreateNewPost(post, postId, now, resolvedImageUrl, tagMap, request, generalSeriesId);
                        _log.LogInformation("[Import] Created: slug={Slug}, doc.PublishedOn={DocPub}, doc.CreatedOn={DocCreated}",
                            document.Slug, document.PublishedOn?.ToString("yyyy-MM-dd") ?? "<null>",
                            document.CreatedOn.ToString("yyyy-MM-dd"));
                    }

                    // Create slug reservation
                    if (existingSlug is null || behavior != DuplicateSlugBehavior.Skip)
                    {
                        slugDoc = ContentSlugDocument.Create(post.Slug, document.Id, ContentSlugOwnerType.BlogPost, request.SiteId);
                        slugReservations.Add(slugDoc);
                    }

                    importedPosts.Add(document);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Failed to process import for post '{Title}' (slug: {Slug})", post.Title, post.Slug);
                    errors.Add(new ImportError(post.Slug, $"Processing error: {ex.Message}"));
                }
            }

            // 8. Batch save
            if (importedPosts.Count > 0)
            {
                foreach (var doc in importedPosts)
                    _session.Store(doc);
                foreach (var res in slugReservations)
                    _session.Store(res);

                await _session.SaveChangesAsync(ct);
            }

            // 9. Build result
            var result = new ImportBlogResult(
                TotalProcessed: importablePosts.Count,
                TotalImported: importedPosts.Count,
                TotalSkipped: skippedPosts.Count,
                ImportedPosts: importedPosts.Select(p => new ImportedPostSummary(p.Id, p.Slug, p.Title)).ToList(),
                SkippedPosts: skippedPosts,
                Errors: errors
            );

            return Prelude.Ok<ImportBlogResult, AeroError>(result);
        }
        catch (OperationCanceledException)
        {
            return Prelude.Fail<ImportBlogResult, AeroError>(
                AeroError.CreateError("Import was cancelled"));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unexpected error during blog import");
            return Prelude.Fail<ImportBlogResult, AeroError>(
                AeroError.CreateError($"Import failed: {ex.Message}"));
        }
    }

    // ─── Helpers ─────────────────────────────────────────────

    private static PostDocument CreateNewPost(
        ImportablePost post, long postId, DateTimeOffset now,
        string? imageUrl, Dictionary<string, long> tagMap,
        ImportFileRequest request,
        long generalSeriesId)
    {
        return new PostDocument
        {
            Id = postId,
            SiteId = request.SiteId,
            Title = post.Title,
            Slug = post.Slug,
            Excerpt = post.MarkdownContent.Length > 500
                ? post.MarkdownContent[..500] + "..."
                : post.MarkdownContent,
            Content = CreateContentBlocks(post.MarkdownContent),
            ImageUrl = imageUrl,
            PublicationState = request.PublishImported
                ? ContentPublicationState.Published
                : ContentPublicationState.Draft,
            PublishedOn = request.PublishImported ? post.PublishedOn ?? now : null,
            CreatedOn = post.PublishedOn ?? now,
            ModifiedOn = post.PublishedOn ?? now,
            TagIds = post.Tags
                .Select(t => tagMap.GetValueOrDefault(t.ToLowerInvariant(), 0L))
                .Where(id => id > 0)
                .ToList(),
            SeriesId = generalSeriesId,
            AuthorId = request.DefaultAuthorId
        };
    }

    private static List<BlockBase> CreateContentBlocks(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return [];

        return
        [
            new MarkdownBlock
            {
                Id = Snowflake.NewId(),
                Content = markdown,
                Order = 0
            }
        ];
    }

    private async Task<Dictionary<string, long>> ResolveTagsAsync(
        List<ImportablePost> posts, CancellationToken ct)
    {
        // Collect all unique tag strings
        var allTags = posts
            .SelectMany(p => p.Tags)
            .Select(t => t.Trim().ToLowerInvariant())
            .Where(t => !string.IsNullOrEmpty(t))
            .Distinct()
            .ToList();

        if (allTags.Count == 0)
            return [];

        // Query existing tags
        var existingTags = await _session.Query<Tag>()
            .Where(t => t.SiteId == _siteContext.SiteId && allTags.Contains(t.Slug))
            .ToListAsync(ct);

        var tagMap = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in existingTags)
            tagMap[tag.Slug] = tag.Id;

        // Create missing tags
        var newTags = new List<Tag>();
        foreach (var tagSlug in allTags)
        {
            if (!tagMap.ContainsKey(tagSlug))
            {
                var tag = new Tag
                {
                    Id = Snowflake.NewId(),
                    Name = tagSlug,  // Use slug as name if no display name available
                    Slug = tagSlug,
                    SiteId = _siteContext.SiteId
                };
                tagMap[tagSlug] = tag.Id;
                newTags.Add(tag);
            }
        }

        if (newTags.Count > 0)
        {
            foreach (var tag in newTags)
                _session.Store(tag);
        }

        return tagMap;
    }

    private async Task<long> EnsureGeneralSeriesIdAsync(long siteId, CancellationToken ct)
    {
        var general = await _session.Query<Series>()
            .FirstOrDefaultAsync(x => x.SiteId == siteId && x.Slug == "general", ct);

        if (general is not null)
            return general.Id;

        general = new Series
        {
            Id = Snowflake.NewId(),
            SiteId = siteId,
            Name = "General",
            Slug = "general",
            Description = "Default blog series"
        };

        _session.Store(general);
        return general.Id;
    }

    private async Task<IReadOnlyList<ContentSlugDocument>> GetExistingSlugsAsync(
        List<ImportablePost> posts, CancellationToken ct)
    {
        var slugs = posts
            .Select(p => ContentSlugDocument.Normalize(p.Slug))
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct()
            .ToList();

        if (slugs.Count == 0)
            return [];

        return await _session.Query<ContentSlugDocument>()
            .Where(s =>
                s.SiteId == _siteContext.SiteId &&
                slugs.Contains(s.NormalizedSlug) &&
                s.OwnerType == ContentSlugOwnerType.BlogPost)
            .ToListAsync(ct);
    }

    private async Task<string?> ResolveImageAsync(
        ImportablePost post, bool isOverwrite,
        SemaphoreSlim semaphore, CancellationToken ct)
    {
        // 1. Use coverImage from the import JSON if provided
        if (!string.IsNullOrEmpty(post.CoverImage))
            return post.CoverImage;

        // 2. Try Pexels (throttled) when no coverImage in the import
        if (_pexels is not null)
        {
            await semaphore.WaitAsync(ct);
            try
            {
                return await SearchPexelsWithRetryAsync(post, ct);
            }
            finally
            {
                semaphore.Release();
            }
        }

        // 3. Fallback
        return PlaceholderImage;
    }

    private async Task<string?> SearchPexelsWithRetryAsync(ImportablePost post, CancellationToken ct)
    {
        // Pexels requires a non-empty query
        if (string.IsNullOrWhiteSpace(post.Title))
        {
            _log.LogWarning("Skipping Pexels search — post has no title");
            return PlaceholderImage;
        }
        for (int attempt = 0; attempt <= MaxPexelsRetries; attempt++)
        {
            try
            {
                var photos = await _pexels!.SearchPhotosAsync(post.Title, count: 1, orientation: "landscape", ct);
                if (photos.Count > 0)
                {
                    var photo = photos[0];
                    // Return the best landscape-sized URL
                    return photo.Src.Large2x ?? photo.Src.Large ?? photo.Src.Original;
                }

                // No results — don't retry, use placeholder
                break;
            }
            catch (Exception ex) when (attempt < MaxPexelsRetries)
            {
                _log.LogWarning(ex, "Pexels search attempt {Attempt}/{Max} failed for '{Title}'",
                    attempt + 1, MaxPexelsRetries + 1, post.Title);
                await Task.Delay(1000 * (attempt + 1), ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Pexels search exhausted for '{Title}' after {Max} attempts",
                    post.Title, MaxPexelsRetries + 1);
            }
        }

        return PlaceholderImage;
    }
}
