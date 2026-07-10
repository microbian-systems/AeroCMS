using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Blocks.Dynamic;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Content.Rendering;

/// <summary>
/// Represents a class for ContentItemRenderer.
/// </summary>
public sealed class ContentItemRenderer(
    IContentTypeRenderingBridge bridge,
    ISecureScribanRenderer scribanRenderer) : IContentItemRenderer
{
        /// <summary>
    /// RenderAsync method.
    /// </summary>
public async Task<Result<string, AeroError>> RenderAsync(
        ContentTypeDefinition typeDefinition,
        ContentItem item,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(typeDefinition);
        ArgumentNullException.ThrowIfNull(item);

        if (typeDefinition.RenderMode != ContentTypeRenderMode.DynamicBlock)
        {
            return AeroError.CreateError(
                $"Content type render mode '{typeDefinition.RenderMode}' is not implemented.");
        }

        var blockResult = await bridge.ToDynamicBlockAsync(typeDefinition, item, ct);
        if (blockResult is Result<DynamicTemplateBlock, AeroError>.Failure blockFailure)
            return blockFailure.Error;

        var definitionResult = await bridge.GetDefinitionAsync(typeDefinition, ct);
        if (definitionResult is Result<DynamicBlockDefinition, AeroError>.Failure definitionFailure)
            return definitionFailure.Error;

        var block = ((Result<DynamicTemplateBlock, AeroError>.Ok)blockResult).Value;
        var definition = ((Result<DynamicBlockDefinition, AeroError>.Ok)definitionResult).Value;
        definition.ScribanTemplate = ContentTypeTemplateGenerator.NormalizeTemplate(
            definition.ScribanTemplate,
            typeDefinition.Fields);
        return await scribanRenderer.RenderAsync(definition, block.Data, ct);
    }
}
