using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aero.Cms.Modules.Posts.Parsers;

/// <summary>
/// Parses the JSON entries contained in a ZIP archive into blog post candidates.
/// </summary>
/// <remarks>
/// Non-JSON entries are ignored. Entries whose normalized archive path contains <c>..</c> are
/// skipped, and invalid JSON is accumulated per entry so valid siblings can still be returned.
/// </remarks>
public sealed class ZipPostImportParser : IPostImportParser
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <inheritdoc />
    /// <remarks>Matching is case-insensitive and recognizes the <c>.zip</c> suffix.</remarks>
    public bool Supports(string fileName) =>
        fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    /// <remarks>
    /// The archive owns and closes the supplied stream. A failure is returned only when the archive
    /// is invalid or no posts were parsed and at least one entry error was recorded; entry errors are
    /// otherwise omitted from the successful result.
    /// </remarks>
    public async Task<Result<List<ImportablePost>, AeroError>> ParseAsync(
        Stream fileStream, string fileName, CancellationToken ct = default)
    {
        try
        {
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read, leaveOpen: false);
            var posts = new List<ImportablePost>();
            var errors = new List<string>();

            foreach (var entry in archive.Entries)
            {
                if (!entry.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Zip slip protection
                var entryFullName = entry.FullName.Replace('\\', '/');
                if (entryFullName.Contains(".."))
                {
                    errors.Add($"Skipped '{entry.Name}': path traversal detected");
                    continue;
                }

                await using var entryStream = entry.Open();
                using var reader = new StreamReader(entryStream);
                var json = await reader.ReadToEndAsync(ct);

                try
                {
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        var entries = JsonSerializer.Deserialize<List<ImportEntry>>(json, JsonOpts);
                        if (entries is { Count: > 0 })
                        {
                            posts.AddRange(entries.Select(Map));
                            continue;
                        }
                    }
                    else
                    {
                        var single = JsonSerializer.Deserialize<ImportEntry>(json, JsonOpts);
                        if (single is not null)
                        {
                            posts.Add(Map(single));
                            continue;
                        }
                    }

                    errors.Add($"No valid blog post entries found in '{entry.Name}'");
                }
                catch (JsonException ex)
                {
                    errors.Add($"Invalid JSON in '{entry.Name}': {ex.Message}");
                }
            }

            if (posts.Count == 0 && errors.Count > 0)
            {
                return Prelude.Fail<List<ImportablePost>, AeroError>(
                    AeroError.CreateError(string.Join("; ", errors)));
            }

            return Prelude.Ok<List<ImportablePost>, AeroError>(posts);
        }
        catch (InvalidDataException ex)
        {
            return Prelude.Fail<List<ImportablePost>, AeroError>(
                AeroError.CreateError($"Invalid or corrupted ZIP file: {ex.Message}"));
        }
    }

    /// <summary>
    /// Converts one archive entry to the parser-neutral import model.
    /// </summary>
    /// <param name="entry">The deserialized JSON entry.</param>
    /// <returns>A candidate whose missing strings and tag list are normalized to empty values.</returns>
    private static ImportablePost Map(ImportEntry entry) => new()
    {
        Title = entry.Title ?? string.Empty,
        Slug = entry.Slug ?? GenerateSlug(entry.Title),
        MarkdownContent = entry.Content ?? string.Empty,
        CoverImage = entry.CoverImage,
        PublishedOn = TryParseDate(entry.Date),
        Tags = entry.Tags ?? []
    };

    /// <summary>
    /// Applies the ZIP importer's limited ASCII punctuation replacement to a title.
    /// </summary>
    /// <param name="title">The optional source title.</param>
    /// <returns>A lowercase slug, or <c>untitled</c> when no title is available.</returns>
    private static string GenerateSlug(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "untitled";
        return title
            .ToLowerInvariant().Trim()
            .Replace(" ", "-").Replace(":", "").Replace(".", "")
            .Replace(",", "").Replace("'", "").Replace("\"", "")
            .Replace("--", "-").Trim('-');
    }

    /// <summary>
    /// Parses either a date-only value or an invariant ISO-compatible timestamp.
    /// </summary>
    /// <param name="dateStr">The optional date text.</param>
    /// <returns>The parsed value with universal time assumed, or <see langword="null"/> when parsing fails.</returns>
    private static DateTimeOffset? TryParseDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr)) return null;

        // Primary format: "2023-01-25"
        if (DateTimeOffset.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var dt))
            return dt;

        // Fallback: try ISO 8601 with time component
        if (DateTimeOffset.TryParse(dateStr, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var dt2))
            return dt2;

        return null;
    }

    /// <summary>
    /// Models the JSON properties understood for each archive entry.
    /// </summary>
    private sealed record ImportEntry
    {
        /// <summary>
        /// Gets or sets the optional source-system identifier; it is not used as the persisted post identifier.
        /// </summary>
[JsonPropertyName("id")] public int Id { get; set; }
        /// <summary>
        /// Gets or sets the source title.
        /// </summary>
[JsonPropertyName("title")] public string? Title { get; set; }
        /// <summary>
        /// Gets or sets the source publication date text.
        /// </summary>
[JsonPropertyName("date")] public string? Date { get; set; }
        /// <summary>
        /// Gets or sets the optional source slug.
        /// </summary>
[JsonPropertyName("slug")] public string? Slug { get; set; }
        /// <summary>
        /// Gets or sets the optional source cover-image URL.
        /// </summary>
[JsonPropertyName("coverImage")] public string? CoverImage { get; set; }
        /// <summary>
        /// Gets or sets the Markdown body.
        /// </summary>
[JsonPropertyName("content")] public string? Content { get; set; }
        /// <summary>
        /// Gets or sets the source tag names.
        /// </summary>
[JsonPropertyName("tags")] public List<string>? Tags { get; set; }
    }
}
