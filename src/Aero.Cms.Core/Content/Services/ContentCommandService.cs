using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Serialization;
using Aero.Cms.Abstractions.Enums;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;

namespace Aero.Cms.Core.Content.Services;

/// <summary>
/// Provides high-level save, publish, and delete operations with validation
/// and version history tracking.
/// </summary>
/// <remarks>
/// The service mutates caller-owned <see cref="ContentItem"/> instances after validation.
/// It does not provide a rollback contract if a later persistence operation fails.
/// Cancellation and storage-provider exceptions may propagate to the caller.
/// </remarks>
public sealed class ContentCommandService(
    ContentValidationService validation,
    IContentService contentService,
    IDocumentSession session)
{
    /// <summary>
    /// Validates an item using draft rules, increments its version, and commits it.
    /// </summary>
    /// <param name="item">The caller-owned item to validate and save.</param>
    /// <param name="ct">A token that can cancel validation or persistence.</param>
    /// <returns>
    /// The saved item, or a failed result when validation fails. The version number is
    /// incremented only after successful validation.
    /// </returns>
    /// <remarks>
    /// Draft validation does not run asynchronous validators or enforce fields that are
    /// required only for publication.
    /// </remarks>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> is canceled.</exception>
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
    /// Validates an item using publish rules, records eligible version history, marks it
    /// published, and commits it.
    /// </summary>
    /// <param name="item">The caller-owned item to validate and publish.</param>
    /// <param name="ct">A token that can cancel validation or persistence.</param>
    /// <returns>The published item, or a failed result when validation fails.</returns>
    /// <remarks>
    /// If <paramref name="item"/> is already published, a snapshot of its current field
    /// values and version number is stored before the item is overwritten. After validation,
    /// this method increments <see cref="ContentItem.VersionNumber"/>, sets publication state
    /// to published, and replaces the publication timestamp. These in-memory mutations are
    /// not rolled back if the subsequent save fails, and this API does not promise atomicity
    /// beyond the behavior of the supplied document session.
    /// </remarks>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> is canceled.</exception>
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
                FieldsJson = JsonSerializer.Serialize(
                    item.Fields,
                    ContentJsonContext.Default.DictionaryStringJsonElement),
                CreatedUtc = DateTimeOffset.UtcNow
            });
        }

        item.VersionNumber++;
        item.PublicationState = ContentPublicationState.Published;
        item.PublishedOn = DateTimeOffset.UtcNow;

        return await contentService.SaveAsync(item, ct);
    }

    /// <summary>
    /// Verifies that a content item exists, then delegates deletion and commit.
    /// </summary>
    /// <param name="id">The content-item identifier.</param>
    /// <param name="ct">A token that can cancel the existence check or deletion.</param>
    /// <returns>
    /// A not-found failure when the item does not exist; otherwise the result returned by
    /// <see cref="IContentService.DeleteAsync"/>.
    /// </returns>
    /// <remarks>
    /// This method does not inspect pages or other content for references to the item and
    /// therefore does not provide reference-safety checks.
    /// </remarks>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> is canceled.</exception>
    public async Task<Result<bool, AeroError>> DeleteAsync(
        long siteId, long id, CancellationToken ct = default)
    {
        // Verify the item exists before attempting deletion
        if (!await contentService.ExistsAsync(siteId, id, ct))
            return AeroError.NotFoundError($"Content item '{id}' not found.");

        return await contentService.DeleteAsync(siteId, id, ct);
    }
}
