namespace Aero.Cms.Modules.Posts.Parsers;

/// <summary>
/// Defines a format-specific strategy for converting an import stream into post candidates.
/// </summary>
public interface IPostImportParser
{
    /// <summary>
    /// Determines whether the parser recognizes a file name.
    /// </summary>
    /// <param name="fileName">The original file name, including its extension.</param>
    /// <returns><see langword="true"/> when this parser accepts the file name; otherwise, <see langword="false"/>.</returns>
    bool Supports(string fileName);

    /// <summary>
    /// Parses the supplied stream into zero or more post candidates.
    /// </summary>
    /// <param name="fileStream">The decoded file content, positioned where parsing should begin.</param>
    /// <param name="fileName">The original file name used for format-specific fallbacks.</param>
    /// <param name="ct">A token used to cancel asynchronous reads.</param>
    /// <returns>A success containing parsed posts, or a failure describing invalid input.</returns>
    /// <remarks>
    /// Implementations consume the stream and do not guarantee that it remains open or reusable.
    /// </remarks>
    /// <exception cref="OperationCanceledException">The asynchronous read is canceled.</exception>
    Task<Result<List<ImportablePost>, AeroError>> ParseAsync(
        Stream fileStream, string fileName, CancellationToken ct = default);
}
