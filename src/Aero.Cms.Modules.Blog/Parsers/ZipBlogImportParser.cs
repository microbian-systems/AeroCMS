using System.IO.Compression;
using System.Text.Json;
using Aero.Cms.Modules.Blog.Models;
using Aero.Core;

namespace Aero.Cms.Modules.Blog.Parsers;

/// <summary>
/// Parses ZIP files containing one or more <c>.json</c> blog post files.
/// Delegates JSON parsing inline (avoids double-buffering through the JSON parser).
/// </summary>
public sealed class ZipBlogImportParser : IBlogImportParser
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
        PublishedOn = DateTimeOffset.TryParse(entry.Date, out var dt) ? dt : null,
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
