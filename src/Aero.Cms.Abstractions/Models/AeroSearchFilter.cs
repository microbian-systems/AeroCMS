namespace Aero.Cms.Abstractions.Models;

[Alias("AeroSearchFilter")]
[GenerateSerializer]
public sealed record AeroSearchFilter
{
    [Id(0)]
    public long[] Ids { get; set; } = [];
    [Id(1)]
    public long? SiteId { get; set; }
    [Id(2)]
    public string? ContentType { get; set; }
    [Id(3)]
    public string? Url { get; set; }
    [Id(4)]
    public bool? IsPublished { get; set; }
    [Id(5)]
    public string[] Authors { get; set; } = [];
    [Id(6)]
    public string[] Tags { get;set; } = [];
    [Id(7)]
    public string[] Categories { get; set; } = [];
    [Id(8)]
    public DateTimeOffset? PublishedAfter { get; set; }
    [Id(9)]
    public DateTimeOffset? PublishedBefore { get; set; }
    [Id(10)]
    public string? NameOrTitle { get; set; }
    [Id(11)]
    public string? Contains { get; set; }
    [Id(12)]
    public (int page, int rows) Page { get; set; }
    [Id(13)]
    public DateTimeOffset? CreateBefore { get; set; }
    [Id(14)]
    public DateTimeOffset? CreateAfter { get; set; }
}