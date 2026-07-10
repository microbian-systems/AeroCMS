using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aero.Cms.Modules.Posts.Parsers;

/// <summary>
/// Parses ZIP files containing one or more <c>.json</c> blog post files.
/// Delegates JSON parsing inline (avoids double-buffering through the JSON parser).
/// </summary>
public sealed class ZipPostImportParser : IPostImportParser
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <inheritdoc />
    public bool Supports(string fileName) =>
        fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
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

    private static ImportablePost Map(ImportEntry entry) => new()
    {
        Title = entry.Title ?? string.Empty,
        Slug = entry.Slug ?? GenerateSlug(entry.Title),
        MarkdownContent = entry.Content ?? string.Empty,
        CoverImage = entry.CoverImage,
        PublishedOn = TryParseDate(entry.Date),
        Tags = entry.Tags ?? []
    };

    private static string GenerateSlug(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "untitled";
        return title
            .ToLowerInvariant().Trim()
            .Replace(" ", "-").Replace(":", "").Replace(".", "")
            .Replace(",", "").Replace("'", "").Replace("\"", "")
            .Replace("--", "-").Trim('-');
    }

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

    private sealed record ImportEntry
    {
                /// <summary>
        /// Gets or sets the Id.
        /// </summary>
[JsonPropertyName("id")] public int Id { get; set; }
                /// <summary>
        /// Gets or sets the Title.
        /// </summary>
[JsonPropertyName("title")] public string? Title { get; set; }
                /// <summary>
        /// Gets or sets the Date.
        /// </summary>
[JsonPropertyName("date")] public string? Date { get; set; }
                /// <summary>
        /// Gets or sets the Slug.
        /// </summary>
[JsonPropertyName("slug")] public string? Slug { get; set; }
                /// <summary>
        /// Gets or sets the Cover Image.
        /// </summary>
[JsonPropertyName("coverImage")] public string? CoverImage { get; set; }
                /// <summary>
        /// Gets or sets the Content.
        /// </summary>
[JsonPropertyName("content")] public string? Content { get; set; }
                /// <summary>
        /// Gets or sets the Tags.
        /// </summary>
[JsonPropertyName("tags")] public List<string>? Tags { get; set; }
    }
}
