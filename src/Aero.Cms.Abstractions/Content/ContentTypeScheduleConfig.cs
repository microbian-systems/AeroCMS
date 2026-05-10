namespace Aero.Cms.Abstractions.Content;

/// <summary>
/// Scheduling configuration for ContentTypeDefinition.
/// When set, content items of this type can opt into scheduled publishing.
/// </summary>
public sealed record ContentTypeScheduleConfig
{
    public bool AllowScheduledPublish { get; init; }
    public bool AllowScheduledUnpublish { get; init; }
    public int? MaxPublishDelayDays { get; init; }
}
