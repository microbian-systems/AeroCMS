using System.Text.Json;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Content.Templating;

/// <summary>
/// Defines an interface for ISecureScribanRenderer.
/// </summary>
public interface ISecureScribanRenderer
{
    /// <summary>
    /// Renders a pure template definition against an explicitly mapped JSON object.
    /// </summary>
    Task<Result<string, AeroError>> RenderAsync(
        ScribanRenderDefinition definition,
        JsonDocument? data,
        CancellationToken cancellationToken = default);
}
