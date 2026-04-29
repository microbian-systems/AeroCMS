namespace Aero.Cms.Core.Blocks.Dynamic;

/// <summary>
/// Runtime guardrails for user-authored Scriban templates.
/// </summary>
public sealed record SecureScribanTemplateOptions
{
    public int MaxTemplateLengthBytes { get; init; } = 50_000;

    public int LoopLimit { get; init; } = 1_000;

    public int RecursiveLimit { get; init; } = 50;

    public bool StrictVariables { get; init; } = true;

    public TimeSpan RegexTimeout { get; init; } = TimeSpan.FromSeconds(2);

    public TimeSpan RenderTimeout { get; init; } = TimeSpan.FromSeconds(3);

    public int MaxInputDepth { get; init; } = 10;

    public int MaxOutputLength { get; init; } = 1_048_576;
}
