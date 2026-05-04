using System.Text.Json;
using Aero.Cms.Modules.Blog.Models;
using Aero.Core;

namespace Aero.Cms.Modules.Blog.Parsers;

/// <summary>
/// Parses JSON blog import files. Accepts a single post object or an array of posts.
/// </summary>
public sealed class JsonBlogImportParser : IBlogImportParser
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
        Slug = entry.Slug ?? string.Empty,
        MarkdownContent = entry.Content ?? string.Empty,
        CoverImage = entry.CoverImage,
        PublishedOn = DateTimeOffset.TryParse(entry.Date, out var dt) ? dt : null,
        Tags = entry.Tags ?? []
    };

    // Internal DTO matching the import JSON format
    private sealed record ImportEntry
    {
        public int Id { get; init; }
        public string? Title { get; init; }
        public string? Date { get; init; }
        public string? Slug { get; init; }
        public string? CoverImage { get; init; }
        public string? Content { get; init; }
        public List<string>? Tags { get; init; }
    }
}
