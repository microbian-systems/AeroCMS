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

        /// <summary>
    /// Gets or sets the Max Template Length Bytes.
    /// </summary>
public int MaxTemplateLengthBytes { get; init; } = 50_000;

        /// <summary>
    /// Gets or sets the Loop Limit.
    /// </summary>
public int LoopLimit { get; init; } = 1_000;

        /// <summary>
    /// Gets or sets the Recursive Limit.
    /// </summary>
public int RecursiveLimit { get; init; } = 50;

        /// <summary>
    /// Gets or sets the Strict Variables.
    /// </summary>
public bool StrictVariables { get; init; } = true;

        /// <summary>
    /// Gets or sets the Regex Timeout.
    /// </summary>
public TimeSpan RegexTimeout { get; init; } = TimeSpan.FromSeconds(2);

        /// <summary>
    /// Gets or sets the Render Timeout.
    /// </summary>
public TimeSpan RenderTimeout { get; init; } = TimeSpan.FromSeconds(30);

        /// <summary>
    /// Gets or sets the Max Input Depth.
    /// </summary>
public int MaxInputDepth { get; init; } = 10;

        /// <summary>
    /// Gets or sets the Max Output Length.
    /// </summary>
public int MaxOutputLength { get; init; } = 1_048_576;

        /// <summary>
    /// Gets or sets the Allow All Functions.
    /// </summary>
public bool AllowAllFunctions { get; init; }

    /// <summary>
    /// Fully qualified Scriban function names that user-authored templates may call.
    /// Defaults to a curated, deterministic subset of Scriban string, array, html,
    /// and math helpers. Riskier groups such as object, regex, date.now, imports,
    /// and user-declared functions are not included.
    /// </summary>
    public IReadOnlySet<string> AllowedFunctionNames { get; init; } =
        new HashSet<string>(DefaultAllowedFunctionNames, StringComparer.OrdinalIgnoreCase);
}
