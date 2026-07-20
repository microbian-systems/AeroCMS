using System.Text.RegularExpressions;

namespace Aero.Cms.Modules.Posts.Parsers;

/// <summary>
/// Parses a Markdown file into one blog post candidate.
/// </summary>
/// <remarks>
/// The first level-one heading becomes the title and is removed from the body. When no such
/// heading exists, the file name without its extension becomes the title.
/// </remarks>
public sealed partial class MarkdownPostImportParser : IPostImportParser
{
    /// <summary>
    /// Provides the compiled expression used to locate the first level-one Markdown heading.
    /// </summary>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex(@"^#\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex HeadingRegex();

    /// <inheritdoc />
    /// <remarks>Matching is case-insensitive and recognizes <c>.md</c> and <c>.markdown</c>.</remarks>
    public bool Supports(string fileName) =>
        fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    /// <remarks>
    /// Reading is synchronous despite the task-shaped contract. Empty files and read failures are
    /// returned as failure results; the supplied cancellation token is not observed by this implementation.
    /// </remarks>
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

    /// <summary>
    /// Applies the Markdown importer's limited ASCII punctuation replacement to a title.
    /// </summary>
    /// <param name="title">The extracted title.</param>
    /// <returns>A lowercase slug, or <c>untitled</c> when the title is blank.</returns>
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
