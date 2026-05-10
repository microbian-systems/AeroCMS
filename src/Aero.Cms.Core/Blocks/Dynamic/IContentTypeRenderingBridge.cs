using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Content;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Blocks.Dynamic;

/// <summary>
/// Bridges between ContentTypeDefinition + ContentItem and the dynamic block rendering pipeline.
/// </summary>
public interface IContentTypeRenderingBridge
{
    /// <summary>
    /// Converts a ContentTypeDefinition and ContentItem into a DynamicTemplateBlock
    /// that the existing rendering pipeline can process.
    /// </summary>
    Task<Result<DynamicTemplateBlock, AeroError>> ToDynamicBlockAsync(
        ContentTypeDefinition typeDef,
        ContentItem item,
        CancellationToken ct = default);

    /// <summary>
    /// Resolves (or creates) the DynamicBlockDefinition for the given content type,
    /// which contains the Scriban template and data schema.
    /// </summary>
    Task<Result<DynamicBlockDefinition, AeroError>> GetDefinitionAsync(
        ContentTypeDefinition typeDef,
        CancellationToken ct = default);
}
