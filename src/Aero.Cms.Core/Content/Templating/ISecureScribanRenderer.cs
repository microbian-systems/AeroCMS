using Aero.Core;
using Aero.Core.Railway;
using Scriban.Runtime;

namespace Aero.Cms.Core.Content.Templating;

/// <summary>
/// Defines an interface for ISecureScribanRenderer.
/// </summary>
public interface ISecureScribanRenderer
{
    /// <summary>
    /// Renders a pure template definition against explicit content scopes.
    /// Only named <see cref="ScriptObject"/> instances supplied by trusted
    /// application code may be imported by the template.
    /// </summary>
    Task<Result<string, AeroError>> RenderAsync(
        ScribanRenderDefinition definition,
        ScribanContentRenderModel model,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, ScriptObject>? imports = null);
}
