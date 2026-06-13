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
    /// Resolves the save-time synchronized DynamicBlockDefinition for the given
    /// content type. Rendering never creates or updates definitions.
    /// </summary>
    Task<Result<DynamicBlockDefinition, AeroError>> GetDefinitionAsync(
        ContentTypeDefinition typeDef,
        CancellationToken ct = default);
}
