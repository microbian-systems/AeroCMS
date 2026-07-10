using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aero.Cms.Modules.Pages;
using Aero.Cms.Modules.Posts;
using Aero.Core;
using AeroDB.Sable;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Setup.Services;

/// <summary>
/// Defines an interface for ITranslationImportService.
/// </summary>
public interface ITranslationImportService
{
        /// <summary>
    /// ImportAsync method.
    /// </summary>
Task<Result<TranslationImportResult, AeroError>> ImportAsync(
        TranslationImportFileRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a class for TranslationImportService.
/// </summary>
public sealed class TranslationImportService : ITranslationImportService
{
    private readonly IDocumentSession _session;
    private readonly ILogger<TranslationImportService> _log;

        /// <summary>
    /// Initializes a new instance of the <see cref="TranslationImportService"/> class.
    /// </summary>
public TranslationImportService(IDocumentSession session, ILogger<TranslationImportService> log)
    {
        _session = session;
        _log = log;
    }

        /// <summary>
    /// ImportAsync method.
    /// </summary>
public async Task<Result<TranslationImportResult, AeroError>> ImportAsync(
        TranslationImportFileRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Base64Data))
                return Prelude.Fail<TranslationImportResult, AeroError>(AeroError.CreateError("Uploaded file is empty"));

            byte[] fileData;
            try
            {
                fileData = Convert.FromBase64String(request.Base64Data);
            }
            catch (FormatException ex)
            {
                return Prelude.Fail<TranslationImportResult, AeroError>(
                    AeroError.CreateError($"Invalid Base64 data: {ex.Message}"));
            }

            if (fileData.Length == 0)
                return Prelude.Fail<TranslationImportResult, AeroError>(AeroError.CreateError("Uploaded file is empty"));

            var parseResult = await TranslationImportPayloadReader.ReadAsync(
                request.FileName,
                fileData,
                cancellationToken);

            if (parseResult is Result<List<TranslationImportPayload>, AeroError>.Failure parseFailure)
                return Prelude.Fail<TranslationImportResult, AeroError>(parseFailure.Error);

            if (parseResult is not Result<List<TranslationImportPayload>, AeroError>.Ok parseOk)
                return Prelude.Fail<TranslationImportResult, AeroError>(AeroError.CreateError("Unexpected parse result"));

            var payloads = parseOk.Value;
            if (payloads.Count == 0)
                return Prelude.Fail<TranslationImportResult, AeroError>(
                    AeroError.CreateError("No translation payloads found in the uploaded file"));

            var imported = new List<TranslationImportItem>();
            var updated = new List<TranslationImportItem>();
            var skipped = new List<TranslationImportSkip>();
            var errors = new List<TranslationImportError>();

            foreach (var payload in payloads)
            {
                var culture = ContentSlugDocument.NormalizeCulture(payload.Culture);

                foreach (var page in payload.Pages)
                    await ImportPageAsync(page, culture, imported, updated, skipped, errors, cancellationToken);

                foreach (var post in payload.Posts)
                    await ImportPostAsync(post, culture, imported, updated, skipped, errors, cancellationToken);

                foreach (var category in payload.Categories)
                    await UpsertCategoryTranslationAsync(category, culture, imported, updated, skipped, cancellationToken);

                foreach (var tag in payload.Tags)
                    await UpsertTagTranslationAsync(tag, culture, imported, updated, skipped, cancellationToken);

                foreach (var product in payload.Products)
                    await UpsertProductTranslationAsync(product, culture, imported, updated, skipped, cancellationToken);
            }

            if (imported.Count > 0 || updated.Count > 0)
                await _session.SaveChangesAsync(cancellationToken);

            return Prelude.Ok<TranslationImportResult, AeroError>(new TranslationImportResult(
                TotalProcessed: imported.Count + updated.Count + skipped.Count + errors.Count,
                TotalImported: imported.Count,
                TotalUpdated: updated.Count,
                TotalSkipped: skipped.Count,
                ImportedItems: imported,
                UpdatedItems: updated,
                SkippedItems: skipped,
                Errors: errors));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Translation import failed for file {FileName}", request.FileName);
            return Prelude.Fail<TranslationImportResult, AeroError>(
                AeroError.CreateError($"Translation import failed: {ex.Message}"));
        }
    }

    private async Task ImportPageAsync(
        TranslationPageImport page,
        string culture,
        List<TranslationImportItem> imported,
        List<TranslationImportItem> updated,
        List<TranslationImportSkip> skipped,
        List<TranslationImportError> errors,
        CancellationToken cancellationToken)
    {
        if (page.SourceId <= 0)
        {
            skipped.Add(new TranslationImportSkip("page", page.SourceId, "Missing source page id"));
            return;
        }

        var source = await _session.LoadAsync<PageDocument>(page.SourceId, cancellationToken);
        if (source is null)
        {
            errors.Add(new TranslationImportError("page", page.SourceId, "Source page not found"));
            return;
        }

        var TranslationGroupId = source.TranslationGroupId ?? source.Id;
        var existing = await _session.Query<PageDocument>()
            .FirstOrDefaultAsync(x =>
                x.SiteId == source.SiteId &&
                x.Culture == culture &&
                x.TranslationGroupId == TranslationGroupId,
                cancellationToken);

        var slug = ResolveSlug(page.Slug, source.Slug);
        if (existing is null)
        {
            var variant = PageCultureForker.Fork(source, Snowflake.NewId(), culture, slug);
            ApplyPageFields(variant, page);
            await ReserveSlugAsync(variant.Id, ContentSlugOwnerType.Page, slug, source.SiteId, culture, null, cancellationToken);
            _session.Store(variant);
            imported.Add(new TranslationImportItem("page", variant.Id, culture, slug));
            return;
        }

        var previousSlug = existing.Slug;
        existing.Slug = slug;
        existing.Path = "/" + slug.Trim().Trim('/');
        ApplyPageFields(existing, page);
        await ReserveSlugAsync(existing.Id, ContentSlugOwnerType.Page, slug, existing.SiteId, culture, previousSlug, cancellationToken);
        _session.Store(existing);
        updated.Add(new TranslationImportItem("page", existing.Id, culture, slug));
    }

    private async Task ImportPostAsync(
        TranslationPostImport post,
        string culture,
        List<TranslationImportItem> imported,
        List<TranslationImportItem> updated,
        List<TranslationImportSkip> skipped,
        List<TranslationImportError> errors,
        CancellationToken cancellationToken)
    {
        if (post.SourceId <= 0)
        {
            skipped.Add(new TranslationImportSkip("post", post.SourceId, "Missing source post id"));
            return;
        }

        var source = await _session.LoadAsync<PostDocument>(post.SourceId, cancellationToken);
        if (source is null)
        {
            errors.Add(new TranslationImportError("post", post.SourceId, "Source post not found"));
            return;
        }

        var TranslationGroupId = source.TranslationGroupId ?? source.Id;
        var existing = await _session.Query<PostDocument>()
            .FirstOrDefaultAsync(x =>
                x.SiteId == source.SiteId &&
                x.Culture == culture &&
                x.TranslationGroupId == TranslationGroupId,
                cancellationToken);

        var slug = ResolveSlug(post.Slug, source.Slug);
        if (existing is null)
        {
            var variant = PostCultureForker.Fork(source, Snowflake.NewId(), culture, slug);
            ApplyPostFields(variant, post);
            await ReserveSlugAsync(variant.Id, ContentSlugOwnerType.BlogPost, slug, source.SiteId, culture, null, cancellationToken);
            _session.Store(variant);
            imported.Add(new TranslationImportItem("post", variant.Id, culture, slug));
            return;
        }

        var previousSlug = existing.Slug;
        existing.Slug = slug;
        ApplyPostFields(existing, post);
        await ReserveSlugAsync(existing.Id, ContentSlugOwnerType.BlogPost, slug, existing.SiteId, culture, previousSlug, cancellationToken);
        _session.Store(existing);
        updated.Add(new TranslationImportItem("post", existing.Id, culture, slug));
    }

    private async Task UpsertCategoryTranslationAsync(
        TranslationCategoryImport category,
        string culture,
        List<TranslationImportItem> imported,
        List<TranslationImportItem> updated,
        List<TranslationImportSkip> skipped,
        CancellationToken cancellationToken)
    {
        if (category.CategoryId <= 0)
        {
            skipped.Add(new TranslationImportSkip("category", category.CategoryId, "Missing category id"));
            return;
        }

        var translation = await _session.Query<CategoryTranslation>()
            .FirstOrDefaultAsync(x => x.CategoryId == category.CategoryId && x.Culture == culture, cancellationToken);

        if (translation is null)
        {
            translation = new CategoryTranslation { Id = Snowflake.NewId(), CategoryId = category.CategoryId, Culture = culture };
            _session.Store(translation);
            imported.Add(new TranslationImportItem("category", category.CategoryId, culture, category.Slug ?? string.Empty));
        }
        else
        {
            updated.Add(new TranslationImportItem("category", category.CategoryId, culture, category.Slug ?? string.Empty));
        }

        translation.Name = category.Name ?? string.Empty;
        translation.Slug = category.Slug ?? string.Empty;
        translation.Description = category.Description;
    }

    private async Task UpsertTagTranslationAsync(
        TranslationTagImport tag,
        string culture,
        List<TranslationImportItem> imported,
        List<TranslationImportItem> updated,
        List<TranslationImportSkip> skipped,
        CancellationToken cancellationToken)
    {
        if (tag.TagId <= 0)
        {
            skipped.Add(new TranslationImportSkip("tag", tag.TagId, "Missing tag id"));
            return;
        }

        var translation = await _session.Query<TagTranslation>()
            .FirstOrDefaultAsync(x => x.TagId == tag.TagId && x.Culture == culture, cancellationToken);

        if (translation is null)
        {
            translation = new TagTranslation { Id = Snowflake.NewId(), TagId = tag.TagId, Culture = culture };
            _session.Store(translation);
            imported.Add(new TranslationImportItem("tag", tag.TagId, culture, string.Empty));
        }
        else
        {
            updated.Add(new TranslationImportItem("tag", tag.TagId, culture, string.Empty));
        }

        translation.Name = tag.Name ?? string.Empty;
        translation.Description = tag.Description;
    }

    private async Task UpsertProductTranslationAsync(
        TranslationProductImport product,
        string culture,
        List<TranslationImportItem> imported,
        List<TranslationImportItem> updated,
        List<TranslationImportSkip> skipped,
        CancellationToken cancellationToken)
    {
        if (product.ProductId <= 0)
        {
            skipped.Add(new TranslationImportSkip("product", product.ProductId, "Missing product id"));
            return;
        }

        var translation = await _session.Query<ProductTranslation>()
            .FirstOrDefaultAsync(x => x.ProductId == product.ProductId && x.Culture == culture, cancellationToken);

        if (translation is null)
        {
            translation = new ProductTranslation { Id = Snowflake.NewId(), ProductId = product.ProductId, Culture = culture };
            _session.Store(translation);
            imported.Add(new TranslationImportItem("product", product.ProductId, culture, string.Empty));
        }
        else
        {
            updated.Add(new TranslationImportItem("product", product.ProductId, culture, string.Empty));
        }

        translation.Name = product.Name ?? string.Empty;
        translation.Description = product.Description;
        translation.ShortDescription = product.ShortDescription;
    }

    private async Task ReserveSlugAsync(
        long ownerId,
        ContentSlugOwnerType ownerType,
        string slug,
        long siteId,
        string culture,
        string? previousSlug,
        CancellationToken cancellationToken)
        => await ContentSlugReservation.ReserveAsync(
            _session,
            ownerId,
            ownerType,
            slug,
            siteId,
            culture,
            previousSlug,
            cancellationToken);

    private static void ApplyPageFields(PageDocument page, TranslationPageImport import)
    {
        if (!string.IsNullOrWhiteSpace(import.Title))
            page.Title = import.Title;
        if (import.Summary is not null)
            page.Summary = import.Summary;
        if (import.SeoTitle is not null)
            page.SeoTitle = import.SeoTitle;
        if (import.SeoDescription is not null)
            page.SeoDescription = import.SeoDescription;
        page.ModifiedOn = DateTimeOffset.UtcNow;
    }

    private static void ApplyPostFields(PostDocument post, TranslationPostImport import)
    {
        if (!string.IsNullOrWhiteSpace(import.Title))
            post.Title = import.Title;
        if (import.Excerpt is not null)
            post.Excerpt = import.Excerpt;
        if (import.SeoTitle is not null)
            post.SeoTitle = import.SeoTitle;
        if (import.SeoDescription is not null)
            post.SeoDescription = import.SeoDescription;
        post.ModifiedOn = DateTimeOffset.UtcNow;
    }

    private static string ResolveSlug(string? importedSlug, string sourceSlug)
        => string.IsNullOrWhiteSpace(importedSlug)
            ? sourceSlug.Trim().Trim('/')
            : importedSlug.Trim().Trim('/');
}

