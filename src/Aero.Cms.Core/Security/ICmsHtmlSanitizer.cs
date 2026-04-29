namespace Aero.Cms.Core.Security;

public interface ICmsHtmlSanitizer
{
    string Sanitize(string? html);
}
