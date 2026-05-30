using Aero.Cms.Abstractions.Content;
using Aero.Core;
using Aero.Core.Railway;
using Marten;

namespace Aero.Cms.Core.Content.Services;

public sealed class MartenContentTypeService(IDocumentSession session) : IContentTypeService
{
    public async Task<Result<ContentTypeDefinition, AeroError>> GetByAliasAsync(long siteId, string alias, CancellationToken ct = default)
    {
        var doc = await session.Query<ContentTypeDocument>()
            .FirstOrDefaultAsync(x => x.SiteId == siteId && x.Alias == alias, ct);

        if (doc is null)
            return Prelude.Fail<ContentTypeDefinition, AeroError>(AeroError.CreateError($"Content type '{alias}' not found."));
        return Prelude.Ok<ContentTypeDefinition, AeroError>(Map(doc));
    }

    public async Task<Result<IReadOnlyList<ContentTypeDefinition>, AeroError>> GetAllAsync(long siteId, CancellationToken ct = default)
    {
        var docs = await session.Query<ContentTypeDocument>().Where(x => x.SiteId == siteId).ToListAsync(ct);
        return Prelude.Ok<IReadOnlyList<ContentTypeDefinition>, AeroError>(docs.Select(Map).ToList());
    }

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
            Fields = definition.Fields,
            ScribanTemplate = definition.ScribanTemplate,
            RenderMode = definition.RenderMode
        };
        session.Store(doc);
        await session.SaveChangesAsync(ct);
        definition.Id = doc.Id;
        return Prelude.Ok<ContentTypeDefinition, AeroError>(definition);
    }

    private static ContentTypeDefinition Map(ContentTypeDocument doc) => new()
    {
        Id = doc.Id, SiteId = doc.SiteId, Alias = doc.Alias, Name = doc.Name, Description = doc.Description,
        Category = doc.Category, Icon = doc.Icon, AllowPublicUrl = doc.AllowPublicUrl, Fields = doc.Fields,
        ScribanTemplate = doc.ScribanTemplate, RenderMode = doc.RenderMode
    };
}
