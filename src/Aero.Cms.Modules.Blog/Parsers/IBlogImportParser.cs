using Aero.Cms.Modules.Blog.Models;
using Aero.Core;

namespace Aero.Cms.Modules.Blog.Parsers;

/// <summary>
/// Strategy interface for parsing blog import files into <see cref="ImportablePost"/> entries.
/// </summary>
public interface IBlogImportParser
{
    /// <summary>
    /// Determines whether this parser supports the given file name by extension.
    /// </summary>
    bool Supports(string fileName);

    /// <summary>
    /// Parses the file content into a list of importable blog posts.
    /// </summary>
    /// <param name="fileStream">Stream containing the full file content (already decoded).</param>
    /// <param name="fileName">Original file name for extension detection.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of parsed posts or an error.</returns>
    Task<Result<List<ImportablePost>, AeroError>> ParseAsync(
        Stream fileStream, string fileName, CancellationToken ct = default);
}
