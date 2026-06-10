namespace Aero.Cms.Abstractions.Content;

/// <summary>
/// Scheduling configuration for ContentTypeDefinition.
/// When set, content items of this type can opt into scheduled publishing.
/// </summary>
[GenerateSerializer]
[Alias("ContentTypeScheduleConfig")]
public sealed record ContentTypeScheduleConfig
{
    [Id(0)]
    public bool AllowScheduledPublish { get; init; }

    [Id(1)]
    public bool AllowScheduledUnpublish { get; init; }

    [Id(2)]
    public int? MaxPublishDelayDays { get; init; }
}
