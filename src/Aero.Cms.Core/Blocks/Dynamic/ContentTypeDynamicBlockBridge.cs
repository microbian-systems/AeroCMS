using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Serialization;
using Aero.Cms.Abstractions.Content;
using Aero.Core;
using Aero.Core.Railway;
using Marten;

namespace Aero.Cms.Core.Blocks.Dynamic;

/// <summary>
/// Default implementation of IContentTypeRenderingBridge.
/// Creates or resolves a DynamicBlockDefinition for each content type and
/// produces a DynamicTemplateBlock from a ContentItem.
/// </summary>
public sealed class ContentTypeDynamicBlockBridge(
    IEnumerable<IFieldTemplateSnippet> snippets,
    IDocumentSession session) : IContentTypeRenderingBridge
{
    /// <inheritdoc />
    public async Task<Result<DynamicTemplateBlock, AeroError>> ToDynamicBlockAsync(
        ContentTypeDefinition typeDef,
        ContentItem item,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(typeDef);
        ArgumentNullException.ThrowIfNull(item);

        // 1. Resolve or create the DynamicBlockDefinition for this content type
        var definitionResult = await GetOrCreateDefinitionAsync(typeDef, ct);
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
        return await GetOrCreateDefinitionAsync(typeDef, ct);
    }

    private async Task<Result<DynamicBlockDefinition, AeroError>> GetOrCreateDefinitionAsync(
        ContentTypeDefinition typeDef,
        CancellationToken ct)
    {
        // Check if a DynamicBlockDefinition already exists for this content type
        var existing = await session.Query<DynamicBlockDefinition>()
            .FirstOrDefaultAsync(d =>
                d.BlockType == DynamicTemplateBlock.Discriminator &&
                d.Name == $"ct:{typeDef.Alias}" &&
                d.IsPublished, ct);

        if (existing is not null)
            return Prelude.Ok<DynamicBlockDefinition, AeroError>(existing);

        // Auto-generate a Scriban template from the field definitions
        var template = string.IsNullOrWhiteSpace(typeDef.ScribanTemplate)
            ? ContentTypeTemplateGenerator.GenerateTemplate(typeDef, snippets)
            : typeDef.ScribanTemplate;

        var definition = new DynamicBlockDefinition
        {
            Id = Snowflake.NewId(),
            Name = $"ct:{typeDef.Alias}",
            BlockType = DynamicTemplateBlock.Discriminator,
            ScribanTemplate = template,
            DataSchema = null,
            Version = 1,
            IsPublished = true
        };

        session.Store(definition);
        await session.SaveChangesAsync(ct);
        return Prelude.Ok<DynamicBlockDefinition, AeroError>(definition);
    }
}
