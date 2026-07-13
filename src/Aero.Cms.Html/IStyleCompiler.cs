using Aero.Core.Railway;

namespace Aero.Cms.Html;

/// <summary>
/// Strategy for compiling semantic page styles into a concrete CSS profile.
/// </summary>
public interface IStyleCompiler
{
    Result<CompiledPageStyles> Compile(HtmlPageContent content, IStyleProfile profile);
}
