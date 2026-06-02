namespace Aero.Cms.Core.Blocks.Dynamic;

/// <summary>
/// Runtime guardrails for user-authored Scriban templates.
/// </summary>
public sealed record SecureScribanTemplateOptions
{
    private static readonly HashSet<string> DefaultAllowedFunctionNames =
    [
        "array.first",
        "array.last",
        "array.size",
        "html.escape",
        "html.newline_to_br",
        "html.strip",
        "math.abs",
        "math.ceil",
        "math.floor",
        "math.round",
        "string.append",
        "string.capitalize",
        "string.contains",
        "string.downcase",
        "string.ends_with",
        "string.handleize",
        "string.prepend",
        "string.replace",
        "string.size",
        "string.slice",
        "string.split",
        "string.starts_with",
        "string.strip",
        "string.strip_newlines",
        "string.truncate",
        "string.truncatewords",
        "string.upcase"
    ];

    public int MaxTemplateLengthBytes { get; init; } = 50_000;

    public int LoopLimit { get; init; } = 1_000;

    public int RecursiveLimit { get; init; } = 50;

    public bool StrictVariables { get; init; } = true;

    public TimeSpan RegexTimeout { get; init; } = TimeSpan.FromSeconds(2);

    public TimeSpan RenderTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public int MaxInputDepth { get; init; } = 10;

    public int MaxOutputLength { get; init; } = 1_048_576;

    // TODO: Re-tighten this before production. MVP authoring temporarily allows
    // arbitrary Scriban built-in function calls so custom templates can be tested quickly.
    public bool AllowAllFunctions { get; init; } = true;

    /// <summary>
    /// Fully qualified Scriban function names that user-authored templates may call.
    /// Defaults to a curated, deterministic subset of Scriban string, array, html,
    /// and math helpers. Riskier groups such as object, regex, date.now, imports,
    /// and user-declared functions are not included.
    /// </summary>
    public IReadOnlySet<string> AllowedFunctionNames { get; init; } =
        new HashSet<string>(DefaultAllowedFunctionNames, StringComparer.OrdinalIgnoreCase);
}
