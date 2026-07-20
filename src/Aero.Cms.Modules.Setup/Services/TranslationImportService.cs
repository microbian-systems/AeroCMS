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
/// Imports culture-specific variants and translation records from a JSON or ZIP payload.
/// </summary>
public interface ITranslationImportService
{
    /// <summary>
    /// Parses, validates, and applies a translation import as one document-session save.
    /// </summary>
    /// <param name="request">The file metadata and Base64-encoded content.</param>
    /// <param name="cancellationToken">Cancels parsing, database queries, slug reservations, or persistence.</param>
    /// <returns>
    /// A successful aggregate result when the file can be processed, including per-item skips
    /// and missing-source errors; otherwise a failure describing a file-level or unexpected error.
    /// </returns>
Task<Result<TranslationImportResult, AeroError>> ImportAsync(
        TranslationImportFileRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Applies translation imports to pages, posts, categories, tags, and products in an AeroDB document session.
/// </summary>
/// <remarks>
/// Page and post imports require an existing source document and reserve localized slugs.
/// Re-importing the same translation group and culture updates the existing variant. Changes
/// are saved once after all payloads have been processed. The method catches all exceptions,
/// including cancellation, logs them, and returns a failed result.
/// </remarks>
public sealed class TranslationImportService : ITranslationImportService
{
    private readonly IDocumentSession _session;
    private readonly ILogger<TranslationImportService> _log;

    /// <summary>
    /// Initializes an importer over the supplied document session.
    /// </summary>
    /// <param name="session">The session used for queries, stores, slug reservations, and the final save.</param>
    /// <param name="log">The logger used for file-level failures.</param>
public TranslationImportService(IDocumentSession session, ILogger<TranslationImportService> log)
    {
        _session = session;
        _log = log;
    }

    /// <inheritdoc />
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

    /// <summary>
    /// Forks a source page into a culture or updates the existing translation-group variant.
    /// </summary>
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

    /// <summary>
    /// Forks a source post into a culture or updates the existing translation-group variant.
    /// </summary>
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

    /// <summary>
    /// Creates or updates a category translation identified by category and culture.
    /// </summary>
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

    /// <summary>
    /// Creates or updates a tag translation identified by tag and culture.
    /// </summary>
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

    /// <summary>
    /// Creates or updates a product translation identified by product and culture.
    /// </summary>
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

    /// <summary>
    /// Reserves the localized slug and releases the previous reservation when it changes.
    /// </summary>
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

    /// <summary>
    /// Applies only supplied page fields and refreshes the modification timestamp.
    /// </summary>
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

    /// <summary>
    /// Applies only supplied post fields and refreshes the modification timestamp.
    /// </summary>
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

    /// <summary>
    /// Normalizes an imported slug or falls back to the normalized source slug.
    /// </summary>
    private static string ResolveSlug(string? importedSlug, string sourceSlug)
        => string.IsNullOrWhiteSpace(importedSlug)
            ? sourceSlug.Trim().Trim('/')
            : importedSlug.Trim().Trim('/');
}

/// <summary>
/// Parses translation payloads from JSON files or JSON entries in ZIP archives.
/// </summary>
internal static class TranslationImportPayloadReader
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Dispatches parsing based on a case-insensitive <c>.json</c> or <c>.zip</c> file-name extension.
    /// </summary>
    /// <param name="fileName">The uploaded file name used for format selection and error context.</param>
    /// <param name="fileData">The decoded file bytes.</param>
    /// <param name="cancellationToken">Cancels JSON stream parsing.</param>
    /// <returns>The parsed payloads, or a failure for an unsupported extension or malformed content.</returns>
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

    /// <summary>
    /// Parses one JSON document that may contain either a payload object or an array.
    /// </summary>
    private static async Task<Result<List<TranslationImportPayload>, AeroError>> ReadJsonAsync(
        byte[] fileData,
        string fileName,
        CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream(fileData);
        return await DeserializePayloadsAsync(stream, fileName, cancellationToken);
    }

    /// <summary>
    /// Parses JSON file entries from a ZIP archive, ignoring non-JSON entries.
    /// </summary>
    /// <remarks>
    /// Entry names containing <c>..</c> are skipped. When at least one entry parses,
    /// errors from other entries do not make the overall parse fail.
    /// </remarks>
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

    /// <summary>
    /// Deserializes a JSON object or array with case-insensitive property matching.
    /// </summary>
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
/// Carries an uploaded translation file as Base64 text.
/// </summary>
/// <remarks><see cref="MimeType"/> is descriptive; format selection uses <see cref="FileName"/>.</remarks>
public sealed record TranslationImportFileRequest(
    string FileName,
    string MimeType,
    string Base64Data);

/// <summary>
/// Summarizes the persisted, updated, skipped, and rejected items in an import.
/// </summary>
/// <remarks>
/// <see cref="TotalProcessed"/> is the sum of imported, updated, skipped, and error items.
/// A successful result may still contain item-level errors.
/// </remarks>
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
/// Identifies a translation that was created or updated.
/// </summary>
public sealed record TranslationImportItem(string Type, long Id, string Culture, string Slug);

/// <summary>
/// Identifies an input item that was intentionally skipped before persistence.
/// </summary>
public sealed record TranslationImportSkip(string Type, long Id, string Reason);

/// <summary>
/// Identifies an input item that could not be imported.
/// </summary>
public sealed record TranslationImportError(string Type, long Id, string Message);

/// <summary>
/// Represents one culture's translation import collections.
/// </summary>
public sealed record TranslationImportPayload
{
    /// <summary>
    /// Gets the target culture, defaulting to the site model's default culture.
    /// </summary>
[JsonPropertyName("culture")]
    public string Culture { get; init; } = SitesModel.DefaultCultureName;

    /// <summary>
    /// Gets page translations to fork or update.
    /// </summary>
[JsonPropertyName("pages")]
    public List<TranslationPageImport> Pages { get; init; } = [];

    /// <summary>
    /// Gets post translations to fork or update.
    /// </summary>
[JsonPropertyName("posts")]
    public List<TranslationPostImport> Posts { get; init; } = [];

    /// <summary>
    /// Gets category translation records to upsert.
    /// </summary>
[JsonPropertyName("categories")]
    public List<TranslationCategoryImport> Categories { get; init; } = [];

    /// <summary>
    /// Gets tag translation records to upsert.
    /// </summary>
[JsonPropertyName("tags")]
    public List<TranslationTagImport> Tags { get; init; } = [];

    /// <summary>
    /// Gets product translation records to upsert.
    /// </summary>
[JsonPropertyName("products")]
    public List<TranslationProductImport> Products { get; init; } = [];
}

/// <summary>
/// Describes translated fields for a page identified by its source document.
/// </summary>
public sealed record TranslationPageImport
{
    /// <summary>
    /// Gets the source page identifier used to locate the translation group.
    /// </summary>
[JsonPropertyName("sourceId")]
    public long SourceId { get; init; }

    /// <summary>
    /// Gets an optional localized slug; the source slug is used when omitted.
    /// </summary>
[JsonPropertyName("slug")]
    public string? Slug { get; init; }

    /// <summary>
    /// Gets an optional localized title; blank values do not replace the source title.
    /// </summary>
[JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// Gets an optional localized summary; <see langword="null"/> preserves the source value.
    /// </summary>
[JsonPropertyName("summary")]
    public string? Summary { get; init; }

    /// <summary>
    /// Gets an optional localized SEO title; <see langword="null"/> preserves the source value.
    /// </summary>
[JsonPropertyName("seoTitle")]
    public string? SeoTitle { get; init; }

    /// <summary>
    /// Gets an optional localized SEO description; <see langword="null"/> preserves the source value.
    /// </summary>
[JsonPropertyName("seoDescription")]
    public string? SeoDescription { get; init; }
}

/// <summary>
/// Describes translated fields for a post identified by its source document.
/// </summary>
public sealed record TranslationPostImport
{
    /// <summary>
    /// Gets the source post identifier used to locate the translation group.
    /// </summary>
[JsonPropertyName("sourceId")]
    public long SourceId { get; init; }

    /// <summary>
    /// Gets an optional localized slug; the source slug is used when omitted.
    /// </summary>
[JsonPropertyName("slug")]
    public string? Slug { get; init; }

    /// <summary>
    /// Gets an optional localized title; blank values do not replace the source title.
    /// </summary>
[JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// Gets an optional localized excerpt; <see langword="null"/> preserves the source value.
    /// </summary>
[JsonPropertyName("excerpt")]
    public string? Excerpt { get; init; }

    /// <summary>
    /// Gets an optional localized SEO title; <see langword="null"/> preserves the source value.
    /// </summary>
[JsonPropertyName("seoTitle")]
    public string? SeoTitle { get; init; }

    /// <summary>
    /// Gets an optional localized SEO description; <see langword="null"/> preserves the source value.
    /// </summary>
[JsonPropertyName("seoDescription")]
    public string? SeoDescription { get; init; }
}

/// <summary>
/// Describes a category translation upsert.
/// </summary>
public sealed record TranslationCategoryImport
{
    /// <summary>
    /// Gets the source category identifier.
    /// </summary>
[JsonPropertyName("categoryId")]
    public long CategoryId { get; init; }

    /// <summary>
    /// Gets the localized name; <see langword="null"/> is stored as an empty string.
    /// </summary>
[JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// Gets the localized slug; <see langword="null"/> is stored as an empty string.
    /// </summary>
[JsonPropertyName("slug")]
    public string? Slug { get; init; }

    /// <summary>
    /// Gets the optional localized description.
    /// </summary>
[JsonPropertyName("description")]
    public string? Description { get; init; }
}

/// <summary>
/// Describes a tag translation upsert.
/// </summary>
public sealed record TranslationTagImport
{
    /// <summary>
    /// Gets the source tag identifier.
    /// </summary>
[JsonPropertyName("tagId")]
    public long TagId { get; init; }

    /// <summary>
    /// Gets the localized name; <see langword="null"/> is stored as an empty string.
    /// </summary>
[JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// Gets the optional localized description.
    /// </summary>
[JsonPropertyName("description")]
    public string? Description { get; init; }
}

/// <summary>
/// Describes a product translation upsert.
/// </summary>
public sealed record TranslationProductImport
{
    /// <summary>
    /// Gets the source product identifier.
    /// </summary>
[JsonPropertyName("productId")]
    public long ProductId { get; init; }

    /// <summary>
    /// Gets the localized name; <see langword="null"/> is stored as an empty string.
    /// </summary>
[JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// Gets the optional localized description.
    /// </summary>
[JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Gets the optional localized short description.
    /// </summary>
[JsonPropertyName("shortDescription")]
    public string? ShortDescription { get; init; }
}
