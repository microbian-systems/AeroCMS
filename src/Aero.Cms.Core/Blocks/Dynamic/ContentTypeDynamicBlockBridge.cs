using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Serialization;
using Aero.Cms.Abstractions.Content;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;

namespace Aero.Cms.Core.Blocks.Dynamic;

/// <summary>
/// Default implementation of IContentTypeRenderingBridge.
/// Creates or resolves a DynamicBlockDefinition for each content type and
/// produces a DynamicTemplateBlock from a ContentItem.
/// </summary>
public sealed class ContentTypeDynamicBlockBridge(
    IQuerySession session) : IContentTypeRenderingBridge
{
    /// <inheritdoc />
    public async Task<Result<DynamicTemplateBlock, AeroError>> ToDynamicBlockAsync(
        ContentTypeDefinition typeDef,
        ContentItem item,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(typeDef);
        ArgumentNullException.ThrowIfNull(item);

        // Definitions are synchronized when the content type is saved.
        var definitionResult = await GetDefinitionAsync(typeDef, ct);
        if (definitionResult is Result<DynamicBlockDefinition, AeroError>.Failure fail)
            return fail.Error;

        var definition = ((Result<DynamicBlockDefinition, AeroError>.Ok)definitionResult).Value;

        // 2. Serialize ContentItem.Fields into a JsonDocument
        var dataJson = JsonSerializer.Serialize(item.Fields, BlockJsonContext.Default.Options);
        var dataDocument = JsonDocument.Parse(dataJson);

        // 3. Produce a DynamicTemplateBlock
        return Prelude.Ok<DynamicTemplateBlock, AeroError>(new DynamicTemplateBlock
        {
            Id = item.Id,
            DefinitionId = definition.Id,
            DefinitionVersion = definition.Version,
            Data = dataDocument
        });
    }

    /// <inheritdoc />
    public async Task<Result<DynamicBlockDefinition, AeroError>> GetDefinitionAsync(
        ContentTypeDefinition typeDef,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(typeDef);

        var definition = await session.Query<DynamicBlockDefinition>()
            .FirstOrDefaultAsync(d =>
                d.ContentTypeId == typeDef.Id &&
                d.SiteId == typeDef.SiteId &&
                d.BlockType == DynamicTemplateBlock.Discriminator &&
                d.IsPublished, ct);

        definition ??= await session.Query<DynamicBlockDefinition>()
            .FirstOrDefaultAsync(d =>
                d.ContentTypeId == null &&
                d.Name == $"ct:{typeDef.Alias}" &&
                d.BlockType == DynamicTemplateBlock.Discriminator &&
                d.IsPublished, ct);

        if (definition is null)
        {
            return AeroError.CreateError(
                $"Rendering definition for content type '{typeDef.Alias}' was not found. Save the content type to synchronize its template.");
        }

        return Prelude.Ok<DynamicBlockDefinition, AeroError>(definition);
    }
}
