using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Blocks.Dynamic;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;

namespace Aero.Cms.Core.Content.Services;

/// <summary>
/// Represents a class for AeroContentTypeService.
/// </summary>
public sealed class AeroContentTypeService(
    IDocumentSession session,
    IEnumerable<IFieldTemplateSnippet> snippets,
    DynamicTemplateValidator templateValidator) : IContentTypeService
{
        /// <summary>
    /// GetByAliasAsync method.
    /// </summary>
public async Task<Result<ContentTypeDefinition, AeroError>> GetByAliasAsync(long siteId, string alias, CancellationToken ct = default)
    {
        var doc = await session.Query<ContentTypeDocument>()
            .FirstOrDefaultAsync(x => x.SiteId == siteId && x.Alias == alias, ct);

        if (doc is null)
            return Prelude.Fail<ContentTypeDefinition, AeroError>(AeroError.CreateError($"Content type '{alias}' not found."));
        return Prelude.Ok<ContentTypeDefinition, AeroError>(Map(doc));
    }

        /// <summary>
    /// GetAllAsync method.
    /// </summary>
public async Task<Result<IReadOnlyList<ContentTypeDefinition>, AeroError>> GetAllAsync(long siteId, CancellationToken ct = default)
    {
        var docs = await session.Query<ContentTypeDocument>().Where(x => x.SiteId == siteId).ToListAsync(ct);
        return Prelude.Ok<IReadOnlyList<ContentTypeDefinition>, AeroError>(docs.Select(Map).ToList());
    }

        /// <summary>
    /// SaveAsync method.
    /// </summary>
public async Task<Result<ContentTypeDefinition, AeroError>> SaveAsync(ContentTypeDefinition definition, CancellationToken ct = default)
    {
        var existing = await session.Query<ContentTypeDocument>()
            .FirstOrDefaultAsync(x => x.SiteId == definition.SiteId && x.Alias == definition.Alias, ct);

        if (existing is not null && existing.Id != definition.Id)
        {
            return Prelude.Fail<ContentTypeDefinition, AeroError>(
                AeroError.CreateError($"A content type with alias '{definition.Alias}' already exists for this site."));
        }

        var doc = new ContentTypeDocument
        {
            Id = definition.Id == 0 ? Snowflake.NewId() : definition.Id,
            SiteId = definition.SiteId,
            Alias = definition.Alias,
            Name = definition.Name,
            Description = definition.Description,
            Category = definition.Category,
            Icon = definition.Icon,
            AllowPublicUrl = definition.AllowPublicUrl,
            HideFromSearch = definition.HideFromSearch,
            Fields = definition.Fields,
            ScribanTemplate = definition.ScribanTemplate,
            RenderMode = definition.RenderMode,
            ScheduleConfig = definition.ScheduleConfig
        };

        var syncResult = await SynchronizeRenderingDefinitionAsync(definition, doc.Id, ct);
        if (syncResult is Result<NoneType, AeroError>.Failure syncFailure)
            return syncFailure.Error;

        doc.ScribanTemplate = definition.ScribanTemplate;
        session.Store(doc);
        await session.SaveChangesAsync(ct);
        definition.Id = doc.Id;
        return Prelude.Ok<ContentTypeDefinition, AeroError>(definition);
    }

        /// <summary>
    /// DeleteAsync method.
    /// </summary>
public async Task<Result<bool, AeroError>> DeleteAsync(long siteId, string alias, CancellationToken ct = default)
    {
        var doc = await session.Query<ContentTypeDocument>()
            .FirstOrDefaultAsync(x => x.SiteId == siteId && x.Alias == alias, ct);

        if (doc is null)
            return Prelude.Ok<bool, AeroError>(false);

        var renderingDefinitions = await session.Query<DynamicBlockDefinition>()
            .Where(x => x.ContentTypeId == doc.Id)
            .ToListAsync(ct);
        foreach (var renderingDefinition in renderingDefinitions)
            session.Delete(renderingDefinition);

        session.Delete(doc);
        await session.SaveChangesAsync(ct);
        return Prelude.Ok<bool, AeroError>(true);
    }

    private static ContentTypeDefinition Map(ContentTypeDocument doc) => new()
    {
        Id = doc.Id, SiteId = doc.SiteId, Alias = doc.Alias, Name = doc.Name, Description = doc.Description,
        Category = doc.Category, Icon = doc.Icon, AllowPublicUrl = doc.AllowPublicUrl, HideFromSearch = doc.HideFromSearch, Fields = doc.Fields,
        ScribanTemplate = doc.ScribanTemplate, RenderMode = doc.RenderMode, ScheduleConfig = doc.ScheduleConfig
    };

    private async Task<Result<NoneType, AeroError>> SynchronizeRenderingDefinitionAsync(
        ContentTypeDefinition definition,
        long contentTypeId,
        CancellationToken ct)
    {
        if (definition.RenderMode != ContentTypeRenderMode.DynamicBlock)
        {
            var existingDefinition = await session.Query<DynamicBlockDefinition>()
                .FirstOrDefaultAsync(x => x.ContentTypeId == contentTypeId, ct);
            if (existingDefinition is not null && existingDefinition.IsPublished)
            {
                existingDefinition.IsPublished = false;
                existingDefinition.Version++;
                session.Store(existingDefinition);
            }

            return Prelude.Ok<NoneType, AeroError>(Prelude.None);
        }

        var template = string.IsNullOrWhiteSpace(definition.ScribanTemplate)
            ? ContentTypeTemplateGenerator.GenerateTemplate(definition, snippets)
            : definition.ScribanTemplate;
        template = ContentTypeTemplateGenerator.NormalizeTemplate(template, definition.Fields);
        definition.ScribanTemplate = template;
        using var schema = ContentTypeSchemaGenerator.GenerateSchema(definition);

        var validation = templateValidator.Validate(template, schema);
        if (validation is Result<NoneType, AeroError>.Failure)
            return validation;

        var existing = await session.Query<DynamicBlockDefinition>()
            .FirstOrDefaultAsync(x => x.ContentTypeId == contentTypeId, ct);

        existing ??= await session.Query<DynamicBlockDefinition>()
            .FirstOrDefaultAsync(x =>
                x.ContentTypeId == null &&
                x.Name == $"ct:{definition.Alias}" &&
                x.BlockType == DynamicTemplateBlock.Discriminator, ct);

        if (existing is null)
        {
            session.Store(new DynamicBlockDefinition
            {
                Id = Snowflake.NewId(),
                ContentTypeId = contentTypeId,
                SiteId = definition.SiteId,
                Name = $"ct:{definition.SiteId}:{definition.Alias}",
                BlockType = DynamicTemplateBlock.Discriminator,
                ScribanTemplate = template,
                DataSchema = JsonDocument.Parse(schema.RootElement.GetRawText()),
                Version = 1,
                IsPublished = true
            });
            return Prelude.Ok<NoneType, AeroError>(Prelude.None);
        }

        var schemaText = schema.RootElement.GetRawText();
        var existingSchemaText = existing.DataSchema?.RootElement.GetRawText();
        var changed = !string.Equals(existing.ScribanTemplate, template, StringComparison.Ordinal) ||
            !string.Equals(existingSchemaText, schemaText, StringComparison.Ordinal);

        existing.ContentTypeId = contentTypeId;
        existing.SiteId = definition.SiteId;
        existing.Name = $"ct:{definition.SiteId}:{definition.Alias}";
        existing.IsPublished = true;

        if (changed)
        {
            existing.ScribanTemplate = template;
            existing.DataSchema?.Dispose();
            existing.DataSchema = JsonDocument.Parse(schemaText);
            existing.Version++;
        }

        session.Store(existing);
        return Prelude.Ok<NoneType, AeroError>(Prelude.None);
    }
}
