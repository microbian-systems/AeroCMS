using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aero.Cms.Modules.Posts.Parsers;

/// <summary>
/// Parses a JSON object or array into blog post candidates.
/// </summary>
public sealed class JsonPostImportParser : IPostImportParser
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <inheritdoc />
    /// <remarks>Matching is case-insensitive and recognizes the <c>.json</c> suffix.</remarks>
    public bool Supports(string fileName) =>
        fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    /// <remarks>
    /// JSON property names are matched case-insensitively. Malformed JSON and documents without a
    /// deserializable object are represented as failure results; cancellation while copying the
    /// source stream is allowed to propagate.
    /// </remarks>
    public async Task<Result<List<ImportablePost>, AeroError>> ParseAsync(
        Stream fileStream, string fileName, CancellationToken ct = default)
    {
        try
        {
            using var memoryStream = new MemoryStream();
            await fileStream.CopyToAsync(memoryStream, ct);
            var json = System.Text.Encoding.UTF8.GetString(memoryStream.ToArray());

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                var posts = JsonSerializer.Deserialize<List<ImportEntry>>(json, JsonOpts);
                if (posts is { Count: > 0 })
                {
                    return Prelude.Ok<List<ImportablePost>, AeroError>(
                        posts.Select(Map).ToList());
                }
            }
            else
            {
                var single = JsonSerializer.Deserialize<ImportEntry>(json, JsonOpts);
                if (single is not null)
                {
                    return Prelude.Ok<List<ImportablePost>, AeroError>(
                        [Map(single)]);
                }
            }

            return Prelude.Fail<List<ImportablePost>, AeroError>(
                AeroError.CreateError("No valid blog post entries found in JSON"));
        }
        catch (JsonException ex)
        {
            return Prelude.Fail<List<ImportablePost>, AeroError>(
                AeroError.CreateError($"Invalid JSON: {ex.Message}"));
        }
    }

    /// <summary>
    /// Converts the wire-format entry to the parser-neutral import model.
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
    /// Applies the importer's limited ASCII punctuation replacement to a title.
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


    // Internal DTO matching the import JSON format
    /// <summary>
    /// Models the JSON properties understood by the importer.
    /// </summary>
    private sealed class ImportEntry
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
