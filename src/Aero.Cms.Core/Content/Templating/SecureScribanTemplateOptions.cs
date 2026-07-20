namespace Aero.Cms.Core.Content.Templating;

/// <summary>
/// Names the fixed capability policy used for CMS-authored Scriban templates.
/// </summary>
/// <remarks>
/// This is currently descriptive metadata. The renderer supports one policy and enforces its
/// controls directly rather than branching on the enum value.
/// </remarks>
public enum ScribanTemplateTrustPolicy
{
    /// <summary>
    /// Identifies the policy containing Scriban's safe registered built-ins, local functions, and
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
    /// <summary>Gets the maximum UTF-8 template size accepted by validation.</summary>
    public int MaxTemplateLengthBytes { get; init; } = 50_000;

    /// <summary>Gets the Scriban loop-iteration limit for enumerable and queryable loops.</summary>
    public int LoopLimit { get; init; } = 1_000;

    /// <summary>Gets the Scriban recursive-call limit.</summary>
    public int RecursiveLimit { get; init; } = 50;

    /// <summary>Gets whether Scriban reports references to unavailable variables.</summary>
    public bool StrictVariables { get; init; } = true;

    /// <summary>Gets the timeout applied by Scriban regular-expression operations.</summary>
    public TimeSpan RegexTimeout { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Gets the overall rendering deadline.</summary>
    public TimeSpan RenderTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets the maximum JSON nesting depth projected into Scriban scopes.</summary>
    public int MaxInputDepth { get; init; } = 10;

    /// <summary>Gets the maximum rendered character count accepted before sanitization.</summary>
    public int MaxOutputLength { get; init; } = 1_048_576;

    /// <summary>
    /// Gets the named template capability policy. Runtime safety is enforced
    /// through explicit ScriptObject inputs, disabled relaxed CLR access, a
    /// null template loader, sanitization, and resource limits.
    /// </summary>
    /// <remarks>
    /// The current renderer does not branch on this value; the listed controls enforce its
    /// single supported policy directly.
    /// </remarks>
    public ScribanTemplateTrustPolicy TrustPolicy { get; init; } =
        ScribanTemplateTrustPolicy.FullCmsTemplate;
}
