namespace Aero.Cms.Abstractions.Content;

/// <summary>
/// Scheduling configuration for ContentTypeDefinition.
/// When set, content items of this type can opt into scheduled publishing.
/// </summary>
[GenerateSerializer]
[Alias("ContentTypeScheduleConfig")]
public sealed record ContentTypeScheduleConfig
{
        /// <summary>
    /// Gets or sets the Allow Scheduled Publish.
    /// </summary>
[Id(0)]
    public bool AllowScheduledPublish { get; init; }

        /// <summary>
    /// Gets or sets the Allow Scheduled Unpublish.
    /// </summary>
[Id(1)]
    public bool AllowScheduledUnpublish { get; init; }

        /// <summary>
    /// Gets or sets the Max Publish Delay Days.
    /// </summary>
[Id(2)]
    public int? MaxPublishDelayDays { get; init; }
}
