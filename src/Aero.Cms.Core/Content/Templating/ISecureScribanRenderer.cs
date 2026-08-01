using Aero.Core;
using Aero.Core.Railway;
using Scriban.Runtime;

namespace Aero.Cms.Core.Content.Templating;

/// <summary>
/// Validates and renders Scriban templates within an explicitly supplied, constrained model.
/// </summary>
/// <remarks>
/// Implementations expose only projected content scopes and explicitly supplied imports,
/// apply configured Scriban resource limits, and sanitize successful HTML output. Imports
/// are trusted application inputs: callers must not expose unsafe functions or objects.
/// </remarks>
public interface ISecureScribanRenderer
{
    /// <summary>
    /// Renders a pure template definition against explicit content scopes.
    /// Only named <see cref="ScriptObject"/> instances supplied by trusted
    /// application code may be imported by the template.
    /// </summary>
    /// <param name="definition">The template definition to render.</param>
    /// <param name="model">The content model exposed to the template.</param>
    /// <param name="cancellationToken">A token that can cancel rendering.</param>
    /// <param name="imports">Additional trusted named scopes available to the template.</param>
    /// <returns>
    /// Sanitized rendered output on success; otherwise a validation or timeout error for
    /// rejected templates, incompatible data, runtime failures, cancellation, or exhausted
    /// rendering limits.
    /// </returns>
    /// <remarks>
    /// The renderer reads but does not dispose <see cref="ScribanRenderDefinition.DataSchema"/>.
    /// The caller retains ownership of that document. Argument failures and malformed schema
    /// structures may throw rather than being represented by <see cref="AeroError"/>.
    /// </remarks>
    Task<Result<string, AeroError>> RenderAsync(
        ScribanRenderDefinition definition,
        ScribanContentRenderModel model,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, ScriptObject>? imports = null);

    /// <summary>
    /// Renders against a complete application-owned global scope without adding
    /// content-item variables.
    /// </summary>
    /// <param name="definition">The exact template definition to validate and execute.</param>
    /// <param name="trustedGlobals">
    /// The closed global scope supplied by trusted application code. The renderer deep-clones
    /// it before execution so template mutations cannot escape the render.
    /// </param>
    /// <param name="cancellationToken">A token that can cancel rendering.</param>
    /// <returns>Sanitized rendered output or a bounded validation/timeout failure.</returns>
    /// <remarks>
    /// This entry point retains strict variables, resource limits, disabled template loading,
    /// disabled relaxed CLR access, the member filter, parsed-template caching, and output
    /// sanitization. Supplying safe values and functions remains the caller's trust boundary.
    /// </remarks>
    Task<Result<string>> RenderTrustedAsync(
        ScribanRenderDefinition definition,
        ScriptObject trustedGlobals,
        CancellationToken cancellationToken = default);
}
