using Aero.Core.Railway;

namespace Aero.Cms.Html;

/// <summary>
/// Strategy for compiling semantic page styles into a concrete CSS profile.
/// </summary>
public interface IStyleCompiler
{
    /// <summary>
    /// Compiles semantic style intent for an entire page under one immutable profile snapshot.
    /// </summary>
    /// <param name="content">The page tree whose node styles are compiled.</param>
    /// <param name="profile">The profile that resolves tokens and responsive breakpoints.</param>
    /// <returns>Deterministic classes, CSS, and a content hash, or validation errors for unsupported or unsafe values.</returns>
    Result<CompiledPageStyles> Compile(HtmlPageContent content, IStyleProfile profile);
}
