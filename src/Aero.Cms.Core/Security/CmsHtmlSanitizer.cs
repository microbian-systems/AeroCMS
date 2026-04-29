using Ganss.Xss;

namespace Aero.Cms.Core.Security;

public sealed class CmsHtmlSanitizer : ICmsHtmlSanitizer
{
    private readonly HtmlSanitizer sanitizer;

    public CmsHtmlSanitizer()
    {
        sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Remove("script");
        sanitizer.AllowedTags.Remove("style");
        sanitizer.AllowedSchemes.Remove("javascript");

        foreach (var attribute in sanitizer.AllowedAttributes.Where(static attribute => attribute.StartsWith("on", StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            sanitizer.AllowedAttributes.Remove(attribute);
        }
    }

    public string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        return sanitizer.Sanitize(html);
    }
}
