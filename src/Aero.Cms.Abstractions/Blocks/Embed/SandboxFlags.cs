namespace Aero.Cms.Abstractions.Blocks.Embed;

/// <summary>
/// Bitmask of iframe sandbox permissions.
/// Serializes to the HTML sandbox attribute string.
/// </summary>
[Flags]
public enum SandboxFlags
{
    /// <summary>No permissions granted — most restrictive.</summary>
    None = 0,

    /// <summary>Allow scripts to execute inside the iframe.</summary>
    AllowScripts = 1 << 0,

    /// <summary>Allow the iframe to access same-origin resources.</summary>
    AllowSameOrigin = 1 << 1,

    /// <summary>Allow the iframe to submit forms.</summary>
    AllowForms = 1 << 2,

    /// <summary>Allow the iframe to open popups/new windows.</summary>
    AllowPopups = 1 << 3,

    /// <summary>Allow the iframe to use the Presentation API.</summary>
    AllowPresentation = 1 << 4,

    /// <summary>Allow the iframe to open modals (e.g., via requestFullscreen).</summary>
    AllowModals = 1 << 5,

    // --- Named presets for property editor UI ---

    /// <summary>Strict: no permissions. Safest default for unknown content.</summary>
    Strict = None,

    /// <summary>Video preset: scripts + same-origin (YouTube, Vimeo).</summary>
    Video = AllowScripts | AllowSameOrigin,

    /// <summary>Form preset: scripts, same-origin, forms (Typeform, Google Forms).</summary>
    Form = AllowScripts | AllowSameOrigin | AllowForms,

    /// <summary>Full preset: most permissive for trusted providers.</summary>
    Full = AllowScripts | AllowSameOrigin | AllowForms | AllowPopups | AllowPresentation | AllowModals,
}

/// <summary>
/// Extension methods for <see cref="SandboxFlags"/> to convert to/from HTML attribute strings.
/// </summary>
public static class SandboxFlagsExtensions
{
    /// <summary>
    /// Converts the flags to the sandbox attribute string value.
    /// Returns empty string for None/Strict (no sandbox attribute means all restrictions).
    /// For the Strict preset, returns "sandbox" with no allow-* tokens.
    /// </summary>
    public static string ToAttributeValue(this SandboxFlags flags)
    {
        var tokens = new List<string>();

        if (flags.HasFlag(SandboxFlags.AllowScripts)) tokens.Add("allow-scripts");
        if (flags.HasFlag(SandboxFlags.AllowSameOrigin)) tokens.Add("allow-same-origin");
        if (flags.HasFlag(SandboxFlags.AllowForms)) tokens.Add("allow-forms");
        if (flags.HasFlag(SandboxFlags.AllowPopups)) tokens.Add("allow-popups");
        if (flags.HasFlag(SandboxFlags.AllowPresentation)) tokens.Add("allow-presentation");
        if (flags.HasFlag(SandboxFlags.AllowModals)) tokens.Add("allow-modals");

        return tokens.Count > 0 ? string.Join(" ", tokens) : string.Empty;
    }
}
