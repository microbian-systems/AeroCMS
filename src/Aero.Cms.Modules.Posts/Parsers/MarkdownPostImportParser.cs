using System.Text.RegularExpressions;

namespace Aero.Cms.Modules.Posts.Parsers;

/// <summary>
/// Parses plain Markdown (.md) files into a single blog post.
/// Title is extracted from the first <c># Heading</c> line;
/// slug is generated from the title; the remainder is the markdown body.
/// </summary>
public sealed partial class MarkdownPostImportParser : IPostImportParser
{
    [GeneratedRegex(@"^#\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex HeadingRegex();

    /// <inheritdoc />
    public bool Supports(string fileName) =>
        fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Task<Result<List<ImportablePost>, AeroError>> ParseAsync(
        Stream fileStream, string fileName, CancellationToken ct = default)
    {
        try
        {
            using var reader = new StreamReader(fileStream);
            var content = reader.ReadToEnd();

            if (string.IsNullOrWhiteSpace(content))
            {
                return Task.FromResult(
                    Prelude.Fail<List<ImportablePost>, AeroError>(
                        AeroError.CreateError("Markdown file is empty")));
            }

            var match = HeadingRegex().Match(content);
            string title;
            string body;

            if (match.Success)
            {
                title = match.Groups[1].Value.Trim();
                // Content after the heading line
                var afterHeading = content[(match.Index + match.Length)..].TrimStart();
                body = afterHeading;
            }
            else
            {
                // No heading found — use filename without extension as title
                title = Path.GetFileNameWithoutExtension(fileName);
                body = content;
            }

            var slug = Slugify(title);

            var post = new ImportablePost
            {
                Title = title,
                Slug = slug,
                MarkdownContent = body,
                PublishedOn = DateTimeOffset.UtcNow
            };

            return Task.FromResult(
                Prelude.Ok<List<ImportablePost>, AeroError>([post]));
        }
        catch (Exception ex)
        {
            return Task.FromResult(
                Prelude.Fail<List<ImportablePost>, AeroError>(
                    AeroError.CreateError($"Failed to parse markdown file: {ex.Message}")));
        }
    }

    private static string Slugify(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "untitled";

        return title
            .ToLowerInvariant()
            .Trim()
            .Replace(" ", "-")
            .Replace("--", "-")
            .Replace(":", "")
            .Replace(".", "")
            .Replace(",", "")
            .Replace("'", "")
            .Replace("\"", "")
            .Replace("!", "")
            .Replace("?", "")
            .Replace("(", "")
            .Replace(")", "")
            .Replace("&", "and")
            .Replace("--", "-")
            .Trim('-');
    }
}
