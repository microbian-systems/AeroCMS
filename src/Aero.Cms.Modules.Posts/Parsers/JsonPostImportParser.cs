using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aero.Cms.Modules.Posts.Models;
using Aero.Core;

namespace Aero.Cms.Modules.Posts.Parsers;

/// <summary>
/// Parses JSON blog import files. Accepts a single post object or an array of posts.
/// </summary>
public sealed class JsonPostImportParser : IPostImportParser
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <inheritdoc />
    public bool Supports(string fileName) =>
        fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
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


    // Internal DTO matching the import JSON format
    private sealed class ImportEntry
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("date")] public string? Date { get; set; }
        [JsonPropertyName("slug")] public string? Slug { get; set; }
        [JsonPropertyName("coverImage")] public string? CoverImage { get; set; }
        [JsonPropertyName("content")] public string? Content { get; set; }
        [JsonPropertyName("tags")] public List<string>? Tags { get; set; }
    }
}