internal static class TranslationImportPayloadReader
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

        /// <summary>
    /// ReadAsync method.
    /// </summary>
public static async Task<Result<List<TranslationImportPayload>, AeroError>> ReadAsync(
        string fileName,
        byte[] fileData,
        CancellationToken cancellationToken)
    {
        if (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return await ReadJsonAsync(fileData, fileName, cancellationToken);

        if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return await ReadZipAsync(fileData, cancellationToken);

        return Prelude.Fail<List<TranslationImportPayload>, AeroError>(
            AeroError.CreateError($"Unsupported file type: '{fileName}'. Accepted: .json, .zip"));
    }

    private static async Task<Result<List<TranslationImportPayload>, AeroError>> ReadJsonAsync(
        byte[] fileData,
        string fileName,
        CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream(fileData);
        return await DeserializePayloadsAsync(stream, fileName, cancellationToken);
    }

    private static async Task<Result<List<TranslationImportPayload>, AeroError>> ReadZipAsync(
        byte[] fileData,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new MemoryStream(fileData);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            var payloads = new List<TranslationImportPayload>();
            var errors = new List<string>();

            foreach (var entry in archive.Entries)
            {
                if (!entry.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    continue;

                var entryFullName = entry.FullName.Replace('\\', '/');
                if (entryFullName.Contains(".."))
                {
                    errors.Add($"Skipped '{entry.Name}': path traversal detected");
                    continue;
                }

                await using var entryStream = entry.Open();
                var result = await DeserializePayloadsAsync(entryStream, entry.Name, cancellationToken);
                if (result is Result<List<TranslationImportPayload>, AeroError>.Ok ok)
                {
                    payloads.AddRange(ok.Value);
                    continue;
                }

                if (result is Result<List<TranslationImportPayload>, AeroError>.Failure failure)
                    errors.Add(failure.Error.ToString());
            }

            if (payloads.Count == 0 && errors.Count > 0)
                return Prelude.Fail<List<TranslationImportPayload>, AeroError>(
                    AeroError.CreateError(string.Join("; ", errors)));

            return Prelude.Ok<List<TranslationImportPayload>, AeroError>(payloads);
        }
        catch (InvalidDataException ex)
        {
            return Prelude.Fail<List<TranslationImportPayload>, AeroError>(
                AeroError.CreateError($"Invalid or corrupted ZIP file: {ex.Message}"));
        }
    }

    private static async Task<Result<List<TranslationImportPayload>, AeroError>> DeserializePayloadsAsync(
        Stream stream,
        string fileName,
        CancellationToken cancellationToken)
    {
        try
        {
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var json = doc.RootElement.GetRawText();

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                var payloads = JsonSerializer.Deserialize<List<TranslationImportPayload>>(json, JsonOpts) ?? [];
                return Prelude.Ok<List<TranslationImportPayload>, AeroError>(payloads);
            }

            var payload = JsonSerializer.Deserialize<TranslationImportPayload>(json, JsonOpts);
            return Prelude.Ok<List<TranslationImportPayload>, AeroError>(payload is null ? [] : [payload]);
        }
        catch (JsonException ex)
        {
            return Prelude.Fail<List<TranslationImportPayload>, AeroError>(
                AeroError.CreateError($"Invalid JSON in '{fileName}': {ex.Message}"));
        }
    }
}

