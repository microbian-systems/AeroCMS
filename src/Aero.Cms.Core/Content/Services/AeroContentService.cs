using Aero.Cms.Abstractions.Content;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using System.Globalization;

namespace Aero.Cms.Core.Content.Services;

/// <summary>
/// Implements <see cref="IContentService"/> with a Sable document session.
/// </summary>
public sealed class AeroContentService(IDocumentSession session) : IContentService
{
    /// <inheritdoc />
    public async Task<Result<ContentItem, AeroError>> LoadAsync(long siteId, long id, CancellationToken ct = default)
    {
        var item = await session.LoadAsync<ContentItem>(id, ct);
        return item is null || item.SiteId != siteId
            ? Prelude.Fail<ContentItem, AeroError>(AeroError.CreateError($"Content item '{id}' not found."))
            : Prelude.Ok<ContentItem, AeroError>(item);
    }

    /// <inheritdoc />
    public async Task<Result<ContentItem, AeroError>> GetBySlugAsync(long siteId, string slug, CancellationToken ct = default)
    {
        var item = await session.Query<ContentItem>().FirstOrDefaultAsync(x => x.SiteId == siteId && x.Slug == slug, ct);
        return item is null
            ? Prelude.Fail<ContentItem, AeroError>(AeroError.CreateError($"Content item with slug '{slug}' not found."))
            : Prelude.Ok<ContentItem, AeroError>(item);
    }

    /// <inheritdoc />
    public async Task<Result<ContentItem, AeroError>> GetBySlugAndTypeAsync(
    long siteId,
    string contentTypeAlias,
    string culture,
    string slug,
    CancellationToken ct = default)
    {
        var normalizedCulture = NormalizeCulture(culture);
        var item = await session.Query<ContentItem>()
            .FirstOrDefaultAsync(x =>
                x.SiteId == siteId &&
                x.ContentTypeAlias == contentTypeAlias &&
                x.Culture == normalizedCulture &&
                x.Slug == slug,
                ct);
        return item is null
            ? Prelude.Fail<ContentItem, AeroError>(AeroError.CreateError(
                $"Content item with slug '{slug}' and culture '{normalizedCulture}' not found in type '{contentTypeAlias}'."))
            : Prelude.Ok<ContentItem, AeroError>(item);
    }

    /// <inheritdoc />
    public async Task<Result<ContentItem, AeroError>> SaveAsync(ContentItem item, CancellationToken ct = default)
    {
        ContentItem? existing = null;
        if (item.Id != 0)
        {
            existing = await session.LoadAsync<ContentItem>(item.Id, ct);
            if (existing is null || existing.SiteId != item.SiteId)
                return Prelude.Fail<ContentItem, AeroError>(AeroError.NotFoundError($"Content item '{item.Id}' not found."));

            item.SiteId = existing.SiteId;
            item.ContentTypeAlias = existing.ContentTypeAlias;
            item.TranslationGroupId = existing.TranslationGroupId;
            item.SourceItemId = existing.SourceItemId;
        }

        var type = await session.Query<ContentTypeDocument>()
            .FirstOrDefaultAsync(x => x.SiteId == item.SiteId && x.Alias == item.ContentTypeAlias, ct);
        if (type is null)
            return Prelude.Fail<ContentItem, AeroError>(AeroError.NotFoundError("Content type or related content was not found."));

        if (item.SourceItemId is { } sourceId && !await BelongsToSiteAsync(item.SiteId, sourceId, ct))
            return Prelude.Fail<ContentItem, AeroError>(AeroError.NotFoundError("Content type or related content was not found."));

        if (item.TranslationGroupId is { } groupId && groupId != item.Id &&
            !await TranslationGroupBelongsToSiteAsync(item.SiteId, groupId, ct))
            return Prelude.Fail<ContentItem, AeroError>(AeroError.NotFoundError("Content type or related content was not found."));

        if (item.ParentId is { } parentId && !await BelongsToSiteAsync(item.SiteId, parentId, ct))
            return Prelude.Fail<ContentItem, AeroError>(AeroError.NotFoundError("Content type or related content was not found."));

        foreach (var field in type.Fields.Where(x => x.FieldType == "reference"))
        {
            if (!item.Fields.TryGetValue(field.Name, out var value) || value.ValueKind is System.Text.Json.JsonValueKind.Null)
                continue;
            var multiple = field.Settings.TryGetValue("allowMultiple", out var setting) &&
                           setting.ValueKind == System.Text.Json.JsonValueKind.True;
            if (!multiple
                && value.ValueKind == System.Text.Json.JsonValueKind.String
                && string.IsNullOrWhiteSpace(value.GetString()))
            {
                continue;
            }
            var values = multiple ? value.EnumerateArray().ToArray() : [value];
            foreach (var reference in values)
            {
                if (reference.ValueKind != System.Text.Json.JsonValueKind.String ||
                    !long.TryParse(reference.GetString(), out var referenceId) ||
                    !await BelongsToSiteAsync(item.SiteId, referenceId, ct))
                    return Prelude.Fail<ContentItem, AeroError>(AeroError.NotFoundError("Content type or related content was not found."));
            }
        }

        if (item.Id == 0)
            item.Id = Snowflake.NewId();
        item.TranslationGroupId ??= item.Id;
        session.Store(item);
        await session.SaveChangesAsync(ct);
        return Prelude.Ok<ContentItem, AeroError>(item);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(long siteId, long id, CancellationToken ct = default)
        => await session.LoadAsync<ContentItem>(id, ct) is { } item && item.SiteId == siteId;

    /// <inheritdoc />
    public async Task<Result<bool, AeroError>> DeleteAsync(long siteId, long id, CancellationToken ct = default)
    {
        var item = await session.LoadAsync<ContentItem>(id, ct);
        if (item is null || item.SiteId != siteId)
            return Prelude.Fail<bool, AeroError>(AeroError.NotFoundError($"Content item '{id}' not found."));

        var hasChildren = await session.Query<ContentItem>()
            .Where(candidate => candidate.SiteId == siteId && candidate.ParentId == id)
            .AnyAsync(ct);
        if (hasChildren)
        {
            return Prelude.Fail<bool, AeroError>(
                AeroError.ConflictError(
                    "This content item has children. Move or delete its children before deleting it."));
        }

        session.Delete(item);
        await session.SaveChangesAsync(ct);
        return Prelude.Ok<bool, AeroError>(true);
    }

    private static string NormalizeCulture(string? culture) =>
        string.IsNullOrWhiteSpace(culture)
            ? "en-US"
            : CultureInfo.GetCultureInfo(culture.Trim()).Name;

    private async Task<bool> BelongsToSiteAsync(long siteId, long id, CancellationToken ct)
        => await session.LoadAsync<ContentItem>(id, ct) is { } item && item.SiteId == siteId;

    private async Task<bool> TranslationGroupBelongsToSiteAsync(long siteId, long groupId, CancellationToken ct)
        => await session.Query<ContentItem>()
            .FirstOrDefaultAsync(x => x.SiteId == siteId && (x.Id == groupId || x.TranslationGroupId == groupId), ct)
            is not null;
}
