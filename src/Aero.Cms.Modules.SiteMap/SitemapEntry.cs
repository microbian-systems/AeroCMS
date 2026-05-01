namespace Aero.Cms.Modules.SiteMap;

public sealed record SitemapEntry
{
    public string Loc { get; init; } = string.Empty;
    public DateTimeOffset? LastMod { get; init; }
    public ChangeFrequency ChangeFreq { get; init; } = ChangeFrequency.Weekly;
    public double Priority { get; init; } = 0.5;
}

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
