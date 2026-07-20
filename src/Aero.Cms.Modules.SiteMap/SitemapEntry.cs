namespace Aero.Cms.Modules.SiteMap;

/// <summary>
/// Describes one URL element in a generated sitemap.
/// </summary>
public sealed record SitemapEntry
{
        /// <summary>
    /// Gets the absolute canonical URL written to the <c>loc</c> element.
    /// </summary>
public string Loc { get; init; } = string.Empty;
        /// <summary>
    /// Gets the optional last-modified timestamp, serialized as a calendar date.
    /// </summary>
public DateTimeOffset? LastMod { get; init; }
        /// <summary>
    /// Gets the crawler change-frequency hint.
    /// </summary>
public ChangeFrequency ChangeFreq { get; init; } = ChangeFrequency.Weekly;
        /// <summary>
    /// Gets the crawler priority hint serialized with one fractional digit.
    /// </summary>
public double Priority { get; init; } = 0.5;
        /// <summary>
    /// Gets culture-specific alternate links for the same translation group.
    /// </summary>
public IReadOnlyList<SitemapAlternateLink> Alternates { get; init; } = [];
}

/// <summary>
/// Associates an HTML language code with an absolute alternate URL.
/// </summary>
/// <param name="Hreflang">The language value written to the <c>hreflang</c> attribute.</param>
/// <param name="Href">The absolute URL written to the <c>href</c> attribute.</param>
public sealed record SitemapAlternateLink(string Hreflang, string Href);

/// <summary>
/// Defines the sitemap protocol's crawler change-frequency hints.
/// </summary>
public enum ChangeFrequency
{
    /// <summary>Indicates that content changes whenever it is accessed.</summary>
    Always,
    /// <summary>Indicates that content generally changes hourly.</summary>
    Hourly,
    /// <summary>Indicates that content generally changes daily.</summary>
    Daily,
    /// <summary>Indicates that content generally changes weekly.</summary>
    Weekly,
    /// <summary>Indicates that content generally changes monthly.</summary>
    Monthly,
    /// <summary>Indicates that content generally changes yearly.</summary>
    Yearly,
    /// <summary>Indicates that archived content is not expected to change.</summary>
    Never
}