/// <summary>
/// Represents a record for TranslationImportFileRequest.
/// </summary>
public sealed record TranslationImportFileRequest(
    string FileName,
    string MimeType,
    string Base64Data);

/// <summary>
/// Represents a record for TranslationImportResult.
/// </summary>
public sealed record TranslationImportResult(
    int TotalProcessed,
    int TotalImported,
    int TotalUpdated,
    int TotalSkipped,
    IReadOnlyList<TranslationImportItem> ImportedItems,
    IReadOnlyList<TranslationImportItem> UpdatedItems,
    IReadOnlyList<TranslationImportSkip> SkippedItems,
    IReadOnlyList<TranslationImportError> Errors);

/// <summary>
/// Represents a record for TranslationImportItem.
/// </summary>
public sealed record TranslationImportItem(string Type, long Id, string Culture, string Slug);

/// <summary>
/// Represents a record for TranslationImportSkip.
/// </summary>
public sealed record TranslationImportSkip(string Type, long Id, string Reason);

/// <summary>
/// Represents a record for TranslationImportError.
/// </summary>
public sealed record TranslationImportError(string Type, long Id, string Message);

/// <summary>
/// Represents a record for TranslationImportPayload.
/// </summary>
public sealed record TranslationImportPayload
{
        /// <summary>
    /// Gets or sets the Culture.
    /// </summary>
[JsonPropertyName("culture")]
    public string Culture { get; init; } = SitesModel.DefaultCultureName;

