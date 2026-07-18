namespace Aero.Cms.Core.Content.Templating;

/// <summary>
/// Defines the capabilities granted to a CMS-authored Scriban template.
/// </summary>
public enum ScribanTemplateTrustPolicy
{
    /// <summary>
    /// Enables Scriban's safe registered built-ins, local functions, and
    /// imports from explicitly supplied script objects. Dynamic evaluation,
    /// filesystem/network includes, and relaxed CLR access remain disabled.
    /// </summary>
    FullCmsTemplate
}

/// <summary>
/// Runtime guardrails for user-authored Scriban templates.
/// </summary>
public sealed record SecureScribanTemplateOptions
{
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
    /// Gets the named template capability policy. Runtime safety is enforced
    /// through explicit ScriptObject inputs, disabled relaxed CLR access, a
    /// null template loader, sanitization, and resource limits.
    /// </summary>
    public ScribanTemplateTrustPolicy TrustPolicy { get; init; } =
        ScribanTemplateTrustPolicy.FullCmsTemplate;
}
