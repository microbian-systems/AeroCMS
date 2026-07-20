using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Content.Templating;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;

namespace Aero.Cms.Core.Content.Services;

/// <summary>
/// Implements <see cref="IContentTypeService"/> with a Sable document session.
/// </summary>
public sealed class AeroContentTypeService(
    IDocumentSession session,
    IEnumerable<IFieldTemplateSnippet> snippets,
    ScribanTemplateValidator templateValidator) : IContentTypeService
{
    /// <inheritdoc />
    public async Task<Result<ContentTypeDefinition, AeroError>> GetByAliasAsync(long siteId, string alias, CancellationToken ct = default)
    {
        var doc = await session.Query<ContentTypeDocument>()
            .FirstOrDefaultAsync(x => x.SiteId == siteId && x.Alias == alias, ct);

        if (doc is null)
            return Prelude.Fail<ContentTypeDefinition, AeroError>(AeroError.CreateError($"Content type '{alias}' not found."));
        return Prelude.Ok<ContentTypeDefinition, AeroError>(Map(doc));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<ContentTypeDefinition>, AeroError>> GetAllAsync(long siteId, CancellationToken ct = default)
    {
        var docs = await session.Query<ContentTypeDocument>().Where(x => x.SiteId == siteId).ToListAsync(ct);
        return Prelude.Ok<IReadOnlyList<ContentTypeDefinition>, AeroError>(docs.Select(Map).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<ContentTypeDefinition, AeroError>> SaveAsync(ContentTypeDefinition definition, CancellationToken ct = default)
    {
        ContentTypeDocument? stored = null;
        if (definition.Id != 0)
        {
            stored = await session.LoadAsync<ContentTypeDocument>(definition.Id, ct);
            if (stored is null || stored.SiteId != definition.SiteId)
                return Prelude.Fail<ContentTypeDefinition, AeroError>(AeroError.NotFoundError($"Content type '{definition.Id}' not found."));
        }

        var existing = await session.Query<ContentTypeDocument>()
            .FirstOrDefaultAsync(x => x.SiteId == definition.SiteId && x.Alias == definition.Alias, ct);

        if (existing is not null && existing.Id != definition.Id)
        {
            return Prelude.Fail<ContentTypeDefinition, AeroError>(
                AeroError.CreateError($"A content type with alias '{definition.Alias}' already exists for this site."));
        }

        var templateValidation = PrepareTemplate(definition);
        if (templateValidation is Result<NoneType, AeroError>.Failure templateFailure)
            return templateFailure.Error;

        var doc = new ContentTypeDocument
        {
            Id = stored?.Id ?? Snowflake.NewId(),
            SiteId = stored?.SiteId ?? definition.SiteId,
            Alias = definition.Alias,
            Name = definition.Name,
            Description = definition.Description,
            Category = definition.Category,
            Icon = definition.Icon,
            AllowPublicUrl = definition.AllowPublicUrl,
            HideFromSearch = definition.HideFromSearch,
            Fields = definition.Fields,
            ScribanTemplate = definition.ScribanTemplate,
            ScheduleConfig = definition.ScheduleConfig
        };

        session.Store(doc);
        await session.SaveChangesAsync(ct);
        definition.Id = doc.Id;
        return Prelude.Ok<ContentTypeDefinition, AeroError>(definition);
    }

    /// <inheritdoc />
    public async Task<Result<bool, AeroError>> DeleteAsync(long siteId, string alias, CancellationToken ct = default)
    {
        var doc = await session.Query<ContentTypeDocument>()
            .FirstOrDefaultAsync(x => x.SiteId == siteId && x.Alias == alias, ct);

        if (doc is null)
            return Prelude.Ok<bool, AeroError>(false);

        session.Delete(doc);
        await session.SaveChangesAsync(ct);
        return Prelude.Ok<bool, AeroError>(true);
    }

    private static ContentTypeDefinition Map(ContentTypeDocument doc) => new()
    {
        Id = doc.Id, SiteId = doc.SiteId, Alias = doc.Alias, Name = doc.Name, Description = doc.Description,
        Category = doc.Category, Icon = doc.Icon, AllowPublicUrl = doc.AllowPublicUrl, HideFromSearch = doc.HideFromSearch, Fields = doc.Fields,
        ScribanTemplate = doc.ScribanTemplate, ScheduleConfig = doc.ScheduleConfig
    };

    private Result<NoneType, AeroError> PrepareTemplate(ContentTypeDefinition definition)
    {
        var template = string.IsNullOrWhiteSpace(definition.ScribanTemplate)
            ? ContentTypeTemplateGenerator.GenerateTemplate(definition, snippets)
            : definition.ScribanTemplate;
        template = ContentTypeTemplateGenerator.NormalizeTemplate(template, definition.Fields);
        definition.ScribanTemplate = template;
        using var schema = ContentTypeSchemaGenerator.GenerateSchema(definition);

        var validation = templateValidator.Validate(template, schema);
        if (validation is Result<NoneType, AeroError>.Failure)
            return validation;
        return Prelude.Ok<NoneType, AeroError>(Prelude.None);
    }
}