        /// <summary>
    /// Gets or sets the Pages.
    /// </summary>
[JsonPropertyName("pages")]
    public List<TranslationPageImport> Pages { get; init; } = [];

        /// <summary>
    /// Gets or sets the Posts.
    /// </summary>
[JsonPropertyName("posts")]
    public List<TranslationPostImport> Posts { get; init; } = [];

        /// <summary>
    /// Gets or sets the Categories.
    /// </summary>
[JsonPropertyName("categories")]
    public List<TranslationCategoryImport> Categories { get; init; } = [];

        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
[JsonPropertyName("tags")]
    public List<TranslationTagImport> Tags { get; init; } = [];

        /// <summary>
    /// Gets or sets the Products.
    /// </summary>
[JsonPropertyName("products")]
    public List<TranslationProductImport> Products { get; init; } = [];
}

/// <summary>
/// Represents a record for TranslationPageImport.
/// </summary>
public sealed record TranslationPageImport
{
        /// <summary>
    /// Gets or sets the Source Id.
    /// </summary>
[JsonPropertyName("sourceId")]
    public long SourceId { get; init; }

        /// <summary>
    /// Gets or sets the Slug.
    /// </summary>
[JsonPropertyName("slug")]
    public string? Slug { get; init; }

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
[JsonPropertyName("title")]
    public string? Title { get; init; }

