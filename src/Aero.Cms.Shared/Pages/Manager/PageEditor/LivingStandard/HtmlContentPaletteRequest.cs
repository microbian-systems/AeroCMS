using System.Globalization;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>
/// Carries one validated structured-content palette request between the sidebar,
/// browser drag intent, and owning page editor.
/// </summary>
public sealed record HtmlContentPaletteRequest
{
    private const string TokenVersion = "1";

    public HtmlPaletteItemKind ItemKind { get; init; }

    public long ContentTypeId { get; init; }

    public string ContentTypeAlias { get; init; } = string.Empty;

    public long? ContentItemId { get; init; }

    public string? ContentItemSlug { get; init; }

    public string? ContentItemTitle { get; init; }

    public string? FieldName { get; init; }

    public string? FieldType { get; init; }

    public string? FieldLabel { get; init; }

    /// <summary>
    /// Creates the opaque, URI-escaped token stored on a palette drag source.
    /// </summary>
    public string ToToken() => string.Join(
        '|',
        TokenVersion,
        ContentTypeId.ToString(CultureInfo.InvariantCulture),
        Escape(ContentTypeAlias),
        ContentItemId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
        Escape(ContentItemSlug),
        Escape(ContentItemTitle),
        Escape(FieldName),
        Escape(FieldType),
        Escape(FieldLabel));

    /// <summary>
    /// Parses an opaque palette token and validates the shape required by its item kind.
    /// The PageEditor still revalidates the values against its current HTTP metadata.
    /// </summary>
    public static bool TryParse(
        HtmlPaletteItemKind itemKind,
        string token,
        out HtmlContentPaletteRequest? request)
    {
        request = null;
        if (itemKind is not HtmlPaletteItemKind.ContentList
            and not HtmlPaletteItemKind.ContentItem
            and not HtmlPaletteItemKind.ContentField)
        {
            return false;
        }

        var parts = token.Split('|');
        if (parts.Length != 9
            || parts[0] != TokenVersion
            || !long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var contentTypeId)
            || contentTypeId <= 0)
        {
            return false;
        }

        try
        {
            long? contentItemId = long.TryParse(
                parts[3],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsedItemId)
                    ? parsedItemId
                    : null;
            var candidate = new HtmlContentPaletteRequest
            {
                ItemKind = itemKind,
                ContentTypeId = contentTypeId,
                ContentTypeAlias = Unescape(parts[2]),
                ContentItemId = contentItemId,
                ContentItemSlug = NullIfEmpty(Unescape(parts[4])),
                ContentItemTitle = NullIfEmpty(Unescape(parts[5])),
                FieldName = NullIfEmpty(Unescape(parts[6])),
                FieldType = NullIfEmpty(Unescape(parts[7])),
                FieldLabel = NullIfEmpty(Unescape(parts[8]))
            };

            if (string.IsNullOrWhiteSpace(candidate.ContentTypeAlias)
                || itemKind == HtmlPaletteItemKind.ContentItem && candidate.ContentItemId is not > 0
                || itemKind == HtmlPaletteItemKind.ContentField
                    && (string.IsNullOrWhiteSpace(candidate.FieldName)
                        || string.IsNullOrWhiteSpace(candidate.FieldType)))
            {
                return false;
            }

            request = candidate;
            return true;
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    private static string Escape(string? value) => Uri.EscapeDataString(value ?? string.Empty);

    private static string Unescape(string value) => Uri.UnescapeDataString(value);

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
