namespace Aero.Cms.Modules.SiteMap;

/// <summary>
/// Represents a record for SitemapEntry.
/// </summary>
public sealed record SitemapEntry
{
        /// <summary>
    /// Gets or sets the Loc.
    /// </summary>
public string Loc { get; init; } = string.Empty;
        /// <summary>
    /// Gets or sets the Last Mod.
    /// </summary>
public DateTimeOffset? LastMod { get; init; }
        /// <summary>
    /// Gets or sets the Change Freq.
    /// </summary>
public ChangeFrequency ChangeFreq { get; init; } = ChangeFrequency.Weekly;
        /// <summary>
    /// Gets or sets the Priority.
    /// </summary>
public double Priority { get; init; } = 0.5;
        /// <summary>
    /// Gets or sets the Alternates.
    /// </summary>
public IReadOnlyList<SitemapAlternateLink> Alternates { get; init; } = [];
}

/// <summary>
/// Represents a record for SitemapAlternateLink.
/// </summary>
public sealed record SitemapAlternateLink(string Hreflang, string Href);

/// <summary>
/// Defines an enumeration for ChangeFrequency.
/// </summary>
public enum ChangeFrequency
{
    Always,
    Hourly,
    Daily,
    Weekly,
    Monthly,
    Yearly,
    Never
}
