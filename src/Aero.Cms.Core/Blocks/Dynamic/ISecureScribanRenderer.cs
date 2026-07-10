using System.Text.Json;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Blocks.Dynamic;

/// <summary>
/// Defines an interface for ISecureScribanRenderer.
/// </summary>
public interface ISecureScribanRenderer
{
        /// <summary>
    /// RenderAsync method.
    /// </summary>
Task<Result<string, AeroError>> RenderAsync(
        DynamicBlockDefinition definition,
        JsonDocument? data,
        CancellationToken cancellationToken = default);
}
