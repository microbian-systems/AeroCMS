using Aero.Core.Railway;

namespace Aero.Cms.Html;

/// <summary>
/// Validates a complete persisted HTML fragment at save, publish, preview, and render boundaries.
/// </summary>
public interface IHtmlContentValidator
{
    Result<bool> Validate(HtmlPageContent content);
}