        /// <summary>
    /// Gets or sets the Summary.
    /// </summary>
[JsonPropertyName("summary")]
    public string? Summary { get; init; }

        /// <summary>
    /// Gets or sets the Seo Title.
    /// </summary>
[JsonPropertyName("seoTitle")]
    public string? SeoTitle { get; init; }

        /// <summary>
    /// Gets or sets the Seo Description.
    /// </summary>
[JsonPropertyName("seoDescription")]
    public string? SeoDescription { get; init; }
}

/// <summary>
/// Represents a record for TranslationPostImport.
/// </summary>
public sealed record TranslationPostImport
{
        /// <summary>
    /// Gets or sets the Source Id.
    /// </summary>
[JsonPropertyName("sourceId")]
    public long SourceId { get; init; }

        /// <summary>
    /// Gets or sets the Slug.
    /// </summary>
[JsonPropertyName("slug")]
    public string? Slug { get; init; }

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
[JsonPropertyName("title")]
    public string? Title { get; init; }

        /// <summary>
    /// Gets or sets the Excerpt.
    /// </summary>
[JsonPropertyName("excerpt")]
    public string? Excerpt { get; init; }

        /// <summary>
    /// Gets or sets the Seo Title.
    /// </summary>
[JsonPropertyName("seoTitle")]
    public string? SeoTitle { get; init; }

        /// <summary>
    /// Gets or sets the Seo Description.
    /// </summary>
[JsonPropertyName("seoDescription")]
    public string? SeoDescription { get; init; }
}

/// <summary>
/// Represents a record for TranslationCategoryImport.
/// </summary>
public sealed record TranslationCategoryImport
{
        /// <summary>
    /// Gets or sets the Category Id.
    /// </summary>
[JsonPropertyName("categoryId")]
    public long CategoryId { get; init; }

        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
[JsonPropertyName("name")]
    public string? Name { get; init; }

        /// <summary>
    /// Gets or sets the Slug.
    /// </summary>
[JsonPropertyName("slug")]
    public string? Slug { get; init; }

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
[JsonPropertyName("description")]
    public string? Description { get; init; }
}

/// <summary>
/// Represents a record for TranslationTagImport.
/// </summary>
public sealed record TranslationTagImport
{
        /// <summary>
    /// Gets or sets the Tag Id.
    /// </summary>
[JsonPropertyName("tagId")]
    public long TagId { get; init; }

        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
[JsonPropertyName("name")]
    public string? Name { get; init; }

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
[JsonPropertyName("description")]
    public string? Description { get; init; }
}

/// <summary>
/// Represents a record for TranslationProductImport.
/// </summary>
public sealed record TranslationProductImport
{
        /// <summary>
    /// Gets or sets the Product Id.
    /// </summary>
[JsonPropertyName("productId")]
    public long ProductId { get; init; }

        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
[JsonPropertyName("name")]
    public string? Name { get; init; }

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
[JsonPropertyName("description")]
    public string? Description { get; init; }

        /// <summary>
    /// Gets or sets the Short Description.
    /// </summary>
[JsonPropertyName("shortDescription")]
    public string? ShortDescription { get; init; }
}
