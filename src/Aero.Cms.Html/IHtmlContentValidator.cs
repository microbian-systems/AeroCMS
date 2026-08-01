using Aero.Core.Railway;

namespace Aero.Cms.Html;

/// <summary>
/// Validates a complete persisted HTML fragment at save, publish, preview, and render boundaries.
/// </summary>
public interface IHtmlContentValidator
{
    /// <summary>
    /// Validates a complete tree without mutating it.
    /// </summary>
    /// <param name="content">The page content to validate.</param>
    /// <returns>A successful result containing <see langword="true"/>, or all discovered validation errors.</returns>
    Result<bool> Validate(HtmlPageContent content);
}
