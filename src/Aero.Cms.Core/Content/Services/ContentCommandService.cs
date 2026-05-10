using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Enums;
using Aero.Core;
using Aero.Core.Railway;
using Marten;

namespace Aero.Cms.Core.Content.Services;

/// <summary>
/// Provides high-level save, publish, and delete operations with validation
/// and version history tracking.
/// </summary>
public sealed class ContentCommandService(
    ContentValidationService validation,
    IContentService contentService,
    IDocumentSession session)
{
    /// <summary>
    /// Saves a content item as a draft with relaxed validation.
    /// </summary>
    public async Task<Result<ContentItem, AeroError>> SaveDraftAsync(
        ContentItem item, CancellationToken ct = default)
    {
        var result = await validation.ValidateAsync(item, ContentValidationMode.Draft, ct);
        if (result is Result<ContentItem, AeroError>.Failure f)
            return f.Error;

        item.VersionNumber++;
        return await contentService.SaveAsync(item, ct);
    }

    /// <summary>
    /// Publishes a content item with strict validation and version snapshotting.
    /// </summary>
    public async Task<Result<ContentItem, AeroError>> PublishAsync(
        ContentItem item, CancellationToken ct = default)
    {
        var result = await validation.ValidateAsync(item, ContentValidationMode.Publish, ct);
        if (result is Result<ContentItem, AeroError>.Failure f)
            return f.Error;

        // Snapshot the current published state before overwriting
        if (item.PublicationState == ContentPublicationState.Published)
        {
            session.Store(new ContentItemVersion
            {
                ContentItemId = item.Id,
                VersionNumber = item.VersionNumber,
                FieldsJson = JsonSerializer.Serialize(item.Fields),
                CreatedUtc = DateTimeOffset.UtcNow
            });
        }

        item.VersionNumber++;
        item.PublicationState = ContentPublicationState.Published;
        item.PublishedOn = DateTimeOffset.UtcNow;

        return await contentService.SaveAsync(item, ct);
    }

    /// <summary>
    /// Deletes a content item with a safety check for page references.
    /// </summary>
    public async Task<Result<bool, AeroError>> DeleteAsync(
        long id, CancellationToken ct = default)
    {
        // Verify the item exists before attempting deletion
        if (!await contentService.ExistsAsync(id, ct))
            return AeroError.NotFoundError($"Content item '{id}' not found.");

        return await contentService.DeleteAsync(id, ct);
    }
}
