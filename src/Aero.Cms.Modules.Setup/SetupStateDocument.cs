namespace Aero.Cms.Modules.Setup;

public sealed class SetupStateDocument
{
    public const string FixedId = "cms/setup-state";

    public string Id { get; set; } = FixedId;
    public bool IsComplete { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
}
